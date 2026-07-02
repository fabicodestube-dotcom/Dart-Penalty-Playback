using System.Collections.Generic;
using UnityEngine;

public enum SwitchBindingType
{
    SoundEnabled,
    UseCustomSounds,
    VibrationEnabled,
    PenaltyEnabled
}

public class UISwitchBinder : MonoBehaviour
{
    public UISwitch uiSwitch;
    public SwitchBindingType bindingType;
    public PenaltyType penaltyType;
    public List<CanvasGroup> groupsToEnableOnTrue; // Optionale Gruppen, die bei true aktiviert und bei false deaktiviert werden

    private OptionsHandler options;

    public void Initialize(OptionsHandler optionsHandler)
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

            case SwitchBindingType.VibrationEnabled: // NEU
                value = AppSettingsManager.Instance.Settings.Vibration.Enabled;
                break;

            case SwitchBindingType.PenaltyEnabled:
                var p = AppSettingsManager.Instance.Settings
                    .Penalties.Settings
                    .Find(x => x.Type == penaltyType);

                value = p != null && p.Enabled;
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

            case SwitchBindingType.VibrationEnabled: // NEU
                options.SetVibration(value);
                break;

            case SwitchBindingType.PenaltyEnabled:
                AppSettingsManager.Instance.SetPenaltyEnabled(penaltyType, value);
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