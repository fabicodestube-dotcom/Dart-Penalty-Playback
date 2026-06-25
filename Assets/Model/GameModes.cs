using Newtonsoft.Json;

[System.Serializable]
public enum GameMode
{
    [JsonProperty] All, 
    [JsonProperty] X01,
    [JsonProperty] Cricket,
    [JsonProperty] ATC
}