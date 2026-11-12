using System.Collections.Generic;
using UnityEngine;

public enum SwitchBindingType
{
    SoundEnabled,
    UseCustomSounds,
    VibrationEnabled,

    /// <summary>
    /// Use built-in clips as fallback. NEW — always attach to the end:
    /// Unity serializes enums as int, so an insertion in the middle would then
    /// Move existing bindingType values in the scene.
    /// </summary>
    UseDefaultClipsAsFallback,

    /// <summary>
    /// Dark mode (true = dark, false = light). NEW — always attach to the end.
    /// </summary>
    DarkMode
}

public class UISwitchBinder : MonoBehaviour
{
    public UISwitch uiSwitch;
    public SwitchBindingType bindingType;
    public List<CanvasGroup> groupsToEnableOnTrue; // Optional groups that are activated at true and deactivated at false

    private OptionsScreen options;

    public void Initialize(OptionsScreen optionsHandler)
    {
        options = optionsHandler;

        uiSwitch.toggle.onValueChanged.RemoveListener(OnValueChanged);
        uiSwitch.toggle.onValueChanged.AddListener(OnValueChanged);

        SetInitialState();
    }

    private void SetInitialState()
    {
        bool value = false;

        switch (bindingType)
        {
            case SwitchBindingType.SoundEnabled:
                value = AppSettingsManager.Instance.Settings.Sound.Enabled;
                break;

            case SwitchBindingType.UseCustomSounds:
                value = AppSettingsManager.Instance.Settings.Sound.UseCustomSounds;
                break;

            case SwitchBindingType.VibrationEnabled: // NEW
                value = AppSettingsManager.Instance.Settings.Vibration.Enabled;
                break;

            case SwitchBindingType.UseDefaultClipsAsFallback:
                value = AppSettingsManager.Instance.Settings.Sound.UseDefaultClipsAsFallback;
                break;

            case SwitchBindingType.DarkMode:
                value = AppSettingsManager.Instance.Settings.ThemeMode == ThemeMode.Dark;
                break;
        }

        uiSwitch.toggle.SetIsOnWithoutNotify(value);
        uiSwitch.RefreshVisual();
        UpdateCanvasGroups(value);
    }

    private void OnValueChanged(bool value)
    {
        switch (bindingType)
        {
            case SwitchBindingType.SoundEnabled:
                options.SetSound(value);
                break;

            case SwitchBindingType.UseCustomSounds:
                options.SetCustomSound(value);
                break;

            case SwitchBindingType.VibrationEnabled: // NEW
                options.SetVibration(value);
                break;

            case SwitchBindingType.UseDefaultClipsAsFallback:
                options.SetDefaultClipsFallback(value);
                break;

            case SwitchBindingType.DarkMode:
                options.SetThemeMode(value);
                break;
        }

        UpdateCanvasGroups(value);
    }

    public void SetInteractable(bool interactable)
    {
        uiSwitch.toggle.interactable = interactable;
    }

    public void SetValue(bool value)
    {
        uiSwitch.toggle.SetIsOnWithoutNotify(value);
    }

    private void UpdateCanvasGroups(bool value)
    {
        foreach (var group in groupsToEnableOnTrue)
        {
            group.alpha = value ? 1 : 0.4f;
            group.interactable = value;
            group.blocksRaycasts = value;
        }
    }
}