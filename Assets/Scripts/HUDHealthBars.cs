using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Can barı = sadece PlayerHealth / BossHealth sayıları. TakeDamage → HealthChanged → fill güncellenir.
/// </summary>
public class HUDHealthBars : MonoBehaviour
{
    [Header("Kaynak (sahnedeki Player / Boss root)")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private BossHealth bossHealth;

    [Tooltip("Inspector boşsa ilk PlayerHealth otomatik bulunur.")]
    [SerializeField] private bool autoResolvePlayerHealth = true;

    [Header("UI — Image Type = Filled")]
    [SerializeField] private Image playerHealthFill;
    [SerializeField] private Image bossHealthFill;

    [Header("Opsiyonel")]
    [SerializeField] private bool hideBossBarWhenDead = true;
    [SerializeField] private GameObject bossBarRoot;

    [Header("Düşük can — bar okunabilirliği")]
    [Tooltip("Can > 0 iken fill bu değerin altına inmez. Az can doğrusal oranda çok ince kaldığı için bar ‘bitmiş’ gibi görünmesin diye. 0 = tam doğrusal (current/max).")]
    [SerializeField, Range(0f, 0.3f)] private float minVisibleFillWhenAlive = 0.08f;

    private void Awake()
    {
        TryResolvePlayerHealth();
    }

    private void Start()
    {
        // Tüm Awake’ler bittikten sonra çizim (Canvas bazen Player’dan önce enable olabiliyor).
        TryResolvePlayerHealth();
        if (playerHealth != null)
            OnPlayerHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        if (bossHealth != null)
            OnBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
    }

    private void TryResolvePlayerHealth()
    {
        if (playerHealth != null || !autoResolvePlayerHealth)
            return;
        playerHealth = FindObjectOfType<PlayerHealth>();
    }

    private void OnEnable()
    {
        TryResolvePlayerHealth();
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += OnPlayerHealthChanged;
            OnPlayerHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        if (bossHealth != null)
        {
            bossHealth.HealthChanged += OnBossHealthChanged;
            OnBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= OnPlayerHealthChanged;
        if (bossHealth != null)
            bossHealth.HealthChanged -= OnBossHealthChanged;
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        if (playerHealthFill == null || max <= 0) return;
        playerHealthFill.fillAmount = ComputeFillAmount(current, max);
    }

    private void OnBossHealthChanged(int current, int max)
    {
        if (bossHealthFill != null && max > 0)
            bossHealthFill.fillAmount = ComputeFillAmount(current, max);

        if (hideBossBarWhenDead && bossBarRoot != null && bossHealth != null)
            bossBarRoot.SetActive(!bossHealth.IsDead);
    }

    private float ComputeFillAmount(int current, int max)
    {
        float linear = Mathf.Clamp01((float)current / max);
        if (minVisibleFillWhenAlive <= 0f || current <= 0)
            return linear;
        return Mathf.Max(linear, minVisibleFillWhenAlive);
    }
}
