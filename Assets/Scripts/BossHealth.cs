using System;
using UnityEngine;

/// <summary>
/// Basit boss can sistemi. Hasar aldığında hit animasyonu, can bitince death animasyonu tetikler.
/// </summary>
[RequireComponent(typeof(Animator))]
public class BossHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string deathTriggerName = "Death";
    [Header("Hit reaction kontrolü")]
    [Tooltip("Bu süreden kısa aralıkta tekrar hit animasyonu tetiklenmez (hasar yine uygulanır).")]
    [SerializeField] private float hitReactionCooldown = 0.2f;
    [Tooltip("Boss Attack state'lerindeyken Hit animasyonu tetiklenmesin (super armor hissi).")]
    [SerializeField] private bool suppressHitReactionWhileAttacking = true;
    [Tooltip("Boss hareket (koşu) animasyonundayken Hit reaksiyonu tetiklenmesin; sliding/hit-lock azaltır.")]
    [SerializeField] private bool suppressHitReactionWhileMoving = true;
    [SerializeField] private string moveSpeedParamName = "MoveSpeed";
    [SerializeField] private float movingThreshold = 0.1f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public float HitReactionCooldown => hitReactionCooldown;

    /// <summary>current, max — UI doğrudan buna abone olur.</summary>
    public event Action<int, int> HealthChanged;

    public event Action<int> DamageTaken;
    public event Action Died;

    private Animator _animator;
    private float _nextHitReactionTime;
    private bool _suppressHitReactionRuntime;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        CurrentHealth = maxHealth;
        IsDead = false;
        RaiseHealthChanged();
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
                if (!string.IsNullOrEmpty(deathTriggerName))
                    _animator.SetTrigger(deathTriggerName);
            }

            Died?.Invoke();
        }
        else
        {
            if (ShouldPlayHitReaction())
                _animator.SetTrigger(hitTriggerName);
        }

        RaiseHealthChanged();
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private bool ShouldPlayHitReaction()
    {
        if (_suppressHitReactionRuntime)
            return false;

        if (_animator == null || string.IsNullOrEmpty(hitTriggerName))
            return false;

        if (Time.time < _nextHitReactionTime)
            return false;

        if (suppressHitReactionWhileAttacking)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            bool inAttack =
                state.IsName("Attack1") ||
                state.IsName("Attack2") ||
                state.IsName("Attack3") ||
                state.IsName("Attack4") ||
                state.IsName("Attack5");
            if (inAttack)
                return false;
        }

        if (suppressHitReactionWhileMoving && !string.IsNullOrEmpty(moveSpeedParamName))
        {
            float moveSpeed = _animator.GetFloat(moveSpeedParamName);
            if (moveSpeed > movingThreshold)
                return false;
        }

        _nextHitReactionTime = Time.time + Mathf.Max(0f, hitReactionCooldown);
        return true;
    }

    /// <summary>
    /// AI kovalamadayken hit reaction'ı geçici bastırmak için.
    /// </summary>
    public void SetHitReactionSuppressedRuntime(bool suppressed)
    {
        _suppressHitReactionRuntime = suppressed;
    }

    /// <summary>Yeni run / LLM zorluk sonrası max can (clamp içeride).</summary>
    public void SetMaxHealthForRun(int newMax, bool refillToFull, bool clearDeadState = true)
    {
        maxHealth = Mathf.Clamp(newMax, 1, 9999);
        if (clearDeadState)
            IsDead = false;
        CurrentHealth = refillToFull ? maxHealth : Mathf.Clamp(CurrentHealth, 0, maxHealth);
        RaiseHealthChanged();
    }

    public void SetHitReactionCooldownForRun(float value)
    {
        hitReactionCooldown = Mathf.Clamp(value, 0f, 2.5f);
    }
}

