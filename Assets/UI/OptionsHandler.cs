using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class OptionsHandler : MonoBehaviour
{
    public WindowHandler windowHandler;
    //public AppHandler appHandler;

    public List<UISwitchBinder> binders;

    // Language
    [Header("Language Settings")]
    public UIScreen popupLanguages;
    public TMP_Text selectedLanguageText;

    // Volume
    [Header("Volume Settings")]
    [SerializeField] private Slider volumeSlider;

    // Vibration
    [Header("Vibration Settings")]
    [SerializeField] private SingleSelectionGroup vibrationStrengthSelection;

    // Penalty-Kosten werden seit Customization (Seite 1) dort editiert.
    // Enable-Toggles (UiSwitchBinder) bleiben hier unangetastet.




    // =========================
    // UNITY LIFECYCLE
    // =========================

    void Start()
    {
        InitializeUI();
    }



private void UpdateLanguageText(string locale)
{
    if (selectedLanguageText == null) return;

    // Fallback: If no locale string is passed, load the one saved in your AppSettings
    if (string.IsNullOrEmpty(locale))
    {
        locale = AppSettingsManager.Instance.Settings.Language.lastLocaleCode;
    }

    // Modern switch statement to display the correct name for each locale code
    switch (locale.ToLower())
    {
        case "de":
            selectedLanguageText.text = "Deutsch";
            break;
        case "en":
            selectedLanguageText.text = "English";
            break;
        case "fr":
            selectedLanguageText.text = "Français";
            break;
        case "es":
            selectedLanguageText.text = "Español";
            break;
        case "ru":
            selectedLanguageText.text = "Русский";
            break;
        case "pl":
            selectedLanguageText.text = "Polski";
            break;
        case "it":
            selectedLanguageText.text = "Italiano";
            break;
        case "tr":
            selectedLanguageText.text = "Türkçe";
            break;
        default:
            // Fallback in case a locale is requested that is not yet configured
            selectedLanguageText.text = "English"; 
            Debug.LogWarning($"[LanguageUI] Unhandled locale code: {locale}. Defaulting to English.");
            break;
    }
}



    // =========================
    // INITIALIZATION
    // =========================

    private void InitializeUI()
    {
        UpdateLanguageText(AppSettingsManager.Instance.Settings.Language.lastLocaleCode);

        foreach (var binder in binders)
        {
            binder.Initialize(this);
        }

        var settings = AppSettingsManager.Instance.Settings;

        if (volumeSlider != null && settings?.Sound != null)
        {
            volumeSlider.SetValueWithoutNotify(settings.Sound.Volume);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (vibrationStrengthSelection != null)
        {
            vibrationStrengthSelection.Init((int)settings.Vibration.Strength);
        }
    }


    // =========================
    // NAVIGATION
    // =========================

    public void GoBack()
    {
        windowHandler.GoBack();
    }

    // =========================
    // LANGUAGE HANDLING
    // =========================

    public void ShowPopupLanguages()
    {
        if (popupLanguages != null)
        {
            windowHandler.ShowPopup(popupLanguages);
        }
    }

    public void ReturnFromPopupLanguages()
    {
        windowHandler.HidePopup();
    }

    public void SetLanguage(string localeCode)
    {
        AppSettingsManager.Instance.SetLanguage(localeCode);
        UpdateLanguageText(localeCode);
        ReturnFromPopupLanguages();
    }




    // =========================
    // THEME HANDLING
    // =========================

    public void SetGreen()
    {
        SetColorScheme(ThemeColorScheme.Green);
    }

    public void SetRed()
    {
        SetColorScheme(ThemeColorScheme.Red);
    }

    public void SetBlue()
    {
        SetColorScheme(ThemeColorScheme.Blue);
    }

    public void SetPurple()
    {
        SetColorScheme(ThemeColorScheme.Purple);
    }

    public void SetCyan()
    {
        SetColorScheme(ThemeColorScheme.Cyan);
    }

    public void SetGold()
    {
        SetColorScheme(ThemeColorScheme.Gold);
    }

    private void SetColorScheme(ThemeColorScheme scheme)
    {
        // zentrale Stelle für Theme-Wechsel (Single Point of Truth)
        AppSettingsManager.Instance.SetTheme(scheme);
    }



    // =========================
    // AUDIO SETTINGS
    // =========================

    public void SetSound(bool soundEnabled)
    {
        // Persistiert Sound-Setting im AppHandler
        AppSettingsManager.Instance.SetSoundEnabled(soundEnabled);
    }

    public void SetCustomSound(bool customSoundEnabled)
    {
        // Speichert Einstellung für Custom Sounds
        AppSettingsManager.Instance.SetCustomSoundsEnabled(customSoundEnabled);
    }


    // =========================
    // VIBRATION SETTINGS
    // =========================
    public void SetVibration(bool vibrationEnabled)
    {
        AppSettingsManager.Instance.SetVibrationEnabled(vibrationEnabled);
    }

    public void SetVibrationStrength(VibrationStrength strength)
    {
        AppSettingsManager.Instance.SetVibrationStrength(strength);
    }
    public void SetVibrationWeak()
    {
        SetVibrationStrength(VibrationStrength.Weak);
    }

    public void SetVibrationMedium()
    {
        SetVibrationStrength(VibrationStrength.Medium);
    }

    public void SetVibrationStrong()
    {
        SetVibrationStrength(VibrationStrength.Strong);
    }


    // =========================
    // UI LOGIC
    // =========================

    private void OnVolumeChanged(float value)
    {
        AppSettingsManager.Instance.SetVolume(value);
    }
}
