using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sadece <b>koridor çıkış kapısına</b> ekleyin. Diğer dekoratif kapılarda bu script olmamalı.
/// Oyuncu dalga bitmeden geçemez; 5. düşman öldükten sonra tetikleyici sahne yükler.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CorridorExitDoor : MonoBehaviour
{
    [Header("Dalga (5 düşman)")]
    [Tooltip("Boşsa sahnede CorridorWaveController aranır.")]
    [SerializeField] private CorridorWaveController waveController;

    [Header("Geçiş")]
    [Tooltip("File → Build Settings → Scenes In Build listesinde olmalı.")]
    [SerializeField] private string sceneToLoad = "BossScene";

    [SerializeField] private string playerTag = "Player";

    [Tooltip("Dalga bitmeden tetikleyiciye girilirse hiçbir şey yapılmaz.")]
    [SerializeField] private bool blockUntilWaveComplete = true;

    private Collider _collider;
    private bool _loaded;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;

        if (waveController == null)
            waveController = FindObjectOfType<CorridorWaveController>();
    }

    private void OnTriggerEnter(Collider other) => TryTransition(other);

    private void OnTriggerStay(Collider other) => TryTransition(other);

    private void TryTransition(Collider other)
    {
        if (_loaded)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (blockUntilWaveComplete && waveController != null && !waveController.IsWaveComplete)
            return;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("[CorridorExitDoor] sceneToLoad boş.", this);
            return;
        }

        _loaded = true;
        SceneManager.LoadScene(sceneToLoad);
    }
}
