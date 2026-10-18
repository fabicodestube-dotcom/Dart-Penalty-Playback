using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public enum ATCTargetType
{
    [JsonProperty] Singles,
    [JsonProperty] Doubles,
    [JsonProperty] Triples,
    [JsonProperty] SinglesDoubles,
    [JsonProperty] SinglesTriples,
    [JsonProperty] DoublesTriples,
    [JsonProperty] Everything,
}

public static class ATCTargetTypeExtensions
{
    /// <summary>Anzeigename für Setup-Label und Spielzusammenfassungen.</summary>
    public static string ToDisplayName(this ATCTargetType type)
    {
        return type switch
        {
            ATCTargetType.Singles => "Singles",
            ATCTargetType.Doubles => "Doubles",
            ATCTargetType.Triples => "Triples",
            ATCTargetType.SinglesDoubles => "Singles + Doubles",
            ATCTargetType.SinglesTriples => "Singles + Triples",
            ATCTargetType.DoublesTriples => "Doubles + Triples",
            ATCTargetType.Everything => "Everything",
            _ => type.ToString()
        };
    }

    /// <summary>Ist der Single-Ring für diesen Modus ein gültiger Treffer?</summary>
    public static bool IncludesSingles(this ATCTargetType type)
    {
        return type is ATCTargetType.Singles
            or ATCTargetType.SinglesDoubles
            or ATCTargetType.SinglesTriples
            or ATCTargetType.Everything;
    }

    /// <summary>Ist der Double-Ring für diesen Modus ein gültiger Treffer?</summary>
    public static bool IncludesDoubles(this ATCTargetType type)
    {
        return type is ATCTargetType.Doubles
            or ATCTargetType.SinglesDoubles
            or ATCTargetType.DoublesTriples
            or ATCTargetType.Everything;
    }

    /// <summary>Ist der Triple-Ring für diesen Modus ein gültiger Treffer?</summary>
    public static bool IncludesTriples(this ATCTargetType type)
    {
        return type is ATCTargetType.Triples
            or ATCTargetType.SinglesTriples
            or ATCTargetType.DoublesTriples
            or ATCTargetType.Everything;
    }
}


[System.Serializable]
public enum ATCOrder
{
    [JsonProperty] Ascending,
    [JsonProperty] Descending,
    [JsonProperty] Right,
    [JsonProperty] Left,
    [JsonProperty] Random
}

[System.Serializable]
public class ATCGameSettings : GameSettings
{
    [JsonProperty] public ATCTargetType targetType = ATCTargetType.Singles;
    [JsonProperty] public ATCOrder order = ATCOrder.Ascending;


    public override string GetString()
    {
        string target = targetType.ToDisplayName();
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
        return new TargetOption(
            singles: targetType.IncludesSingles(),
            doubles: targetType.IncludesDoubles(),
            triples: targetType.IncludesTriples());
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

