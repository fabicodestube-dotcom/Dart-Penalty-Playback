using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class AppSettings
{
    [JsonProperty] public ThemeColorScheme Theme = ThemeColorScheme.Green;

    [JsonProperty] public SoundSettings Sound = new SoundSettings();
    [JsonProperty] public VibrationSettings Vibration = new VibrationSettings();

    [JsonProperty] public PenaltySettings Penalties = new PenaltySettings();
}

[System.Serializable]
public class SoundSettings
{
    [JsonProperty] public bool Enabled = true;
    [JsonProperty] public bool UseCustomSounds = true;

    [Range(0f, 1f)]
    [JsonProperty] public float Volume = 1f; // 🔥 DAS ist neu
}

[System.Serializable]
public class VibrationSettings
{
    [JsonProperty] public bool Enabled = true;
    [JsonProperty] public VibrationStrength Strength = VibrationStrength.Medium;
}

public enum VibrationStrength
{
    Weak = 0,
    Medium = 1,
    Strong = 2
}

[System.Serializable]
public class PenaltySettings
{
    [JsonProperty] public List<PenaltySetting> Settings = new List<PenaltySetting>();

    public PenaltySettings Clone()
    {
        PenaltySettings copy = new PenaltySettings
        {
            Settings = new List<PenaltySetting>()
        };

        foreach (var setting in this.Settings)
        {
            copy.Settings.Add(new PenaltySetting
            {
                Type = setting.Type,
                Enabled = setting.Enabled,
                Cost = setting.Cost
            });
        }

        return copy;
    }

    public void SetPenaltyEnabled(PenaltyType type, bool enabled)
    {
        var p = Settings.Find(x => x.Type == type);
        if (p != null)
        {
            p.Enabled = enabled;
        }
    }

    public bool IsEnabled(PenaltyType type)
    {
        var p = Settings.Find(x => x.Type == type);
        return p != null && p.Enabled;
    }

    public void SetPenaltyCost(PenaltyType type, int value)
    {
        var p = Settings.Find(x => x.Type == type);
        if (p != null)
        {
            p.Cost = value;
        }
    }

    public int GetCost(PenaltyType type)
    {
        var p = Settings.Find(x => x.Type == type);
        return p != null ? p.Cost : 0;
    }
}

[System.Serializable]
public abstract class GameSettings
{
    [JsonProperty] public SetsAndLegs setsAndLegsMode;
    [JsonProperty] public int setCount;
    [JsonProperty] public int legCount;
    [JsonProperty] public bool soundEnabled;
    public abstract string GetString();
    [JsonProperty] public PenaltySettings Penalties;

    public abstract GameSettings Clone();

}

[System.Serializable]
public enum SetsAndLegs
{    
    [JsonProperty, Description("First To")] FirstTo,
    [JsonProperty, Description("Best Of")] BestOf
}

