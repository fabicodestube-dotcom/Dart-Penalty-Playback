using Newtonsoft.Json;

[System.Serializable]
public class PenaltySetting
{
    [JsonProperty] public PenaltyType Type;
    [JsonProperty] public bool Enabled = true;
    [JsonProperty] public int Cost = 1;
}