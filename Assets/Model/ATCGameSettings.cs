using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public enum ATCTargetType
{
    [JsonProperty] Singles,
    [JsonProperty] Doubles,
    [JsonProperty] Triples,
}

[System.Serializable]
public enum ATCOrder
{
    [JsonProperty] Ascending,
    [JsonProperty] Descending,
    [JsonProperty] Random
}

[System.Serializable]
public class ATCGameSettings : GameSettings
{
    [JsonProperty] public ATCTargetType targetType = ATCTargetType.Singles;
    [JsonProperty] public ATCOrder order = ATCOrder.Ascending;


    public override string GetString()
    {
        string target = targetType.ToString();
        string ord = order.ToString();

        string setText = setCount == 1 ? "Set" : "Sets";
        string legText = legCount == 1 ? "Leg" : "Legs";

        string modeText = setsAndLegsMode == SetsAndLegs.BestOf
            ? "Best of"
            : "First to";

        return $"{target}, {ord}, {modeText} {setCount} {setText} {legCount} {legText}";
    }

    public bool IsValid(out string reason)
    {
        var allowedCounts = new HashSet<int> { 1, 3, 5, 7, 9 };
        if (!allowedCounts.Contains(setCount) || !allowedCounts.Contains(legCount))
        {
            reason = "Sets/Legs müssen 1, 3, 5, 7 oder 9 sein";
            return false;
        }

        reason = null;
        return true;
    }

    public TargetOption GetTargetOption()
    {
        switch (targetType)
        {
            case ATCTargetType.Singles:
                return new TargetOption(singles: true, doubles: false, triples: false);
            case ATCTargetType.Doubles:
                return new TargetOption(singles: false, doubles: true, triples: false);
            case ATCTargetType.Triples:
                return new TargetOption(singles: false, doubles: false, triples: true);
            default:
                return new TargetOption(singles: true, doubles: false, triples: false);
        }
    }

    public override GameSettings Clone()
    {
        return new ATCGameSettings
        {
            setsAndLegsMode = setsAndLegsMode,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = soundEnabled,
            targetType = targetType,
            order = order,
            Penalties = Penalties?.Clone()
        };
    }

}

