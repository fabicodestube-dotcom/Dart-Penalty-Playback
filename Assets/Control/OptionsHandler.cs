using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsHandler : MonoBehaviour
{
    public WindowHandler windowHandler;
    //public AppHandler appHandler;

    public List<UISwitchBinder> binders;


    // Volume
    [SerializeField] private Slider volumeSlider;

    // Vibration
    [SerializeField] private SingleSelectionGroup vibrationStrengthSelection;

    // Penalties
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


    // =========================
    // INITIALIZATION
    // =========================

    private void InitializeUI()
    {
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
