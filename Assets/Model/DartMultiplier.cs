using Newtonsoft.Json;

[System.Serializable]
public enum DartMultiplier
{
    [JsonProperty] Single,
    [JsonProperty] Double,
    [JsonProperty] Triple
}

[System.Serializable]
public enum HitType
{
    [JsonProperty] Board,
    [JsonProperty] Wall,
    [JsonProperty] Ceiling,
    /// <summary>
    /// Benutzerdefinierter Treffer-Bereich (Tab 1 „Bereiche“).
    /// Die Sektor-ID steht in Throw.CustomHitId. Score hängt von Value/Multiplier ab
    /// (0 = Miss).
    /// </summary>
    [JsonProperty] Custom
}