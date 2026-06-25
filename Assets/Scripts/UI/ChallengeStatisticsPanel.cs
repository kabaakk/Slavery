using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Slavery.LLM;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyun sonu istatistik paneli. Başlık metni title TextMeshPro üzerinde Inspector’da ne yazdıysan odur (kod ezmez).
/// </summary>
public class ChallengeStatisticsPanel : MonoBehaviour
{
    private enum DisplayMode
    {
        BossDetailed,
        CorridorMinimal
    }

    private enum ContinueAction
    {
        BeginFightOnly,
        ReloadActiveScene,
        LoadSceneByName
    }

    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private LlmDifficultyService llmDifficultyService;
    [Header("Sunum")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.BossDetailed;
    [Header("Buton davranışı")]
    [SerializeField] private ContinueAction continueAction = ContinueAction.BeginFightOnly;
    [Tooltip("ContinueAction = LoadSceneByName iken yüklenecek sahne (örn: CorridorScene).")]
    [SerializeField] private string continueSceneName = "CorridorScene";

    private ChallengeRunSnapshot _lastSnapshot;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Show(ChallengeRunSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        if (root != null)
            root.SetActive(true);

        if (bodyText != null)
            bodyText.text = FormatBody(snapshot, displayMode);
    }

    private static string FormatBody(ChallengeRunSnapshot s, DisplayMode mode)
    {
        if (mode == DisplayMode.CorridorMinimal)
            return FormatMinimalBody(s);

        string duration = FormatDuration(s.FightDurationSeconds);
        string outcome = s.PlayerWon ? "Sonuç: Zafer." : "Sonuç: Yenilgi.";
        string hitsDealt = $"İsabet eden vuruş: {s.HitsDealtToBoss}";
        string hitsTaken = $"Aldığın vuruş: {s.HitsTakenByPlayer}";
        string dash = s.DashCount > 0
            ? $"Dash: kullanıldı ({s.DashCount} kez)."
            : "Dash: kullanılmadı.";
        string playerHp = $"Oyuncu canı: {s.PlayerHpRemaining} / {s.PlayerHpMax}";
        string bossHp = $"Boss canı: {s.BossHpRemaining} / {s.BossHpMax}";

        return
            $"{outcome}\n\n" +
            $"Süre: {duration}\n" +
            $"{hitsDealt}\n" +
            $"{hitsTaken}\n" +
            $"{dash}\n" +
            $"{playerHp}\n" +
            $"{bossHp}";
    }

    private static string FormatMinimalBody(ChallengeRunSnapshot s)
    {
        string duration = FormatDuration(s.FightDurationSeconds);
        string outcome = s.PlayerWon ? "Sonuç: Koridor temizlendi." : "Sonuç: Yenildin.";
        string enemiesDefeated = $"Öldürdüğün düşman: {s.EnemiesDefeated}";
        string playerHp = $"Kalan can: {s.PlayerHpRemaining} / {s.PlayerHpMax}";

        return
            $"{outcome}\n\n" +
            $"Süre: {duration}\n" +
            $"{enemiesDefeated}\n" +
            $"{playerHp}";
    }

    private static string FormatDuration(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.FloorToInt(seconds);
        int m = total / 60;
        int sec = total % 60;
        return $"{m:00}:{sec:00}";
    }

    private void OnContinueClicked()
    {
        if (continueAction == ContinueAction.LoadSceneByName)
        {
            if (string.IsNullOrEmpty(continueSceneName))
            {
                Debug.LogWarning("[ChallengeStatisticsPanel] continueSceneName boş.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(continueSceneName);
            return;
        }

        if (continueAction == ContinueAction.ReloadActiveScene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (llmDifficultyService != null)
        {
            llmDifficultyService.RequestAndApplyNextDifficulty(
                _lastSnapshot.PlayerWon,
                onComplete: () => RunStatisticsTracker.Instance?.BeginFight());
        }
        else
        {
            RunStatisticsTracker.Instance?.BeginFight();
        }

        Hide();
    }
}
