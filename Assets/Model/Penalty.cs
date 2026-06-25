using Newtonsoft.Json;

[System.Serializable]
public enum PenaltyType
{
    [JsonProperty] Wall,
    [JsonProperty] Ceiling,
    [JsonProperty] Schnapszahl,
    [JsonProperty] ThreeOnes,
    [JsonProperty] LostGame,
    [JsonProperty] AllMiss
}

[System.Serializable]
public class Penalty
{
    [JsonProperty] public int PlayerId;
    [JsonProperty] public PenaltyType Type;
    [JsonProperty] public Turn Turn; // 🔴 Referenz für Undo
}

[System.Serializable]
public enum PenaltyTiming
{
    [JsonProperty] Instant,
    [JsonProperty] EndOfTurn
}