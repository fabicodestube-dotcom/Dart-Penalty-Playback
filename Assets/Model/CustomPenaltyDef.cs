using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Bedingungsart eines (Custom-)Penalties. HitTypeMiss/RepdigitScore sind
/// Legacy (Altdaten + Evaluierung); der Editor bietet TurnPattern (2.1),
/// ExactScore (2.2) und Leg/Set/GameLost (2.3) an.
/// </summary>
public enum PenaltyConditionKind
{
    /// <summary>Einzelner Fehlwurf (Wand/Decke). Wird instant bei AddThrow geprüft. (Legacy)</summary>
    HitTypeMiss,
    /// <summary>Exakte Turn-Punktzahl (Punkte-Event).</summary>
    ExactScore,
    /// <summary>
    /// Exakter Rest-Score nach dem Turn (X01-Rest, Cricket-Total).
    /// Mit Übergangsregel: feuert nur bei neu erreichtem Total.
    /// Ersetzt die Legacy-Schnapszahl (Seeds 111–999).
    /// </summary>
    RemainingExactScore,
    /// <summary>Schnapszahl: Turn-Punktzahl ist Repdigit (11, 22, ..., 111, 222, ...). (Legacy)</summary>
    RepdigitScore,
    /// <summary>Bestimmte Würfe innerhalb einer Turn (z.B. 3x1).</summary>
    TurnPattern,
    /// <summary>Leg verloren (Kosten tragen alle außer Leg-Gewinner).</summary>
    LegLost,
    /// <summary>Set verloren (Kosten tragen alle außer Set-Gewinner).</summary>
    SetLost,
    /// <summary>Game verloren (Kosten tragen alle außer Match-Gewinner).</summary>
    GameLost
}

/// <summary>
/// Ein Slot eines Turn-Patterns. anyValue/anyMultiplier/anyHitType = Wildcard.
/// </summary>
[System.Serializable]
public class TurnPatternEntry
{
    [JsonProperty] public bool anyValue = true;
    [JsonProperty] public int value;
    [JsonProperty] public bool anyMultiplier = true;
    [JsonProperty] public DartMultiplier multiplier = DartMultiplier.Single;
    [JsonProperty] public bool anyHitType = true;
    [JsonProperty] public HitType hitType = HitType.Board;

    /// <summary>
    /// Sektor-ID bei hitType == Custom (Tab 1 „Bereiche“). Leer = jeder
    /// Custom-Sektor. Wird nur ausgewertet, wenn anyHitType false ist.
    /// </summary>
    [JsonProperty] public string customHitId;

    /// <summary>
    /// True = Wall-oder-Ceiling (für AllMiss-Slots). Board scheidet aus.
    /// </summary>
    [JsonProperty] public bool anyMissHitType;
}

/// <summary>
/// Auswertbare Bedingung eines Penalties.
/// </summary>
[System.Serializable]
public class PenaltyCondition
{
    [JsonProperty] public PenaltyConditionKind kind = PenaltyConditionKind.HitTypeMiss;

    // HitTypeMiss
    [JsonProperty] public HitType hitType = HitType.Wall;

    // ExactScore
    [JsonProperty] public int score;

    // RepdigitScore (Mindest-Punktzahl, Default 11)
    [JsonProperty] public int minScore = 11;

    // TurnPattern
    [JsonProperty] public List<TurnPatternEntry> pattern = new List<TurnPatternEntry>();

    /// <summary>
    /// Einziger Reihenfolge-Toggle (2.1): true = geordnet + exakt
    /// (Slot i muss Wurf i entsprechen), false = ungeordnet + exakt
    /// (Pattern als Multimenge im Turn). Ersetzt das alte matchExact
    /// (Altdaten ohne matchOrder verhalten sich wie false).
    /// </summary>
    [JsonProperty] public bool matchOrder = false;
}

/// <summary>
/// Benutzerdefiniertes Penalty (stabile String-ID, damit Audio-Clips beim
/// Editieren verlinkt bleiben). Built-ins bleiben PenaltyType-Enum.
/// </summary>
[System.Serializable]
public class CustomPenaltyDef
{
    [JsonProperty] public string id;
    [JsonProperty] public string name = "Neues Penalty";

    /// <summary>
    /// Optionale Kurzbezeichnung für schmale Tabellenspalten (z.B. "Floor"
    /// statt "ExtraLangerPenaltyName..."). Leer = Anzeigename (ggf. gekürzt).
    /// </summary>
    [JsonProperty] public string shortName;

    [JsonProperty] public bool enabled = true;

    /// <summary>
    /// True = Seed (nicht löschbar, aber normal evaluiert/editierbar).
    /// Penalty-Seeds sind löschbar (false); Event-/Sektor-Seeds nutzen das
    /// Flag zum Löschschutz.
    /// </summary>
    [JsonProperty] public bool builtIn;

    /// <summary>
    /// Kosten. Wird hier editiert/gespeichert; die Abrechnungslogik
    /// (Anwendung im Spiel) folgt separat.
    /// </summary>
    [JsonProperty] public int cost = 10;

    [JsonProperty] public PenaltyCondition condition = new PenaltyCondition();

    /// <summary>"builtin:wall" → PenaltyType.Wall (für PenaltySettings-Sync und Enum-Clip-Buttons).</summary>
    public static bool TryParseBuiltInType(string id, out PenaltyType type)
    {
        type = default;

        if (string.IsNullOrEmpty(id) || !id.StartsWith("builtin:"))
            return false;

        foreach (PenaltyType candidate in System.Enum.GetValues(typeof(PenaltyType)))
        {
            if (string.Equals(candidate.ToString(), id.Substring(8),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        return false;
    }

    public static CustomPenaltyDef CreateNew()
    {
        return new CustomPenaltyDef
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = "Neues Penalty",
            enabled = true,
            cost = 10,
            condition = new PenaltyCondition
            {
                kind = PenaltyConditionKind.TurnPattern,
                matchOrder = false,
                pattern = new List<TurnPatternEntry>
                {
                    new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                    new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                    new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true }
                }
            }
        };
    }

    /// <summary>True für LegLost/SetLost/GameLost (Auswertung an Leg-/Set-/Game-Ende).</summary>
    public static bool IsGameEventKind(PenaltyConditionKind kind)
    {
        return kind == PenaltyConditionKind.LegLost
            || kind == PenaltyConditionKind.SetLost
            || kind == PenaltyConditionKind.GameLost;
    }
}
