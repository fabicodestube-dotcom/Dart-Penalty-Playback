using System.Collections.Generic;
using Newtonsoft.Json;  

[System.Serializable]
public class X01GamePlayerStats
{
    [JsonProperty] public int GamesPlayed = 1; // Immer 1 für dieses Spiel
    [JsonProperty] public int GamesWon = 0;
    [JsonProperty] public int LegsPlayed = 0;
    [JsonProperty] public int LegsWon = 0;
    [JsonProperty] public float AvgPoints = 0f;
    [JsonProperty] public float DoublePercent = 0f;
    [JsonProperty] public float TriplePercent = 0f;
    [JsonProperty] public int BestTurn = 0;
    [JsonProperty] public int Count60 = 0;
    [JsonProperty] public int Count100 = 0;
    [JsonProperty] public int Count140 = 0;
    [JsonProperty] public int Count180 = 0;
    [JsonProperty] public int HighestCheckout = 0;
    [JsonProperty] public Dictionary<string, int> CheckoutDoubles = new Dictionary<string, int>();
    [JsonProperty] public Dictionary<PenaltyType, int> Penalties = new Dictionary<PenaltyType, int>();
    [JsonProperty] public float TotalPenaltyCost = 0f;
}