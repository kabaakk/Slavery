using System;
using UnityEngine;

/// <summary>
/// Basit player can sistemi. Hasar aldığında damage animasyonunu, can bitince death animasyonunu tetikler.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 115;
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private float hitStunDuration = 0.16f;

    [Header("Debug")]
    [SerializeField] private bool logDamageToConsole = true;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsInHitStun => !IsDead && Time.time < _hitStunUntil;

    /// <summary>current, max — UI doğrudan buna abone olur.</summary>
    public event Action<int, int> HealthChanged;

    /// <summary>Her başarılı TakeDamage (miktar).</summary>
    public event Action<int> DamageTaken;

    /// <summary>Can 0 olduğunda bir kez.</summary>
    public event Action Died;

    private Animator _animator;
    private float _hitStunUntil;

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
            _hitStunUntil = 0f;

            if (_animator != null && !string.IsNullOrEmpty(deathTriggerName))
                _animator.SetTrigger(deathTriggerName);

            Died?.Invoke();
        }
        else
        {
            if (_animator != null && !string.IsNullOrEmpty(hitTriggerName))
                _animator.SetTrigger(hitTriggerName);
            _hitStunUntil = Time.time + Mathf.Max(0f, hitStunDuration);
        }

        RaiseHealthChanged();

        if (logDamageToConsole)
            Debug.Log($"[PlayerHealth] hasar={amount} can={CurrentHealth}/{maxHealth} öldü={IsDead}", this);
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>Yeni run / LLM zorluk sonrası max can.</summary>
    public void SetMaxHealthForRun(int newMax, bool refillToFull, bool clearDeadState = true)
    {
        maxHealth = Mathf.Clamp(newMax, 1, 9999);
        if (clearDeadState)
            IsDead = false;
        _hitStunUntil = 0f;
        CurrentHealth = refillToFull ? maxHealth : Mathf.Clamp(CurrentHealth, 0, maxHealth);
        RaiseHealthChanged();
    }
}

