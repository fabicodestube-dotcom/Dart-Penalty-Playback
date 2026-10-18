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
    /// Die Sektor-ID steht in Throw.CustomHitId. Zählt wie Wall/Ceiling
    /// als Fehlwurf (0 Punkte, kein Board-Treffer).
    /// </summary>
    [JsonProperty] Custom
}