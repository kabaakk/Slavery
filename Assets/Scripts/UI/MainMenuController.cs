using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menüde Play / Settings / Exit akışını ve temel ayarları yönetir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Sahne Geçişi")]
    [SerializeField] private string playSceneName = "CorridorScene";

    [Header("Settings UI (opsiyonel)")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private const string KeyMasterVolume = "settings_master_volume";
    private const string KeyQualityIndex = "settings_quality_index";
    private const string KeyFullscreen = "settings_fullscreen";

    private bool _isApplyingFromLoad;

    private void Awake()
    {
        Time.timeScale = 1f;
        ShowMainPanel();
        BuildQualityDropdownIfNeeded();
        WireUiEvents();
        LoadAndApplySettings();
    }

    public void OnClickPlay()
    {
        if (string.IsNullOrEmpty(playSceneName))
        {
            Debug.LogWarning("[MainMenuController] playSceneName boş.", this);
            return;
        }

        SceneManager.LoadScene(playSceneName);
    }

    public void OnClickOpenSettings() => ShowSettingsPanel();

    public void OnClickCloseSettings() => ShowMainPanel();

    public void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowMainPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ShowSettingsPanel()
    {
        Debug.Log("[MainMenuController] Ayarlara tıklandı (şimdilik placeholder).", this);
    }

    private void WireUiEvents()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void BuildQualityDropdownIfNeeded()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();
        var options = new List<string>(QualitySettings.names);
        qualityDropdown.AddOptions(options);
    }

    private void LoadAndApplySettings()
    {
        _isApplyingFromLoad = true;

        float volume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
        int quality = PlayerPrefs.GetInt(KeyQualityIndex, QualitySettings.GetQualityLevel());
        int fullscreenAsInt = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0);
        bool fullscreen = fullscreenAsInt == 1;

        ApplyMasterVolume(volume);
        ApplyQuality(quality);
        ApplyFullscreen(fullscreen);

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(volume);

        if (qualityDropdown != null)
        {
            int clamped = Mathf.Clamp(quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            qualityDropdown.SetValueWithoutNotify(clamped);
        }

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        _isApplyingFromLoad = false;
    }

    private void OnMasterVolumeChanged(float value)
    {
        ApplyMasterVolume(value);
        if (_isApplyingFromLoad) return;
        PlayerPrefs.SetFloat(KeyMasterVolume, value);
        PlayerPrefs.Save();
    }

    private void OnQualityChanged(int value)
    {
        ApplyQuality(value);
        if (_isApplyingFromLoad) return;
        PlayerPrefs.SetInt(KeyQualityIndex, value);
        PlayerPrefs.Save();
    }

    private void OnFullscreenChanged(bool value)
    {
        ApplyFullscreen(value);
        if (_isApplyingFromLoad) return;
        PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void ApplyMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    private static void ApplyQuality(int value)
    {
        int clamped = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        QualitySettings.SetQualityLevel(clamped, true);
    }

    private static void ApplyFullscreen(bool value)
    {
        Screen.fullScreen = value;
    }
}
