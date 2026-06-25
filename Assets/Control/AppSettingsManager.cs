using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class AppSettingsManager : MonoBehaviour
{
    public static AppSettingsManager Instance { get; private set; }

    public AppSettings Settings { get; private set; }

    private string filePath;

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

        filePath = Path.Combine(Application.persistentDataPath, "appsettings.json");

        Load();
    }

    // =========================================================
    // LOAD
    // =========================================================
    public void Load()
    {
        if (!File.Exists(filePath))
        {
            Debug.Log("[Settings] No settings file found -> create defaults");
            CreateDefaultSettings();
            Save();

            ApplyLoadedSettings(); // 🔥 wichtig
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            Settings = JsonConvert.DeserializeObject<AppSettings>(json);

            Debug.Log("[Settings] Loaded from file");

            EnsureDefaults();

            ApplyLoadedSettings(); // 🔥 wichtig
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Settings] Load failed: {e.Message}");
            CreateDefaultSettings();

            ApplyLoadedSettings(); // 🔥 wichtig
        }
    }

    // =========================================================
    // SAVE
    // =========================================================
    public void Save()
    {
        try
        {
            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Settings] Save failed: {e.Message}");
        }
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
            
        // 🎨 Theme anwenden
        ThemeManager.Instance.Initialize(Settings.Theme);
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