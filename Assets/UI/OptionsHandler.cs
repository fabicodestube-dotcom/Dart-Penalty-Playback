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

    // Penalties
    [Header("Penalty Settings")]
    public TMP_InputField penaltyWall;
    public TMP_InputField penaltyCeiling;
    public TMP_InputField penaltyAllMiss;
    public TMP_InputField penaltyTripleOnes;
    public TMP_InputField penaltySchnapszahl;
    public TMP_InputField penaltyLostGame;




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

        if (penaltyWall != null)
        {
            SetupInputField(penaltyWall, penaltyWall.GetComponent<Outline>(), PenaltyType.Wall);
            penaltyWall.text = settings.Penalties.GetCost(PenaltyType.Wall).ToString();
        }
            

        if (penaltyCeiling != null)
        {
            SetupInputField(penaltyCeiling, penaltyCeiling.GetComponent<Outline>(), PenaltyType.Ceiling);
            penaltyCeiling.text = settings.Penalties.GetCost(PenaltyType.Ceiling).ToString();
        }

        if (penaltyAllMiss != null)
        {
            SetupInputField(penaltyAllMiss, penaltyAllMiss.GetComponent<Outline>(), PenaltyType.AllMiss);
            penaltyAllMiss.text = settings.Penalties.GetCost(PenaltyType.AllMiss).ToString();
        }

        if (penaltyTripleOnes != null)
        {
            SetupInputField(penaltyTripleOnes, penaltyTripleOnes.GetComponent<Outline>(), PenaltyType.ThreeOnes);
            penaltyTripleOnes.text = settings.Penalties.GetCost(PenaltyType.ThreeOnes).ToString();
        }

        if (penaltySchnapszahl != null)
        {
            SetupInputField(penaltySchnapszahl, penaltySchnapszahl.GetComponent<Outline>(), PenaltyType.Schnapszahl);
            penaltySchnapszahl.text = settings.Penalties.GetCost(PenaltyType.Schnapszahl).ToString();
        }

        if (penaltyLostGame != null)
        {
            SetupInputField(penaltyLostGame, penaltyLostGame.GetComponent<Outline>(), PenaltyType.LostGame);
            penaltyLostGame.text = settings.Penalties.GetCost(PenaltyType.LostGame).ToString();
        }
    }

    private void SetupInputField(
        TMP_InputField input,
        Outline outline,
        PenaltyType penaltyType)
    {
        input.onSelect.AddListener(_ =>
        {
            bool isValid = TryValidate(input.text, out decimal _);

            outline.effectColor = isValid
                ? ThemeManager.Instance.GetColor(ThemeColorRole.Accent1)
                : ThemeManager.Instance.GetColor(ThemeColorRole.Error);
        });

        input.onDeselect.AddListener(_ =>
        {
            bool isValid = TryValidate(input.text, out decimal _);

            outline.effectColor = isValid
                ? ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive)
                : ThemeManager.Instance.GetColor(ThemeColorRole.Error);
        });

        input.onValueChanged.AddListener(val =>
        {
            bool isValid = TryValidate(val, out decimal parsedValue);

            if (isValid)
            {
                AppSettingsManager.Instance.SetPenaltyCost(
                    penaltyType,
                    (int)parsedValue);
            }

            // Farbe abhängig von Fokus + Validität
            if (isValid)
            {
                outline.effectColor = input.isFocused
                    ? ThemeManager.Instance.GetColor(ThemeColorRole.Accent1)
                    : ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive);
            }
            else
            {
                outline.effectColor =
                    ThemeManager.Instance.GetColor(ThemeColorRole.Error);
            }
        });
    }

    private bool TryValidate(string input, out decimal value)
    {

        if (string.IsNullOrWhiteSpace(input))
        {
            value = 0;
            return false;
        }

        bool success = decimal.TryParse(
            input,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);

        if (!success)
        {
            return false;
        }

        return true;
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
    // Penalty SETTINGS
    // =========================
    public void SetPenaltyEnabled(PenaltyType type, bool enabled)
    {
        AppSettingsManager.Instance.SetPenaltyEnabled(type, enabled);
    }

    public void SetPenaltyCost(PenaltyType type, int cost)
    {
        AppSettingsManager.Instance.SetPenaltyCost(type, cost);
    }


    // =========================
    // UI LOGIC
    // =========================

    private void OnVolumeChanged(float value)
    {
        AppSettingsManager.Instance.SetVolume(value);
    }
}
