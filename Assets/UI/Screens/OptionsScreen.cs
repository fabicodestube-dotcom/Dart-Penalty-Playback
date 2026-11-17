using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class OptionsScreen : MonoBehaviour
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

    // Bot animation
    [Header("Bot Animation Settings")]
    [SerializeField] private RectTransform optionsContent;
    [SerializeField] private GameObject botAnimationSectionTemplate;
    [SerializeField] private GameObject botAnimationSwitchRowTemplate;
    [SerializeField] private GameObject botAnimationSliderRowTemplate;

    private Slider botAnimationSpeedSlider;
    private bool botAnimationSectionBuilt;



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
            Debug.LogWarning("[OptionsScreen] No locale provided to UpdateLanguageText. Falling back to saved locale.");
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

        BuildBotAnimationSection();

        foreach (var binder in binders)
        {
            if (binder != null)
                binder.Initialize(this);
        }

        // Hardening: initialize all UISwitchBinder in Children, even if
        // They are not on the binders list.
        foreach (var binder in GetComponentsInChildren<UISwitchBinder>(true))
        {
            if (binder != null && binder.enabled && !binders.Contains(binder))
            {
                binders.Add(binder);
                binder.Initialize(this);
            }
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
        //ReturnFromPopupLanguages();
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

    public void SetOrange()
    {
        SetColorScheme(ThemeColorScheme.Orange);
    }

    public void SetPink()
    {
        SetColorScheme(ThemeColorScheme.Pink);
    }

    public void SetTeal()
    {
        SetColorScheme(ThemeColorScheme.Teal);
    }

    public void SetIndigo()
    {
        SetColorScheme(ThemeColorScheme.Indigo);
    }

    public void SetBlueGrey()
    {
        SetColorScheme(ThemeColorScheme.BlueGrey);
    }

    public void SetDeepOrange()
    {
        SetColorScheme(ThemeColorScheme.DeepOrange);
    }

    public void SetThemeMode(bool dark)
    {
        AppSettingsManager.Instance.SetThemeMode(dark ? ThemeMode.Dark : ThemeMode.Light);
    }

    private void SetColorScheme(ThemeColorScheme scheme)
    {
        // Central location for the theme change (Single Point of Truth)
        AppSettingsManager.Instance.SetTheme(scheme);
    }



    // =========================
    // AUDIO SETTINGS
    // =========================

    public void SetSound(bool soundEnabled)
    {
        // Persists the sound setting in AppHandler
        AppSettingsManager.Instance.SetSoundEnabled(soundEnabled);
    }

    public void SetCustomSound(bool customSoundEnabled)
    {
        // Saves settings for custom sounds
        AppSettingsManager.Instance.SetCustomSoundsEnabled(customSoundEnabled);
    }

    /// <summary>
    /// Use built-in clips (default/custom set) as fallback if the
    /// User for an ID has not recorded anything yet. false → only own
    /// Recordings, otherwise mute.
    /// </summary>
    public void SetDefaultClipsFallback(bool fallbackEnabled)
    {
        AppSettingsManager.Instance.SetUseDefaultClipsAsFallback(fallbackEnabled);
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
    // BOT ANIMATION SETTINGS
    // =========================

    public void SetBotFullscreenOverlay(bool enabled)
    {
        AppSettingsManager.Instance.SetBotFullscreenOverlay(enabled);

        if (enabled)
            BotTurnPanel.ResetSessionOverlaySuppression();
    }

    public void SetBotAnimationSpeed(float speed)
    {
        AppSettingsManager.Instance.SetBotAnimationSpeed(speed);
    }

    private void BuildBotAnimationSection()
    {
        if (botAnimationSectionBuilt)
            return;

        RectTransform content = optionsContent;
        if (content == null && botAnimationSectionTemplate != null)
            content = botAnimationSectionTemplate.transform.parent as RectTransform;

        if (content == null || botAnimationSectionTemplate == null ||
            botAnimationSwitchRowTemplate == null || botAnimationSliderRowTemplate == null)
        {
            Debug.LogWarning("[OptionsScreen] Bot-Animation Templates sind nicht verdrahtet.");
            return;
        }

        botAnimationSectionBuilt = true;

        GameObject section = Instantiate(botAnimationSectionTemplate, content, false);
        section.name = "BotAnimation";
        section.transform.SetAsLastSibling();

        var clonedSectionBinders = section.GetComponentsInChildren<UISwitchBinder>(true);
        for (int i = 0; i < clonedSectionBinders.Length; i++)
        {
            clonedSectionBinders[i].enabled = false;
            Destroy(clonedSectionBinders[i]);
        }

        TMP_Text sectionHeadline = FindText(section.transform, "VibrationHeadline");
        SetLocalizedText(sectionHeadline, "Bot_animation", "Bot Animation");

        Transform rows = FindChild(section.transform, "Rows");
        if (rows == null)
            rows = section.transform;

        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            GameObject child = rows.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        GameObject toggleRow = Instantiate(botAnimationSwitchRowTemplate, rows, false);
        toggleRow.name = "FullscreenOverlay";
        var binder = toggleRow.GetComponentInChildren<UISwitchBinder>(true);
        if (binder != null)
        {
            binder.bindingType = SwitchBindingType.BotFullscreenOverlay;
            if (binder.groupsToEnableOnTrue != null)
                binder.groupsToEnableOnTrue.Clear();
        }

        SetLocalizedText(FindText(toggleRow.transform, "TextSettingHeadline"),
            "Fullscreen_overlay", "Fullscreen overlay");
        SetLocalizedText(FindText(toggleRow.transform, "TextSettingExplanation"),
            "Fullscreen_overlay_expl", "Show bot throws in a large overlay while the bot is playing.");

        GameObject sliderRow = Instantiate(botAnimationSliderRowTemplate, rows, false);
        sliderRow.name = "AnimationSpeed";
        SetLocalizedText(FindText(sliderRow.transform, "TextSettingHeadline"),
            "Animation_speed", "Animation speed");
        SetLocalizedText(FindText(sliderRow.transform, "TextSettingExplanation"),
            "Animation_speed_expl", "Controls how fast bot throws are animated.");

        var sliderGroup = sliderRow.GetComponent<CanvasGroup>();
        if (sliderGroup != null)
        {
            sliderGroup.alpha = 1f;
            sliderGroup.interactable = true;
            sliderGroup.blocksRaycasts = true;
        }

        botAnimationSpeedSlider = sliderRow.GetComponentInChildren<Slider>(true);
        if (botAnimationSpeedSlider != null)
        {
            botAnimationSpeedSlider.minValue = 0.5f;
            botAnimationSpeedSlider.maxValue = 2f;
            botAnimationSpeedSlider.wholeNumbers = false;
            botAnimationSpeedSlider.interactable = true;
            botAnimationSpeedSlider.onValueChanged.RemoveAllListeners();
            botAnimationSpeedSlider.SetValueWithoutNotify(
                AppSettingsManager.Instance.Settings.BotAnimation.AnimationSpeed);
            botAnimationSpeedSlider.onValueChanged.AddListener(OnBotAnimationSpeedChanged);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void OnBotAnimationSpeedChanged(float value)
    {
        AppSettingsManager.Instance.SetBotAnimationSpeed(value);
    }

    private static TMP_Text FindText(Transform parent, string name)
    {
        Transform child = FindChild(parent, name);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindChild(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void SetLocalizedText(TMP_Text text, string key, string fallback)
    {
        if (text == null)
            return;

        text.text = fallback;
        var localizeEvent = text.GetComponent<LocalizeStringEvent>()
            ?? text.gameObject.AddComponent<LocalizeStringEvent>();

        localizeEvent.OnUpdateString.RemoveAllListeners();
        localizeEvent.OnUpdateString.AddListener(localizedValue =>
        {
            if (!string.IsNullOrEmpty(localizedValue))
                text.text = localizedValue;
        });
        localizeEvent.StringReference = new LocalizedString("LocalizationTable", key);
        localizeEvent.RefreshString();
    }

    // =========================
    // UI LOGIC
    // =========================

    private void OnVolumeChanged(float value)
    {
        AppSettingsManager.Instance.SetVolume(value);
    }
}
