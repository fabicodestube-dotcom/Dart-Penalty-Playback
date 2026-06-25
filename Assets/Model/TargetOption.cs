using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class TargetOption
{
    [JsonProperty] public bool singles;
    [JsonProperty] public bool doubles;
    [JsonProperty] public bool triples;

    public TargetOption(bool singles, bool doubles, bool triples)
    {
        this.singles = singles;
        this.doubles = doubles;
        this.triples = triples;
    }

    public void ToggleSingle()
    {
        singles = !singles;
    }

    public void ToggleDouble()
    {
        doubles = !doubles;
    }

    public void ToggleTriples()
    {
        triples = !triples;
    }
}