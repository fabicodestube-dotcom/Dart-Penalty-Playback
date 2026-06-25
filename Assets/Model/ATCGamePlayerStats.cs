using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class ATCGamePlayerStats
{
    [JsonProperty] public int GamesPlayed;
    [JsonProperty] public int GamesWon;
    [JsonProperty] public int LegsPlayed;
    [JsonProperty] public int LegsWon;
    [JsonProperty] public int SetsWon;
    [JsonProperty] public int TargetsHit;
    [JsonProperty] public int CompletedRounds;
    [JsonProperty] public int TotalPenaltyCost;
    [JsonProperty] public Dictionary<PenaltyType, int> Penalties = new Dictionary<PenaltyType, int>();
    [JsonProperty] public float FirstDartHitPercentage;
    [JsonProperty] public int LongestHitStreak;
    [JsonProperty] public int Chokes; // Target with most throws needed
    [JsonProperty] public Dictionary<int, float> HitSectors = new Dictionary<int, float>(); // Target -> Hit %
    [JsonProperty] public Dictionary<int, int> ThrowsPerTarget = new Dictionary<int, int>(); // Target -> Throws count
    [JsonProperty] public Dictionary<int, int> HitsPerTarget = new Dictionary<int, int>(); // Target -> Hits count
}
