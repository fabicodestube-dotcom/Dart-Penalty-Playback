using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;

public class AppSettingsManager : MonoBehaviour
{
    public static AppSettingsManager Instance { get; private set; }

    public AppSettings Settings { get; private set; }

    private string legacyFilePath;
    private bool isChangingLanguage = false;

    private void Awake()
    {
        // Singleton absichern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        legacyFilePath = Path.Combine(Application.persistentDataPath, "appsettings.json");

        Load();
    }

    // =========================================================
    // LOAD
    // =========================================================
    public void Load()
    {
        try
        {
            Settings = SqliteDatabaseService.LoadSettings();
            Debug.Log("[Settings] Loaded from SQLite");
            EnsureDefaults();
            ApplyLoadedSettings();
            return;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Settings] SQLite load failed: {e.Message}. Falling back to legacy JSON migration.");
        }

        try
        {
            if (File.Exists(legacyFilePath))
            {
                string json = File.ReadAllText(legacyFilePath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                EnsureDefaults();
                Save();
                Debug.Log("[Settings] Migrated legacy JSON settings into SQLite");
                ApplyLoadedSettings();
                return;
            }
        }
        catch (System.Exception legacyException)
        {
            Debug.LogError($"[Settings] Legacy JSON migration failed: {legacyException.Message}");
        }

        Debug.Log("[Settings] No settings found -> create defaults");
        CreateDefaultSettings();
        Save();
        ApplyLoadedSettings();
    }

    // =========================================================
    // SAVE
    // =========================================================
    public void Save()
    {
        try
        {
            SqliteDatabaseService.SaveSettings(Settings);
            Debug.Log("[Settings] Saved to SQLite");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Settings] Save failed: {e.Message}");
        }
    }

    // =========================
    // LANGUAGE SETTINGS
    // =========================

    public void SetLanguage(string localeCode)
    {
        StartCoroutine(ChangeLocaleCoroutine(localeCode));
    }

    private IEnumerator ChangeLocaleCoroutine(string localeCode)
    {
        isChangingLanguage = true;
        
        // Wait for system initialization to complete before changing the locale
        yield return LocalizationSettings.InitializationOperation;

        // Find the target locale based on the provided locale code
        Locale targetLocale = LocalizationSettings.AvailableLocales.Locales.Find(
            locale => locale.Identifier.Code == localeCode
        );

        if (targetLocale != null)
        {
            LocalizationSettings.SelectedLocale = targetLocale;
            Debug.Log($"Language successfully changed to: {localeCode}");
        }
        else
        {
            Debug.LogWarning($"Language code '{localeCode}' was not found in the available locales!");
        }

        isChangingLanguage = false;
    }

    // =========================
    // THEME SETTINGS
    // =========================

    public void SetTheme(ThemeColorScheme scheme)
    {
        // Aktualisiert aktiv das Theme im laufenden System
        ThemeManager.Instance.SetTheme(scheme);
        Settings.Theme = scheme;
        Save();
    }

    // =========================
    // SOUND SETTINGS
    // =========================

    public void SetSoundEnabled(bool enabled)
    {
        Settings.Sound.Enabled = enabled;
        Save();
    }

    public void SetCustomSoundsEnabled(bool enabled)
    {
        Settings.Sound.UseCustomSounds = enabled;
        Save();
    }

    public void SetVolume(float volume)
    {
        Settings.Sound.Volume = volume;
        Save();
    }

    // =========================
    // VIBRATION SETTINGS
    // =========================

    public void SetVibrationEnabled(bool enabled)
    {
        Settings.Vibration.Enabled = enabled;
        Save();
    }

    public void SetVibrationStrength(VibrationStrength strength)
    {
        Settings.Vibration.Strength = strength;
        Save();
    }

    // =========================
    // PENALTY SETTINGS
    // =========================

    public void SetPenaltyEnabled(PenaltyType type, bool value)
    {
        Settings.Penalties.SetPenaltyEnabled(type, value);
        Save();
    }

    public void SetPenaltyCost(PenaltyType type, int value)
    {
        Settings.Penalties.SetPenaltyCost(type, value);
        Save();
    }


    // =========================================================
    // DEFAULTS
    // =========================================================
    private void CreateDefaultSettings()
    {
        Settings = new AppSettings();

        // =========================
        // 🎨 THEME DEFAULT
        // =========================
        Settings.Theme = ThemeColorScheme.Green;

        // =========================
        // 🔊 SOUND DEFAULT
        // =========================
        Settings.Sound = new SoundSettings
        {
            Enabled = true,
            UseCustomSounds = true,
            Volume = 1f
        };

        // =========================
        // 📳 VIBRATION DEFAULT
        // =========================
        Settings.Vibration = new VibrationSettings
        {
            Enabled = true,
            Strength = VibrationStrength.Weak
        };

        // =========================
        // 🎯 PENALTIES DEFAULT
        // =========================
        Settings.Penalties.Settings = new System.Collections.Generic.List<PenaltySetting>
        {
            new PenaltySetting { Type = PenaltyType.Wall, Enabled = true, Cost = 10 },
            new PenaltySetting { Type = PenaltyType.Ceiling, Enabled = true, Cost = 20 },
            new PenaltySetting { Type = PenaltyType.AllMiss, Enabled = true, Cost = 100 },
            new PenaltySetting { Type = PenaltyType.ThreeOnes, Enabled = true, Cost = 100 },
            new PenaltySetting { Type = PenaltyType.LostGame, Enabled = true, Cost = 50 },
            new PenaltySetting { Type = PenaltyType.Schnapszahl, Enabled = true, Cost = 50 }
        };
    }

    // =========================================================
    // SAFETY: fehlende Einträge ergänzen
    // =========================================================
    private void EnsureDefaults()
    {
        foreach (PenaltyType type in System.Enum.GetValues(typeof(PenaltyType)))
        {
            var existing = Settings.Penalties.Settings.Find(x => x.Type == type);

            if (existing == null)
            {
                Debug.Log($"[Settings] Missing penalty added: {type}");

                Settings.Penalties.Settings.Add(new PenaltySetting
                {
                    Type = type,
                    Enabled = true,
                    Cost = GetDefaultCost(type)
                });
            }
        }
    }

    private int GetDefaultCost(PenaltyType type)
    {
        switch (type)
        {
            case PenaltyType.Wall: return 10;
            case PenaltyType.Ceiling: return 20;
            case PenaltyType.AllMiss: return 100;
            case PenaltyType.ThreeOnes: return 100;
            case PenaltyType.Schnapszahl: return 50;
            case PenaltyType.LostGame: return 50;
            default: return 0;
        }
    }


    // ================= UNITY LIFECYCLE ================= //

    private void ApplyLoadedSettings()
    {
        if (Settings == null)
            return;
        
        // Start a coroutine to wait for the localization system before applying the language
        StartCoroutine(InitializeLanguageRoutine());

        // 🎨 Apply theme (themes can usually be applied instantly)
        ThemeManager.Instance.Initialize(Settings.Theme);
    }


    private IEnumerator InitializeLanguageRoutine()
    {
        // Wait until Unity's localization system is fully initialized
        yield return UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;

        // If the file was newly created, this contains "en", otherwise the saved code
        string codeToLoad = Settings.Language.lastLocaleCode;
        
        Debug.Log($"[Settings] Localization ready. Applying saved locale: {codeToLoad}");

        // Calls your coroutine/method to trigger the language switch in Unity
        SetLanguage(codeToLoad); 
    }




    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Save();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }
}