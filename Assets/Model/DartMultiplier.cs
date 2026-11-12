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
    /// Custom hit area (Tab 1 “Areas”).
    /// The sector ID is in Throw.CustomHitId. Score depends on Value/Multiplier
    /// (0 = Miss).
    /// </summary>
    [JsonProperty] Custom
}