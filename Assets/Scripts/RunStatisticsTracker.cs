using System;
using System.Collections;
using UnityEngine;
using Slavery.LLM;

/// <summary>
/// Dövüş süresi, vuruş sayıları, dash ve bitiş canlarını toplar; oyun sonu panelini açar.
/// Sahneye bir kez ekleyin; Player/Boss ve panel referanslarını atayın.
/// </summary>
public class RunStatisticsTracker : MonoBehaviour
{
    public static RunStatisticsTracker Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private BossHealth bossHealth;
    [Tooltip("Koridor düşmanı — sahnede boss yokken zafer koşulu için (boss atanmışsa boş bırakılabilir).")]
    [SerializeField] private EnemyHealth challengeEnemyHealth;
    [Tooltip("Koridor istatistikleri (öldürülen düşman sayısı). Dalga bitince panel açılmaz; sadece ölümde.")]
    [SerializeField] private CorridorWaveController corridorWaveController;
    [SerializeField] private ChallengeStatisticsPanel statisticsPanel;
    [SerializeField] private DifficultyApplier difficultyApplier;

    [Header("Oyun sonu paneli")]
    [Tooltip("Oyuncu öldüğünde paneli kaç saniye geciktir (ölüm animasyonu görülsün).")]
    [SerializeField] private float playerDefeatPanelDelaySeconds = 1.5f;
    [Tooltip("Boss/koridor zaferinde panel gecikmesi.")]
    [SerializeField] private float victoryPanelDelaySeconds = 1.0f;

    private const float FightTimeUnset = -1f;
    private float _fightStartTime = FightTimeUnset;
    private int _hitsDealtToBoss;
    private int _hitsTakenByPlayer;
    private int _dashCount;
    private bool _fightEnded;

    public bool FightEnded => _fightEnded;
    public int HitsDealtToBoss => _hitsDealtToBoss;
    public int HitsTakenByPlayer => _hitsTakenByPlayer;
    public int DashCount => _dashCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RunStatisticsTracker] Birden fazla örnek var; fazlası devre dışı.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        BeginFight();
    }

    private void OnEnable()
    {
        AutoResolveOptionalReferences();

        if (playerHealth != null)
        {
            playerHealth.DamageTaken += OnPlayerDamageTaken;
            playerHealth.Died += OnPlayerDied;
        }

        if (bossHealth != null)
            bossHealth.Died += OnBossDied;

        if (challengeEnemyHealth != null)
            challengeEnemyHealth.Died += OnChallengeEnemyDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.DamageTaken -= OnPlayerDamageTaken;
            playerHealth.Died -= OnPlayerDied;
        }

        if (bossHealth != null)
            bossHealth.Died -= OnBossDied;

        if (challengeEnemyHealth != null)
            challengeEnemyHealth.Died -= OnChallengeEnemyDied;
    }

    /// <summary>Yeni run veya sahne yeniden başlatıldığında çağrılır.</summary>
    public void BeginFight()
    {
        StopAllCoroutines();
        _fightEnded = false;
        _fightStartTime = Time.time;
        _hitsDealtToBoss = 0;
        _hitsTakenByPlayer = 0;
        _dashCount = 0;
        if (difficultyApplier != null)
            difficultyApplier.BeginFightTimer();
        if (statisticsPanel != null)
            statisticsPanel.Hide();
    }

    public void RegisterDash()
    {
        if (_fightEnded) return;
        _dashCount++;
    }

    /// <summary>Boss veya düşmana başarılı yakın dövüş vuruşu.</summary>
    public void RegisterMeleeHitLanded()
    {
        if (_fightEnded) return;
        _hitsDealtToBoss++;
    }

    /// <summary>Eski isim; <see cref="RegisterMeleeHitLanded"/> ile aynı.</summary>
    public void RegisterHitDealtToBoss() => RegisterMeleeHitLanded();

    public float GetFightDurationSeconds()
    {
        // Start() içinde BeginFight çağrılınca Time.time sıfır olabilir; <=0 kontrolü süreyi hep 0 yapıyordu.
        if (_fightStartTime < 0f) return 0f;
        return Time.time - _fightStartTime;
    }

    /// <summary>LLM bağlamına istatistik alanlarını yazar (DifficultyApplier ile birlikte kullanın).</summary>
    public void ApplyStatsToContext(ref RunEndContext ctx)
    {
        ctx.HitsTakenByPlayer = _hitsTakenByPlayer;
        ctx.HitsDealtToBoss = _hitsDealtToBoss;
        ctx.DashCount = _dashCount;
        ctx.PlayerHpRemainingEnd = playerHealth != null ? playerHealth.CurrentHealth : 0;
        ctx.PlayerHpMaxEnd = playerHealth != null ? playerHealth.MaxHealth : 0;
        ctx.BossHpRemainingEnd = bossHealth != null ? bossHealth.CurrentHealth : 0;
        ctx.BossHpMaxEnd = bossHealth != null ? bossHealth.MaxHealth : 0;
    }

    public ChallengeRunSnapshot BuildSnapshot(bool playerWon)
    {
        return new ChallengeRunSnapshot
        {
            PlayerWon = playerWon,
            FightDurationSeconds = GetFightDurationSeconds(),
            HitsDealtToBoss = _hitsDealtToBoss,
            EnemiesDefeated = corridorWaveController != null ? corridorWaveController.KillCount : 0,
            HitsTakenByPlayer = _hitsTakenByPlayer,
            DashCount = _dashCount,
            PlayerHpRemaining = playerHealth != null ? playerHealth.CurrentHealth : 0,
            PlayerHpMax = playerHealth != null ? playerHealth.MaxHealth : 0,
            BossHpRemaining = bossHealth != null ? bossHealth.CurrentHealth : 0,
            BossHpMax = bossHealth != null ? bossHealth.MaxHealth : 0
        };
    }

    private void OnPlayerDamageTaken(int _)
    {
        if (_fightEnded) return;
        _hitsTakenByPlayer++;
    }

    private void OnPlayerDied()
    {
        EndFight(playerWon: false);
    }

    private void OnBossDied()
    {
        EndFight(playerWon: true);
    }

    private void OnChallengeEnemyDied()
    {
        // Tek düşmanlı eski akış: dalga yoksa ve boss yoksa zafer paneli.
        if (corridorWaveController != null || bossHealth != null)
            return;
        EndFight(playerWon: true);
    }

    private void AutoResolveOptionalReferences()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (bossHealth == null)
            bossHealth = FindObjectOfType<BossHealth>();

        if (corridorWaveController == null)
            corridorWaveController = FindObjectOfType<CorridorWaveController>();
    }

    private void EndFight(bool playerWon)
    {
        if (_fightEnded) return;
        _fightEnded = true;

        // Koridor: 5 düşman bitince panel yok — kapıdan boss sahnesine geçilir.
        bool isCorridorScene = bossHealth == null && corridorWaveController != null;
        if (isCorridorScene && playerWon)
            return;

        var snapshot = BuildSnapshot(playerWon);
        if (statisticsPanel == null)
            return;

        float delay = playerWon ? victoryPanelDelaySeconds : playerDefeatPanelDelaySeconds;
        if (delay > 0f)
            StartCoroutine(ShowStatisticsPanelDelayed(snapshot, delay));
        else
            statisticsPanel.Show(snapshot);
    }

    private IEnumerator ShowStatisticsPanelDelayed(ChallengeRunSnapshot snapshot, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (statisticsPanel != null)
            statisticsPanel.Show(snapshot);
    }
}
