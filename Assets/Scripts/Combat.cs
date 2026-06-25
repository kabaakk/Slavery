using UnityEngine;

namespace Retro.ThirdPersonCharacter
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Animator))]
    public class Combat : MonoBehaviour
    {
        private const string attackTriggerName = "Attack";
        private const string specialAttackTriggerName = "Ability";
        private const string attackStateName = "RFA_Attack";
        private const string specialAttackStateName = "RFA_Ability";

        private Animator _animator;
        private PlayerInput _playerInput;
        private PlayerHealth _playerHealth;
        private float _attackStartedAt;
        [SerializeField] private float attackFailSafeDuration = 1.2f;
        [Header("Hit sonrası kısa atak arası")]
        [SerializeField] private float hitRecoverAttackDelay = 0.07f;

        [Header("Boss / düşmana hasar (Animation Event yok)")]
        [SerializeField] private Transform weaponHitPoint;
        [SerializeField] private int lightAttackDamage = 14;
        [SerializeField] private int heavyAttackDamage = 22;
        [SerializeField] private float hitRadius = 1.35f;
        [SerializeField] private float lightHitNormalizedTime = 0.45f;
        [SerializeField] private float heavyHitNormalizedTime = 0.48f;
        [SerializeField] private float hitMomentTolerance = 0.12f;
        [SerializeField] private LayerMask bossLayer;
        [Tooltip("Koridor düşmanı collider katmanları (boşsa sphere’da boss katmanı veya tüm katmanlar).")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private float fallbackHitPadding = 0.7f;
        [Tooltip("Boş bırakılabilir: aşağıdaki tag veya sahnede tek BossHealth kullanılır.")]
        [SerializeField] private Transform bossReference;
        [SerializeField] private bool autoFindBossHealth = true;
        [SerializeField] private string bossTag = "Boss";
        [Tooltip("Boş: EnemyHealth otomatik aranır veya sphere ile bulunur.")]
        [SerializeField] private Transform enemyReference;
        [SerializeField] private bool autoFindEnemyHealth = true;
        [SerializeField] private string enemyTag = "Enemy";

        [Header("Debug")]
        [SerializeField] private bool debugCombatLogs = true;

        private bool _hasDealtDamageThisSwing;
        private BossHealth _cachedBossHealth;
        private EnemyHealth _cachedEnemyHealth;
        private bool _wasInHitStun;
        private float _attackBlockedUntil;

        public bool AttackInProgress {get; private set;} = false;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _playerInput = GetComponent<PlayerInput>();
            _playerHealth = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                AttackInProgress = false;
                _hasDealtDamageThisSwing = false;
                return;
            }

            bool inHitStun = _playerHealth != null && _playerHealth.IsInHitStun;
            if (inHitStun)
            {
                AttackInProgress = false;
                _hasDealtDamageThisSwing = false;
                _wasInHitStun = true;
                return;
            }

            if (_wasInHitStun)
            {
                _wasInHitStun = false;
                _attackBlockedUntil = Mathf.Max(_attackBlockedUntil, Time.time + Mathf.Max(0f, hitRecoverAttackDelay));
            }

            if (Time.time < _attackBlockedUntil)
                return;

            if(_playerInput.AttackInput && !AttackInProgress)
            {
                Attack();
            }
            else if (_playerInput.SpecialAttackInput && !AttackInProgress)
            {
                SpecialAttack();
            }

            UpdateAttackState();
            HandlePlayerMeleeHit();
        }

        private void SetAttackStart()
        {
            AttackInProgress = true;
        }

        private void SetAttackEnd()
        {
            AttackInProgress = false;
        }

        private void Attack()
        {
            AttackInProgress = true;
            _attackStartedAt = Time.time;
            _animator.SetTrigger(attackTriggerName);
        }

        private void SpecialAttack()
        {
            AttackInProgress = true;
            _attackStartedAt = Time.time;
            _animator.SetTrigger(specialAttackTriggerName);
        }

        private void UpdateAttackState()
        {
            if (!AttackInProgress || _animator == null) return;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            bool inAttackState = state.IsName(attackStateName) || state.IsName(specialAttackStateName);

            // Attack state'inden çıkınca hareket kilidi kalksın
            if (!inAttackState && !_animator.IsInTransition(0))
            {
                AttackInProgress = false;
                return;
            }

            // Animator geçişi/event kaçırırsa sonsuza kadar kilitlenmesin
            if (Time.time - _attackStartedAt > attackFailSafeDuration)
                AttackInProgress = false;
        }

        private void HandlePlayerMeleeHit()
        {
            if (!AttackInProgress || _animator == null)
            {
                _hasDealtDamageThisSwing = false;
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            bool light = state.IsName(attackStateName);
            bool heavy = state.IsName(specialAttackStateName);
            if (!light && !heavy)
                return;

            if (_animator.IsInTransition(0) || _hasDealtDamageThisSwing)
                return;

            float hitMoment = light ? lightHitNormalizedTime : heavyHitNormalizedTime;
            float t = state.normalizedTime % 1f;
            if (Mathf.Abs(t - hitMoment) > hitMomentTolerance)
                return;

            int dmg = light ? lightAttackDamage : heavyAttackDamage;
            if (debugCombatLogs)
                Debug.Log($"[Combat] Vuruş penceresi (t={t:F2}, hedef={hitMoment:F2}) → hasar denemesi: {(light ? "Light" : "Heavy")} dmg={dmg}");

            DealMeleeDamage(dmg);
            _hasDealtDamageThisSwing = true;
        }

        private int BuildMeleeOverlapMask()
        {
            int a = bossLayer.value;
            int b = enemyLayer.value;
            int combined = a | b;
            return combined != 0 ? combined : ~0;
        }

        private void DealMeleeDamage(int damage)
        {
            Transform point = weaponHitPoint != null ? weaponHitPoint : transform;
            bool applied = false;

            int mask = BuildMeleeOverlapMask();
            Collider[] hits = Physics.OverlapSphere(point.position, hitRadius, mask);
            if (debugCombatLogs)
                Debug.Log($"[Combat] OverlapSphere pos={point.position} r={hitRadius} mask={mask} → {hits.Length} collider");

            foreach (var hit in hits)
            {
                if (debugCombatLogs)
                    Debug.Log($"[Combat]   çarpan: {hit.name} (layer={LayerMask.LayerToName(hit.gameObject.layer)})");

                BossHealth bh = hit.GetComponent<BossHealth>();
                if (bh == null) bh = hit.GetComponentInParent<BossHealth>();
                if (bh != null && !bh.IsDead)
                {
                    int hpBefore = bh.CurrentHealth;
                    RunStatisticsTracker.Instance?.RegisterMeleeHitLanded();
                    bh.TakeDamage(damage);
                    applied = true;
                    if (debugCombatLogs)
                        Debug.Log($"[Combat] ✓ BOSS HIT: damage={damage} hp {hpBefore} → {bh.CurrentHealth}");
                    break;
                }
            }

            if (!applied)
            {
                foreach (var hit in hits)
                {
                    EnemyHealth eh = hit.GetComponent<EnemyHealth>();
                    if (eh == null) eh = hit.GetComponentInParent<EnemyHealth>();
                    if (eh != null && !eh.IsDead)
                    {
                        int hpBefore = eh.CurrentHealth;
                        RunStatisticsTracker.Instance?.RegisterMeleeHitLanded();
                        eh.TakeDamage(damage);
                        applied = true;
                        if (debugCombatLogs)
                            Debug.Log($"[Combat] ✓ ENEMY HIT: damage={damage} hp {hpBefore} → {eh.CurrentHealth}");
                        break;
                    }
                }
            }

            if (!applied)
            {
                BossHealth bh = ResolveBossHealth();
                if (bh != null && !bh.IsDead)
                {
                    var cc = bh.GetComponent<CharacterController>();
                    Vector3 closest = cc != null ? cc.ClosestPoint(point.position) : bh.transform.position;
                    float dist = Vector3.Distance(point.position, closest);
                    float maxDist = hitRadius + fallbackHitPadding;
                    if (dist <= maxDist)
                    {
                        int hpBefore = bh.CurrentHealth;
                        RunStatisticsTracker.Instance?.RegisterMeleeHitLanded();
                        bh.TakeDamage(damage);
                        applied = true;
                        if (debugCombatLogs)
                            Debug.Log($"[Combat] ✓ BOSS HIT (mesafe): damage={damage} dist={dist:F2}/{maxDist:F2} hp {hpBefore} → {bh.CurrentHealth}");
                    }
                    else if (debugCombatLogs)
                        Debug.Log($"[Combat] ✗ MISS mesafe (boss): dist={dist:F2} > {maxDist:F2}");
                }

                if (!applied)
                {
                    EnemyHealth eh = ResolveEnemyHealth();
                    if (eh != null && !eh.IsDead)
                    {
                        var cc = eh.GetComponent<CharacterController>();
                        Vector3 closest = cc != null ? cc.ClosestPoint(point.position) : eh.transform.position;
                        float dist = Vector3.Distance(point.position, closest);
                        float maxDist = hitRadius + fallbackHitPadding;
                        if (dist <= maxDist)
                        {
                            int hpBefore = eh.CurrentHealth;
                            RunStatisticsTracker.Instance?.RegisterMeleeHitLanded();
                            eh.TakeDamage(damage);
                            applied = true;
                            if (debugCombatLogs)
                                Debug.Log($"[Combat] ✓ ENEMY HIT (mesafe): damage={damage} dist={dist:F2}/{maxDist:F2} hp {hpBefore} → {eh.CurrentHealth}");
                        }
                    }
                }

                if (!applied && debugCombatLogs)
                    Debug.Log("[Combat] ✗ MISS: collider yok / mesafe dışı — boss+enemy layer, weaponHitPoint, hitRadius, Enemy Reference");
            }
        }

        private BossHealth ResolveBossHealth()
        {
            if (bossReference != null)
            {
                BossHealth bh = bossReference.GetComponent<BossHealth>();
                if (bh == null) bh = bossReference.GetComponentInParent<BossHealth>();
                if (bh != null)
                {
                    _cachedBossHealth = bh;
                    return bh;
                }
            }

            if (_cachedBossHealth != null && _cachedBossHealth.gameObject.activeInHierarchy)
                return _cachedBossHealth;

            if (!string.IsNullOrEmpty(bossTag))
            {
                GameObject go = GameObject.FindGameObjectWithTag(bossTag);
                if (go != null)
                {
                    BossHealth bh = go.GetComponent<BossHealth>();
                    if (bh == null) bh = go.GetComponentInParent<BossHealth>();
                    if (bh != null)
                    {
                        _cachedBossHealth = bh;
                        return bh;
                    }
                }
            }

            if (autoFindBossHealth)
            {
                BossHealth bh = Object.FindObjectOfType<BossHealth>();
                if (bh != null)
                    _cachedBossHealth = bh;
                return bh;
            }

            return null;
        }

        private EnemyHealth ResolveEnemyHealth()
        {
            if (enemyReference != null)
            {
                EnemyHealth eh = enemyReference.GetComponent<EnemyHealth>();
                if (eh == null) eh = enemyReference.GetComponentInParent<EnemyHealth>();
                if (eh != null)
                {
                    _cachedEnemyHealth = eh;
                    return eh;
                }
            }

            if (_cachedEnemyHealth != null && _cachedEnemyHealth.gameObject.activeInHierarchy)
                return _cachedEnemyHealth;

            if (!string.IsNullOrEmpty(enemyTag))
            {
                GameObject go = GameObject.FindGameObjectWithTag(enemyTag);
                if (go != null)
                {
                    EnemyHealth eh = go.GetComponent<EnemyHealth>();
                    if (eh == null) eh = go.GetComponentInParent<EnemyHealth>();
                    if (eh != null)
                    {
                        _cachedEnemyHealth = eh;
                        return eh;
                    }
                }
            }

            if (autoFindEnemyHealth)
            {
                EnemyHealth eh = Object.FindObjectOfType<EnemyHealth>();
                if (eh != null)
                    _cachedEnemyHealth = eh;
                return eh;
            }

            return null;
        }
    }
}