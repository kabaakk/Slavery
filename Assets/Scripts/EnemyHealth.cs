using System;
using UnityEngine;

/// <summary>
/// Koridor düşmanı için basit can. Animator'da Hit / Dead (bool) veya Death (trigger) ile eşleşir.
/// Animator mesh çocuğundaysa opsiyonel referans ver veya otomatik olarak Runtime Controller atanmış Animator bulunur.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Boşsa: önce controller'ı olan Animator (genelde mesh altı). Parent'taki boş Animator'u yok sayar.")]
    [SerializeField] private Animator animatorOverride;

    [SerializeField] private int maxHealth = 40;
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string deathTriggerName = "Death";
    [Tooltip("true: animator.SetBool(deathBoolName, true) — Any State->Die bool koşulu için")]
    [SerializeField] private bool useDeathBool = true;
    [SerializeField] private string deathBoolName = "Dead";

    [Header("Hit tepkisi (BossHealth ile benzer)")]
    [Tooltip("Kısa sürede ardışık Hit trigger spam’ini azaltır; hasar yine uygulanır.")]
    [SerializeField] private float hitReactionCooldown = 0.2f;
    [Tooltip("Attack state’teyken Hit animasyonu tetiklenmesin (süper zırh).")]
    [SerializeField] private bool suppressHitReactionWhileAttacking = true;
    [SerializeField] private string attackStateName = "Attack";

    [Header("Debug (can barı yok — sadece konsol)")]
    [SerializeField] private bool logHpToConsole = true;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    public event Action<int, int> HealthChanged;
    public event Action<int> DamageTaken;
    public event Action Died;

    private Animator _animator;
    private float _nextHitReactionTime;

    private void Awake()
    {
        _animator = ResolveRuntimeAnimator(animatorOverride);
        CurrentHealth = maxHealth;
        IsDead = false;
        if (useDeathBool && _animator != null)
            _animator.SetBool(deathBoolName, false);
        RaiseHealthChanged();
    }

    private Animator ResolveRuntimeAnimator(Animator explicitRef)
    {
        if (explicitRef != null)
            return explicitRef;

        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a != null && a.runtimeAnimatorController != null)
                return a;
        }

        return GetComponent<Animator>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        CurrentHealth -= amount;
        DamageTaken?.Invoke(amount);
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            IsDead = true;
            if (_animator != null)
            {
                if (!string.IsNullOrEmpty(hitTriggerName))
                    _animator.ResetTrigger(hitTriggerName);
                if (useDeathBool)
                {
                    if (!string.IsNullOrEmpty(deathBoolName))
                        _animator.SetBool(deathBoolName, true);
                }
                else if (!string.IsNullOrEmpty(deathTriggerName))
                {
                    _animator.SetTrigger(deathTriggerName);
                }
            }

            Died?.Invoke();
        }
        else
        {
            if (ShouldPlayHitReaction())
                _animator.SetTrigger(hitTriggerName);
        }

        RaiseHealthChanged();

        if (logHpToConsole)
            Debug.Log($"[EnemyHealth] {name} hasar={amount} can={CurrentHealth}/{maxHealth} öldü={IsDead}", this);
    }

    private bool ShouldPlayHitReaction()
    {
        if (_animator == null || string.IsNullOrEmpty(hitTriggerName))
            return false;

        if (Time.time < _nextHitReactionTime)
            return false;

        if (suppressHitReactionWhileAttacking && !string.IsNullOrEmpty(attackStateName))
        {
            AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(attackStateName))
                return false;
        }

        _nextHitReactionTime = Time.time + Mathf.Max(0f, hitReactionCooldown);
        return true;
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}
