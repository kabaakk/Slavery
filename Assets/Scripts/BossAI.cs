using UnityEngine;

/// <summary>
/// Minotaur (ve genel boss) için: player takip, hareket ve animator parametreleri.
/// Animator'da MoveSpeed, Attack, Hit, Death parametreleri kullanılır.
/// </summary>
[RequireComponent(typeof(Animator))]
public class BossAI : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform player;
    [Tooltip("Player bulunamazsa GameObject.FindGameObjectWithTag ile aranır.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Hareket")]
    [SerializeField] private float moveSpeed = 4.4f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stopDistance = 2.2f;
    [SerializeField] private float chaseReacquireDistance = 2.55f;
    [SerializeField] private float gravity = 10f;

    [Header("Saldırı")]
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 0.9f;
    [SerializeField] private int attackDamage = 9;
    public int AttackDamage => attackDamage;
    public float AttackCooldownSeconds => attackCooldown;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;
    public float AttackRadius => attackRadius;
    public float KickAttackRadius => kickAttackRadius;
    public float FallbackHitPadding => fallbackHitPadding;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform kickAttackPoint;
    [SerializeField] private float attackRadius = 1.35f;
    [SerializeField] private float kickAttackRadius = 1.35f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float attack1HitTime = 0.55f;
    [SerializeField] private float attack2HitTime = 0.52f;
    [SerializeField] private float attack3HitTime = 0.58f;
    [SerializeField] private float attack4HitTime = 0.50f; // kick
    [SerializeField] private float attack5HitTime = 0.50f; // kick
    [SerializeField] private float hitMomentTolerance = 0.16f;
    [SerializeField] private float fallbackHitPadding = 0.7f;
    [Tooltip("Boss bu kadar yakın değilse saldırıyı başlatmaz; koşu->saldırı ping-pong'unu azaltır.")]
    [SerializeField] private float attackCommitDistance = 1.95f;

    [Header("Animator parametre isimleri (Minotaur controller ile aynı olmalı)")]
    [SerializeField] private string paramMoveSpeed = "MoveSpeed";
    [SerializeField] private string paramMoveX = "MoveX";
    [SerializeField] private string paramMoveZ = "MoveZ";

    [Header("Attack tetikleyicileri (her biri ayrı Trigger)")]
    [SerializeField] private string paramAttack1 = "Attack1";
    [SerializeField] private string paramAttack2 = "Attack2";
    [SerializeField] private string paramAttack3 = "Attack3";
    [SerializeField] private string paramAttack4 = "Attack4";
    [SerializeField] private string paramAttack5 = "Attack5";

    [Header("Diğer tetikleyiciler")]
    [SerializeField] private string paramHit = "Hit";
    [SerializeField] private string paramDeath = "Death";

    private Animator _animator;
    private BossHealth _bossHealth;
    private CharacterController _controller;
    private Vector3 _velocity;
    private float _lastAttackTime;
    private bool _wasInAttackState;
    private bool _hasAppliedDamageThisAttack;
    private int _currentAttackIndex;
    private int _lastAttackStateHash;
    private Vector3 _lastFramePosition;
    private bool _isChasingState = true;

    private void Start()
    {
        _animator = GetComponent<Animator>();
        _bossHealth = GetComponent<BossHealth>();
        _controller = GetComponent<CharacterController>();
        _lastFramePosition = transform.position;

        if (player == null && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
        }
    }

    private void Update()
    {
        if (_bossHealth == null)
            _bossHealth = GetComponent<BossHealth>();

        if (_bossHealth != null && _bossHealth.IsDead)
        {
            if (_animator != null)
            {
                _animator.SetFloat(paramMoveSpeed, 0f);
                _animator.SetFloat(paramMoveX, 0f);
                _animator.SetFloat(paramMoveZ, 0f);
            }
            return;
        }

        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dirToPlayer = distance > 0.0001f ? toPlayer / distance : Vector3.zero;

        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        float animSpeed = 0f;
        float moveX = 0f, moveZ = 0f;
        // Hareket: hedefe yaklaş ama histerezis ile karar ver (sınırda ping-pong olmasın).
        if (_isChasingState)
        {
            if (distance <= stopDistance)
                _isChasingState = false;
        }
        else
        {
            if (distance >= chaseReacquireDistance)
                _isChasingState = true;
        }
        bool isChasing = _isChasingState;
        if (_bossHealth != null)
            _bossHealth.SetHitReactionSuppressedRuntime(isChasing);

        if (isChasing)
        {
            Vector3 move = dirToPlayer * moveSpeed;
            if (_controller != null)
            {
                _velocity.x = move.x;
                _velocity.z = move.z;
                _velocity.y -= gravity * Time.deltaTime;
                _controller.Move(_velocity * Time.deltaTime);
            }
            else
            {
                transform.position += new Vector3(move.x, 0f, move.z) * Time.deltaTime;
            }
            animSpeed = 1f;
            // Yön: karakterin ileri/sağına göre (-1 ile 1 arası)
            moveZ = Vector3.Dot(dirToPlayer, transform.forward);
            moveX = Vector3.Dot(dirToPlayer, transform.right);
        }
        else
        {
            _velocity.x = _velocity.z = 0f;
            _velocity.y -= gravity * Time.deltaTime;
            if (_controller != null)
                _controller.Move(_velocity * Time.deltaTime);
        }

        // Boss gerçekten yer değiştiriyorsa (attack/hit state sonrası dahil),
        // koşu parametresi açık kalsın.
        Vector3 delta = transform.position - _lastFramePosition;
        delta.y = 0f;
        float planarSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        if (planarSpeed > 0.05f)
            animSpeed = Mathf.Max(animSpeed, 1f);
        _lastFramePosition = transform.position;

        _animator.SetFloat(paramMoveSpeed, animSpeed);
        _animator.SetFloat(paramMoveX, moveX);
        _animator.SetFloat(paramMoveZ, moveZ);

        // Saldırı: attackRange içinde + gerçekten commit mesafesine girdiyse tetikle.
        float effectiveAttackStartDistance = Mathf.Min(attackRange, attackCommitDistance);
        if (!isChasing && !IsInAttackState() && distance <= effectiveAttackStartDistance && Time.time >= _lastAttackTime + attackCooldown)
        {
            TriggerAttack();
            _lastAttackTime = Time.time;
        }

        HandleAttackDamageWindow();
    }

    /// <summary>Kod ile saldırı tetiklemek için (ileride attack/health entegrasyonunda).</summary>
    public void TriggerAttack()
    {
        // 1 ile 5 arasında rastgele bir saldırı seç
        int index = Random.Range(1, 6); // 1,2,3,4,5
        switch (index)
        {
            case 1:
                _animator.SetTrigger(paramAttack1);
                break;
            case 2:
                _animator.SetTrigger(paramAttack2);
                break;
            case 3:
                _animator.SetTrigger(paramAttack3);
                break;
            case 4:
                _animator.SetTrigger(paramAttack4);
                break;
            case 5:
                _animator.SetTrigger(paramAttack5);
                break;
        }
    }

    /// <summary>
    /// Animation Event kullanmadan, attack state içindeyken tek seferlik hasar uygular.
    /// </summary>
    private void HandleAttackDamageWindow()
    {
        if (_animator == null) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        bool inAttack =
            state.IsName("Attack1") ||
            state.IsName("Attack2") ||
            state.IsName("Attack3") ||
            state.IsName("Attack4") ||
            state.IsName("Attack5");

        if (!inAttack)
        {
            _wasInAttackState = false;
            _hasAppliedDamageThisAttack = false;
            return;
        }

        if (!_wasInAttackState)
        {
            _wasInAttackState = true;
            _hasAppliedDamageThisAttack = false;
            _currentAttackIndex = GetCurrentAttackIndex(state);
            _lastAttackStateHash = state.shortNameHash;
        }
        else if (_lastAttackStateHash != state.shortNameHash)
        {
            // Attack1 -> Attack2 gibi zincirlerde non-attack state'e çıkmadan da
            // yeni saldırı başladığını algıla ve hasar penceresini yeniden aç.
            _hasAppliedDamageThisAttack = false;
            _currentAttackIndex = GetCurrentAttackIndex(state);
            _lastAttackStateHash = state.shortNameHash;
        }

        if (_hasAppliedDamageThisAttack) return;

        // normalizedTime 0-1 aralığında: hit moment geçildiği anda tek sefer hasar.
        // Böylece düşük FPS/transition anlarında pencere kaçmaz.
        float t = state.normalizedTime % 1f;
        float hitMoment = GetHitMomentForAttack(_currentAttackIndex);
        if (t + hitMomentTolerance < hitMoment) return;

        bool didDamage = DealDamageToPlayer(_currentAttackIndex);
        _hasAppliedDamageThisAttack = didDamage;
    }

    /// <summary>
    /// Animation Event ile çağrılır. Silahın/elin etrafında bir küre ile player'ı bulup hasar uygular.
    /// </summary>
    public bool DealDamageToPlayer(int attackIndex = 1)
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        float radius = attackRadius;
        bool isKick = attackIndex == 4 || attackIndex == 5;
        if (isKick && kickAttackPoint != null)
        {
            point = kickAttackPoint;
            radius = kickAttackRadius;
        }

        bool appliedDamage = false;
        int mask = playerLayer.value != 0 ? playerLayer.value : ~0;
        Collider[] hits = Physics.OverlapSphere(point.position, radius, mask);
        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph == null)
                ph = hit.GetComponentInParent<PlayerHealth>();

            if (ph != null)
            {
                ph.TakeDamage(attackDamage);
                appliedDamage = true;
                break; // Aynı saldiri ile sadece bir kez vur
            }
        }

        // Fallback: layer ayarı kaçırırsa oyuncuyu doğrudan referanstan kontrol et
        if (!appliedDamage && player != null)
        {
            PlayerHealth directPlayerHealth = player.GetComponent<PlayerHealth>();
            if (directPlayerHealth == null)
                directPlayerHealth = player.GetComponentInParent<PlayerHealth>();

            CharacterController playerCc = player.GetComponent<CharacterController>();
            if (playerCc == null)
                playerCc = player.GetComponentInParent<CharacterController>();

            if (directPlayerHealth != null)
            {
                Vector3 closestPoint = playerCc != null ? playerCc.ClosestPoint(point.position) : player.position;
                // Dikey fark hit'i kaçırmasın: boss/player pivot yüksekliği farklı olabilir.
                Vector3 diff = closestPoint - point.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist <= radius + fallbackHitPadding)
                {
                    directPlayerHealth.TakeDamage(attackDamage);
                    appliedDamage = true;
                }
            }
        }

        // Son emniyet: point referansları yanlış/uzakta olsa bile,
        // boss ile player gerçekten yakınsa hasar kaçmasın.
        if (!appliedDamage && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float bodyDist = toPlayer.magnitude;
            if (bodyDist <= attackRange + 0.2f)
            {
                PlayerHealth directPlayerHealth = player.GetComponent<PlayerHealth>();
                if (directPlayerHealth == null)
                    directPlayerHealth = player.GetComponentInParent<PlayerHealth>();

                if (directPlayerHealth != null)
                {
                    directPlayerHealth.TakeDamage(attackDamage);
                    appliedDamage = true;
                }
            }
        }

        return appliedDamage;
    }

    private int GetCurrentAttackIndex(AnimatorStateInfo state)
    {
        if (state.IsName("Attack1")) return 1;
        if (state.IsName("Attack2")) return 2;
        if (state.IsName("Attack3")) return 3;
        if (state.IsName("Attack4")) return 4;
        if (state.IsName("Attack5")) return 5;
        return 1;
    }

    private float GetHitMomentForAttack(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1: return attack1HitTime;
            case 2: return attack2HitTime;
            case 3: return attack3HitTime;
            case 4: return attack4HitTime;
            case 5: return attack5HitTime;
            default: return 0.5f;
        }
    }

    private bool IsInAttackState()
    {
        if (_animator == null) return false;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        return
            state.IsName("Attack1") ||
            state.IsName("Attack2") ||
            state.IsName("Attack3") ||
            state.IsName("Attack4") ||
            state.IsName("Attack5");
    }

    private void OnDrawGizmosSelected()
    {
        Transform hammerPoint = attackPoint != null ? attackPoint : transform;
        Transform kickPoint = kickAttackPoint != null ? kickAttackPoint : hammerPoint;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hammerPoint.position, attackRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(kickPoint.position, kickAttackRadius);
    }

    /// <summary>Vurulma animasyonu.</summary>
    public void TriggerHit()
    {
        _animator.SetTrigger(paramHit);
    }

    /// <summary>Ölüm animasyonu.</summary>
    public void TriggerDeath()
    {
        _animator.SetTrigger(paramDeath);
    }

    /// <summary>Player referansı atanmamışsa Inspector'dan veya kod ile set et.</summary>
    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    /// <summary>LLM / zorluk: boss saldırı gücü.</summary>
    public void SetAttackDifficulty(int damage, float cooldownSeconds)
    {
        attackDamage = Mathf.Clamp(damage, 1, 200);
        attackCooldown = Mathf.Clamp(cooldownSeconds, 0.3f, 10f);
    }

    /// <summary>
    /// LLM / mock: boss combat tuning (tempo + menzil + hareket).
    /// </summary>
    public void SetCombatTuning(
        int damage,
        float cooldownSeconds,
        float newMoveSpeed,
        float newAttackRange,
        float newAttackRadius,
        float newKickAttackRadius,
        float newFallbackHitPadding)
    {
        attackDamage = Mathf.Clamp(damage, 1, 200);
        attackCooldown = Mathf.Clamp(cooldownSeconds, 0.3f, 10f);
        moveSpeed = Mathf.Clamp(newMoveSpeed, 1.5f, 8f);
        attackRange = Mathf.Clamp(newAttackRange, 1f, 8f);
        attackRadius = Mathf.Clamp(newAttackRadius, 0.8f, 8f);
        kickAttackRadius = Mathf.Clamp(newKickAttackRadius, 0.8f, 8f);
        fallbackHitPadding = Mathf.Clamp(newFallbackHitPadding, 0f, 3f);
    }
}
