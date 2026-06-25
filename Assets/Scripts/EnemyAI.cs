using UnityEngine;

/// <summary>
/// Koridor düşmanı: player'ı takip, menzilde saldırı. Locomotion LateUpdate + execution order (Animator'dan sonra pozisyon).
/// FBX Loop Time. CC varsa Move(), yoksa transform. driveMoveSpeedByIntent: MoveSpeed niyet tabanlı.
/// İstersen <see cref="locomotionTransform"/> ile hareket kökünü elle ver (mesh/üst boş ayrımı).
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Boşsa kendi GameObject'inde, yoksa çocuklarda Animator aranır (mesh altı yaygın).")]
    [SerializeField] private Animator animatorOverride;

    [Header("Hareket")]
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float rotationSpeed = 10f;
    [Tooltip("0 veya küçükse: saldırı menziline göre otomatik (ResolveAdvanceCutoff).")]
    [SerializeField] private float advanceStopDistance = 0f;
    [Tooltip("İleri durma mesafesine eklenir (metre). İsim tarihsel; CC olmasa da cutoff hesabında kullanılır.")]
    [SerializeField] private float ccStandoffPadding = 0.06f;
    [Tooltip("advance cutoff yakınında ileri hızı kırpar (menzil içi yumuşak yaklaşma).")]
    [SerializeField] private float approachDecelerationRange = 0.32f;
    [SerializeField] private float gravity = 10f;
    [Tooltip("İleri hareket + dönüşün uygulanacağı transform. Boşsa CC veya bu GameObject. Üst boş + mesh çocuk prefabında buraya üst kökü at.")]
    [SerializeField] private Transform locomotionTransform;

    [Header("İsteğe bağlı / CharacterController varsa")]
    [Tooltip("Sadece düşman veya sahnede CC–CC çarpışması varsa anlamlı. Saf transform + collider yoksa KAPALI bırak.")]
    [SerializeField] private bool ignorePhysicsBetweenEnemyAndPlayerLayers = false;
    [Tooltip(">0 ise bu düşmandaki CharacterController.skinWidth (prefabta CC yoksa etkisiz).")]
    [SerializeField] private float characterControllerSkinWidthOverride = 0f;

    [Header("Animator blend")]
    [Tooltip("Açıkken MoveSpeed fizik hızından türetilmez. İleri=run, yakın duruş=orbit walk.")]
    [SerializeField] private bool driveMoveSpeedByIntent = true;
    [Tooltip("driveMoveSpeedByIntent kapalıyken Animator damp (s).")]
    [SerializeField] private float moveSpeedParamDampTime = 0.1f;
    [Tooltip("Kovalarken blend (Monster01 run eşiği 1).")]
    [SerializeField] private float minChaseMoveSpeedParam = 1f;
    [Tooltip("Yakında ileri yokken ayakta shuffle (Monster01 walk ~0.45–0.5).")]
    [SerializeField] private float orbitWalkBlend = 0.5f;
    [Tooltip("Eski mod: orbit için CC hızı eşiği (intent modda kullanılmaz).")]
    [SerializeField] private float orbitVelocityCutoff = 0.18f;
    [Tooltip("Eski mod: orbit üst mesafe (intent modda kullanılmaz).")]
    [SerializeField] private float orbitRingPadding = 2.5f;

    [Header("Saldırı")]
    [SerializeField] private float attackRange = 1.7f;
    [Tooltip("Saldırı tetiklemek için üst sınır (attackRange ile birlikte min alınır).")]
    [SerializeField] private float attackCommitDistance = 1.45f;
    [SerializeField] private int attackDamage = 6;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.1f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float hitMoment = 0.5f;
    [SerializeField] private float hitMomentTolerance = 0.14f;
    [SerializeField] private float fallbackHitPadding = 0.5f;

    [Tooltip("Açıkken saldırı hasarı, animator geçişinde de uygulanır (IsInTransition sürekli true kalınca hasar hiç gitmeyebilir).")]
    [SerializeField] private bool allowDamageDuringAnimatorTransition = true;

    [Header("Animator")]
    [SerializeField] private string paramMoveSpeed = "MoveSpeed";
    [SerializeField] private string paramAttack = "Attack";
    [Tooltip("Animator state kısa adı (clip adı değil; Monster01_AC / InPlace’ta state: Attack).")]
    [SerializeField] private string attackStateName = "Attack";
    [Tooltip("Saldırı state’i hangi Animator layer’da (çoğu setup 0).")]
    [SerializeField] private int attackAnimatorLayerIndex = 0;
    [Tooltip("Monster01_AC: Get_Hit — hit tepkisinde orbit blend verme.")]
    [SerializeField] private string hitReactStateName = "Get_Hit";

    [Header("Aggro")]
    [SerializeField] private float aggroDelaySeconds = 0.4f;

    [Tooltip("Script ile hareket ediyorsan root motion çakışmasını önlemek için genelde açık bırak.")]
    [SerializeField] private bool disableAnimatorRootMotion = true;
    [Tooltip("Animator bazen kapalı objede donduruluyor; sorun yaşanırsa açı.")]
    [SerializeField] private bool animatorAlwaysAnimate = true;

    private Animator _animator;
    private EnemyHealth _health;
    private CharacterController _controller;
    /// <summary>CC'nin olduğu gövde; mesafe/hız/dönüş bunun üzerinden (script mesh çocukta olsa bile).</summary>
    private Transform _motionRoot;
    private Vector3 _velocity;
    private float _lastAttackTime;
    private bool _wasInAttack;
    private bool _hasDealtThisSwing;
    private float _spawnTime;
    private Vector3 _lastFramePosition;
    private bool _didLayerIgnoreSetup;

    private void Awake()
    {
        _animator = ResolveRuntimeAnimator(animatorOverride);

        if (_animator != null)
        {
            if (disableAnimatorRootMotion)
                _animator.applyRootMotion = false;
            if (animatorAlwaysAnimate)
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    /// <summary>Parent'ta boş Animator varken gerçek controller'ın çocuk mesh'te olduğu durumu çözer.</summary>
    private Animator ResolveRuntimeAnimator(Animator explicitRef)
    {
        if (explicitRef != null)
            return explicitRef;

        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a != null && a.runtimeAnimatorController != null)
                return a;
        }

        return GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        _health = GetComponent<EnemyHealth>();
        _controller = GetComponent<CharacterController>()
                      ?? GetComponentInParent<CharacterController>()
                      ?? GetComponentInChildren<CharacterController>(true);
        _motionRoot = ResolveMotionRoot();

        _spawnTime = Time.time;
        _lastFramePosition = _motionRoot.position;

        if (player == null && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
        }

        if (characterControllerSkinWidthOverride > 0f && _controller != null)
            _controller.skinWidth = characterControllerSkinWidthOverride;

        TrySetupIgnoreCollisionWithPlayer();

        // Duplicate / Instantiate sonrası: parametreler ve Animator penceresi doğru görünür ama model T-pose kalabilir
        // (Avatar–SkinnedMesh bağları senkron değil). Rebind kemik çıktısını bu instance’a yeniden bağlar.
        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    private Transform ResolveMotionRoot()
    {
        if (locomotionTransform != null)
            return locomotionTransform;
        if (_controller != null)
            return _controller.transform;
        return transform;
    }

    /// <summary>Sadece prefabta CC + layer çarpışması kullanıyorsan anlamlı.</summary>
    private void TrySetupIgnoreCollisionWithPlayer()
    {
        if (!ignorePhysicsBetweenEnemyAndPlayerLayers || _didLayerIgnoreSetup)
            return;
        if (player == null)
            return;

        int enemyLayer = _motionRoot.gameObject.layer;
        int plLayer = player.gameObject.layer;
        if (enemyLayer != plLayer)
            Physics.IgnoreLayerCollision(enemyLayer, plLayer, true);

        _didLayerIgnoreSetup = true;
    }

    private void EnsurePlayerReference()
    {
        if (player != null) return;
        if (string.IsNullOrEmpty(playerTag)) return;
        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null)
            player = go.transform;
    }

    private void LateUpdate()
    {
        if (_animator == null)
            return;

        if (_health != null && _health.IsDead)
        {
            _animator.SetFloat(paramMoveSpeed, 0f);
            return;
        }

        EnsurePlayerReference();
        if (player == null) return;

        TrySetupIgnoreCollisionWithPlayer();

        bool aggroReady = Time.time >= _spawnTime + Mathf.Max(0f, aggroDelaySeconds);
        if (!aggroReady)
        {
            _animator.SetFloat(paramMoveSpeed, 0f);
            return;
        }

        Vector3 toPlayer = player.position - _motionRoot.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dir = distance > 0.0001f ? toPlayer / distance : Vector3.zero;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            _motionRoot.rotation = Quaternion.Slerp(_motionRoot.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        float advanceCutoff = ResolveAdvanceCutoff();
        float effectiveAttackStart = Mathf.Min(attackRange, attackCommitDistance);
        // advanceCutoff < saldırı menzili olunca ara bölgede hâlâ "ileri" sayılıyordu; saldırı !shouldAdvance beklediği için tetiklenmiyordu.
        bool shouldAdvance = distance > advanceCutoff;
        if (distance <= effectiveAttackStart)
            shouldAdvance = false;

        float horizSpeed = moveSpeed;
        if (shouldAdvance && approachDecelerationRange > 0.001f)
        {
            float span = advanceCutoff + approachDecelerationRange;
            if (distance < span)
            {
                float t = (distance - advanceCutoff) / Mathf.Max(approachDecelerationRange, 0.001f);
                horizSpeed *= Mathf.Clamp01(t);
            }
        }

        if (shouldAdvance)
        {
            Vector3 move = dir * horizSpeed;
            if (_controller != null)
            {
                _velocity.x = move.x;
                _velocity.z = move.z;
                _velocity.y -= gravity * Time.deltaTime;
                _controller.Move(_velocity * Time.deltaTime);
            }
            else
                _motionRoot.position += new Vector3(move.x, 0f, move.z) * Time.deltaTime;
        }
        else
        {
            _velocity.x = _velocity.z = 0f;
            _velocity.y -= gravity * Time.deltaTime;
            if (_controller != null)
                _controller.Move(_velocity * Time.deltaTime);
        }

        float planarSpeed;
        if (_controller != null)
        {
            Vector3 v = _controller.velocity;
            planarSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        }
        else
        {
            Vector3 delta = _motionRoot.position - _lastFramePosition;
            delta.y = 0f;
            planarSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        _lastFramePosition = _motionRoot.position;

        bool inAttack = IsInAttackState();
        bool inHit =
            !string.IsNullOrEmpty(hitReactStateName) &&
            _animator.GetCurrentAnimatorStateInfo(0).IsName(hitReactStateName);
        float orbitRingLimit = attackRange + Mathf.Max(0f, orbitRingPadding);
        bool orbitFollowingLegacy =
            !shouldAdvance &&
            !inAttack &&
            !inHit &&
            distance <= orbitRingLimit &&
            planarSpeed < orbitVelocityCutoff;
        bool nearHoldStill = !shouldAdvance && !inAttack && !inHit;
        bool canFireAttack =
            !inAttack &&
            !inHit &&
            distance <= effectiveAttackStart &&
            Time.time >= _lastAttackTime + attackCooldown;

        float targetMoveSpeedParam = 0f;
        if (!inAttack && !inHit)
        {
            if (driveMoveSpeedByIntent)
            {
                if (shouldAdvance)
                    targetMoveSpeedParam = minChaseMoveSpeedParam;
                else if (canFireAttack)
                    targetMoveSpeedParam = 0f;
                else if (nearHoldStill)
                    targetMoveSpeedParam = orbitWalkBlend;
                else
                    targetMoveSpeedParam = 0f;
            }
            else
            {
                targetMoveSpeedParam = Mathf.Clamp01(planarSpeed / Mathf.Max(moveSpeed, 0.01f));
                if (shouldAdvance)
                    targetMoveSpeedParam = Mathf.Max(targetMoveSpeedParam, minChaseMoveSpeedParam);
                else if (orbitFollowingLegacy && orbitWalkBlend > 0f)
                    targetMoveSpeedParam = Mathf.Max(targetMoveSpeedParam, orbitWalkBlend);
            }
        }

        _animator.speed = 1f;
        if (driveMoveSpeedByIntent)
            _animator.SetFloat(paramMoveSpeed, targetMoveSpeedParam);
        else
        {
            float damp = Mathf.Max(0.001f, moveSpeedParamDampTime);
            _animator.SetFloat(paramMoveSpeed, targetMoveSpeedParam, damp, Time.deltaTime);
        }

        if (canFireAttack)
        {
            _animator.SetTrigger(paramAttack);
            _lastAttackTime = Time.time;
            _hasDealtThisSwing = false;
        }

        HandleAttackDamageWindow();
    }

    private float ResolveAdvanceCutoff()
    {
        float melee = Mathf.Min(attackRange, attackCommitDistance);

        float raw = advanceStopDistance > 0.02f
            ? advanceStopDistance
            : melee - 0.12f;

        raw += Mathf.Max(0f, ccStandoffPadding);
        raw = Mathf.Max(0.35f, raw);

        // Duruş çok uzakta kalırsa saldırı hiç tetiklenmez (deadlock).
        return Mathf.Min(raw, melee - 0.05f);
    }

    private bool TryGetAttackPhaseState(out float normalizedTime)
    {
        normalizedTime = 0f;
        int layer = attackAnimatorLayerIndex;
        if (_animator == null)
            return false;

        if (_animator.IsInTransition(layer))
        {
            AnimatorStateInfo cur = _animator.GetCurrentAnimatorStateInfo(layer);
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layer);

            if (cur.IsName(attackStateName))
            {
                normalizedTime = cur.normalizedTime % 1f;
                return true;
            }

            if (next.IsName(attackStateName))
            {
                normalizedTime = next.normalizedTime % 1f;
                return true;
            }

            return false;
        }

        AnimatorStateInfo s = _animator.GetCurrentAnimatorStateInfo(layer);
        if (!s.IsName(attackStateName))
            return false;

        normalizedTime = s.normalizedTime % 1f;
        return true;
    }

    private bool IsInAttackState()
    {
        return TryGetAttackPhaseState(out _);
    }

    private void HandleAttackDamageWindow()
    {
        if (_health != null && _health.IsDead) return;

        if (!TryGetAttackPhaseState(out float normT))
        {
            _wasInAttack = false;
            _hasDealtThisSwing = false;
            return;
        }

        if (!_wasInAttack)
        {
            _wasInAttack = true;
            _hasDealtThisSwing = false;
        }

        if (_hasDealtThisSwing)
            return;

        if (!allowDamageDuringAnimatorTransition && _animator.IsInTransition(attackAnimatorLayerIndex))
            return;

        if (normT + hitMomentTolerance < hitMoment)
            return;

        bool ok = DealDamageToPlayer();
        _hasDealtThisSwing = ok;
    }

    private static PlayerHealth ResolvePlayerHealth(Transform actor)
    {
        if (actor == null) return null;
        PlayerHealth ph = actor.GetComponent<PlayerHealth>();
        if (ph != null) return ph;
        ph = actor.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph;
        return actor.GetComponentInChildren<PlayerHealth>(true);
    }

    private bool DealDamageToPlayer()
    {
        EnsurePlayerReference();

        Transform point = attackPoint != null ? attackPoint : (_motionRoot != null ? _motionRoot : transform);
        int mask = playerLayer.value != 0 ? playerLayer.value : ~0;

        Collider[] hits = Physics.OverlapSphere(point.position, attackRadius, mask, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph == null) ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph == null) ph = hit.GetComponentInChildren<PlayerHealth>(true);
            if (ph != null && !ph.IsDead)
            {
                ph.TakeDamage(attackDamage);
                return true;
            }
        }

        if (player != null)
        {
            PlayerHealth ph = ResolvePlayerHealth(player);
            if (ph == null || ph.IsDead)
                return false;

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.GetComponentInParent<CharacterController>();
            Collider playerCol = player.GetComponent<Collider>();
            if (playerCol == null) playerCol = player.GetComponentInChildren<Collider>(true);
            if (playerCol == null) playerCol = player.GetComponentInParent<Collider>();

            Vector3 closest;
            if (cc != null)
                closest = cc.ClosestPoint(point.position);
            else if (playerCol != null)
                closest = playerCol.ClosestPoint(point.position);
            else
                closest = player.position;

            Vector3 diff = closest - point.position;
            diff.y = 0f;
            float maxDist = attackRadius + fallbackHitPadding;
            if (diff.magnitude <= maxDist)
            {
                ph.TakeDamage(attackDamage);
                return true;
            }
        }

        // BossAI ile aynı son emniyet: attackPoint / küre kaçırsa bile gerçekten yakınsa hasar (pivot farkı, boş attackPoint).
        if (player != null)
        {
            PlayerHealth ph = ResolvePlayerHealth(player);
            if (ph != null && !ph.IsDead)
            {
                Vector3 toPlayer = player.position - _motionRoot.position;
                toPlayer.y = 0f;
                float bodyDist = toPlayer.magnitude;
                if (bodyDist <= attackRange + 0.2f)
                {
                    ph.TakeDamage(attackDamage);
                    return true;
                }
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform p = attackPoint != null ? attackPoint : transform;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(p.position, attackRadius);
    }
}
