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
    [JsonProperty] Ceiling
}