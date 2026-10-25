using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Anzeige-Texte für Penalty-Bedingungen (Seite 1) und Trigger (Seite 2).
/// </summary>
public static class PenaltyDisplay
{
    public static string SummarizeCondition(PenaltyCondition condition)
    {
        if (condition == null)
            return "-";

        switch (condition.kind)
        {
            case PenaltyConditionKind.HitTypeMiss:
                return condition.hitType == HitType.Wall ? "Board daneben: Wand"
                    : condition.hitType == HitType.Ceiling ? "Board daneben: Decke"
                    : $"Fehlwurf: {condition.hitType}";

            case PenaltyConditionKind.ExactScore:
                return $"Turn = {condition.score}";

            case PenaltyConditionKind.RemainingExactScore:
                return $"Rest = {condition.score}";

            case PenaltyConditionKind.RepdigitScore:
                return $"Schnapszahl ≥ {condition.minScore}";

            case PenaltyConditionKind.TurnPattern:
                return $"Turn [{DescribePattern(condition.pattern)}]"
                    + (condition.matchOrder ? " (Reihenfolge)" : " (egal)");

            case PenaltyConditionKind.LegLost:
                return "Leg verloren";

            case PenaltyConditionKind.SetLost:
                return "Set verloren";

            case PenaltyConditionKind.GameLost:
                return "Game verloren";

            default:
                return "-";
        }
    }

    /// <summary>Sektor-Name aus Tab 1 (Fallback: Kurz-ID).</summary>
    public static string SectorName(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return "-";

        if (string.Equals(sectorId, "builtin:wall", System.StringComparison.OrdinalIgnoreCase))
            return "Wand";
        if (string.Equals(sectorId, "builtin:ceiling", System.StringComparison.OrdinalIgnoreCase))
            return "Decke";

        try
        {
            var def = CustomizationStore.GetHitSectors()?.Find(x => x != null && x.id == sectorId);
            if (def != null && !string.IsNullOrEmpty(def.name))
                return def.name;
        }
        catch (System.Exception)
        {
            // Store/DB nicht bereit -> Fallback unten
        }

        return sectorId.Length > 6 ? sectorId.Substring(0, 6) : sectorId;
    }

    public static string DescribePattern(List<TurnPatternEntry> pattern)
    {
        if (pattern == null || pattern.Count == 0)
            return "-";

        var parts = new List<string>(pattern.Count);
        foreach (var slot in pattern)
        {
            if (slot == null)
            {
                parts.Add("?");
                continue;
            }

            if (slot.anyMissHitType)
            {
                parts.Add("Miss");
                continue;
            }

            string value = slot.anyValue ? "*" : slot.value.ToString();
            string mult = slot.anyMultiplier ? "" : slot.multiplier switch
            {
                DartMultiplier.Single => "S",
                DartMultiplier.Double => "D",
                DartMultiplier.Triple => "T",
                _ => ""
            };
            string hit = slot.anyHitType || slot.hitType == HitType.Board ? ""
                : slot.hitType == HitType.Custom ? $"@{SectorName(slot.customHitId)}"
                : $"@{slot.hitType}";
            parts.Add(string.IsNullOrEmpty(mult) ? value : $"{mult}{value}{hit}");
        }

        return string.Join(",", parts);
    }

    public static string BuiltInSummary(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Wall => "Board daneben: Wand",
            PenaltyType.Ceiling => "Board daneben: Decke",
            PenaltyType.AllMiss => "Turn ohne Boardtreffer",
            PenaltyType.ThreeOnes => "Turn [1,1,1] (exakt)",
            PenaltyType.Schnapszahl => "Schnapszahl (Rest ≥ 100)",
            PenaltyType.LostGame => "Spiel verloren",
            _ => type.ToString()
        };
    }

    public static string BuiltInDisplayName(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Wall => "Wall",
            PenaltyType.Ceiling => "Ceiling",
            PenaltyType.AllMiss => "No board hit",
            PenaltyType.ThreeOnes => "Three Ones",
            PenaltyType.Schnapszahl => "Lucky number",
            PenaltyType.LostGame => "Lose",
            _ => type.ToString()
        };
    }

    public static string SummarizeTrigger(AudioEventDef def)
    {
        if (def == null || def.trigger == null)
            return "-";

        var t = def.trigger;
        return t.kind switch
        {
            AudioEventTriggerKind.Manual => "Manuell",
            AudioEventTriggerKind.ScoreReached => $"Score = {t.score}",
            AudioEventTriggerKind.TurnPattern =>
                $"Turn [{DescribePattern(t.pattern)}]" + (t.matchOrder ? " (Reihenfolge)" : " (egal)"),
            AudioEventTriggerKind.MarksThreshold => $"Marken ≥ {t.threshold}",
            AudioEventTriggerKind.PointsReached => $"Punkte ≥ {t.threshold}",
            AudioEventTriggerKind.HitsInTurnThreshold => $"Treffer/Turn ≥ {t.threshold}",
            AudioEventTriggerKind.StreakReached => $"Streak ≥ {t.threshold}",
            _ => "-"
        };
    }

    /// <summary>
    /// Kurze Spaltenüberschrift für ein Custom-Penalty (fixe Breite!).
    /// shortName wenn gesetzt, sonst Name auf ~12 Zeichen gekürzt.
    /// </summary>
    public static string ShortLabel(CustomPenaltyDef def)
    {
        if (def == null)
            return "-";

        if (!string.IsNullOrWhiteSpace(def.shortName))
            return def.shortName.Trim();

        return Truncate(def.name, 12);
    }

    /// <summary>Kurze Überschriften der 6 Legacy-Spalten (statt Icons).</summary>
    public static string ShortLabel(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Wall => "Wall",
            PenaltyType.Ceiling => "Decke",
            PenaltyType.AllMiss => "Miss",
            PenaltyType.ThreeOnes => "1en",
            PenaltyType.Schnapszahl => "Schnaps",
            PenaltyType.LostGame => "Lose",
            _ => type.ToString()
        };
    }

    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? "-";

        return text.Substring(0, maxLength) + "…";
    }

    /// <summary>
    /// Kompaktzeile für History-Rows: Top-N Customs als "Name ×n", Rest als
    /// "+k weitere". Leer wenn keine Customs. Daten aus Snapshot-Resolve.
    /// </summary>
    public static string CompactCustomSummary(GameSummary summary, System.Guid playerId, int topN = 3)
    {
        var entries = ResolveCustomEntries(summary, playerId);
        if (entries.Count == 0)
            return "";

        var top = entries
            .OrderByDescending(e => e.Count * e.Cost)
            .ThenBy(e => e.Name)
            .Take(topN)
            .Select(e => $"{Truncate(e.Name, 14)} {e.Count}×")
            .ToList();

        string text = string.Join(" • ", top);
        if (entries.Count > top.Count)
            text += $" • +{entries.Count - top.Count}";

        return text;
    }

    /// <summary>Stabile Spalten-ID für Legacy-Typen (z.B. "legacy:Wall").</summary>
    public static string LegacyColumnId(PenaltyType type) => "legacy:" + type;

    /// <summary>True, wenn die Spalten-ID ein Legacy-Typ ist.</summary>
    public static bool TryParseLegacyColumnId(string columnId, out PenaltyType type)
    {
        type = default;
        if (string.IsNullOrEmpty(columnId) || !columnId.StartsWith("legacy:"))
            return false;
        return System.Enum.TryParse(columnId.Substring(7), out type);
    }


    public static readonly PenaltyType[] LegacyPenaltyTypes =
    {
        PenaltyType.Wall,
        PenaltyType.Ceiling,
        PenaltyType.AllMiss,
        PenaltyType.ThreeOnes,
        PenaltyType.Schnapszahl,
        PenaltyType.LostGame
    };


    public static int LegacyCount(GameStats stats, PenaltyType type)
    {
        if (stats == null)
            return 0;

        return type switch
        {
            PenaltyType.Wall => stats.wallCount,
            PenaltyType.Ceiling => stats.ceilingCount,
            PenaltyType.AllMiss => stats.allMissCount,
            PenaltyType.ThreeOnes => stats.tripleOnesCount,
            PenaltyType.Schnapszahl => stats.tripleDigitCount,
            PenaltyType.LostGame => stats.lostGame,
            _ => 0
        };
    }

    /// <summary>Gesamt-Anzahl kassierter Penalties (Legacy + Customs).</summary>
    public static int TotalPenaltyCount(GameStats stats)
    {
        if (stats == null)
            return 0;

        int total = stats.wallCount + stats.ceilingCount + stats.allMissCount
            + stats.tripleOnesCount + stats.tripleDigitCount + stats.lostGame;

        if (stats.customPenaltyCounts != null)
        {
            foreach (var kvp in stats.customPenaltyCounts)
            {
                if (kvp.Value > 0)
                    total += kvp.Value;
            }
        }

        return total;
    }

    /// <summary>Gesamt-Anzahl kassierter Penalties aus einem Summary-Eintrag.</summary>
    public static int TotalPenaltyCount(PlayerSummaryEntry entry)
    {
        if (entry == null)
            return 0;

        int total = entry.WallCount + entry.CeilingCount + entry.AllMissCount
            + entry.ThreeOnesCount + entry.TripleDigitCount + entry.LostGameCount;

        if (entry.CustomPenaltyCounts != null)
        {
            foreach (var kvp in entry.CustomPenaltyCounts)
            {
                if (kvp.Value > 0)
                    total += kvp.Value;
            }
        }

        return total;
    }

    /// <summary>
    /// Verhängte Summe EINES Spielers: Legacy-Counts × Settings-Kosten plus
    /// Custom-Counts × Snapshot-Kosten. Deckt ein Snapshot-Custom Wall oder
    /// Ceiling ab (Suppression), zählt der Legacy-Typ nicht extra mit.
    /// Rein snapshot-basiert — überlebt Löschen/Ändern der Def.
    /// </summary>
    public static float PlayerImposedTotal(
        GameSummary summary, PlayerSummaryEntry entry, GameSettings settings)
    {
        if (entry == null)
            return 0f;

        float total = 0f;
        bool wallCovered = SnapshotCoversHitType(summary, HitType.Wall);
        bool ceilingCovered = SnapshotCoversHitType(summary, HitType.Ceiling);

        if (settings?.Penalties != null)
        {
            if (!wallCovered)
                total += entry.WallCount * settings.Penalties.GetCost(PenaltyType.Wall);
            if (!ceilingCovered)
                total += entry.CeilingCount * settings.Penalties.GetCost(PenaltyType.Ceiling);
            total += entry.AllMissCount * settings.Penalties.GetCost(PenaltyType.AllMiss);
            total += entry.ThreeOnesCount * settings.Penalties.GetCost(PenaltyType.ThreeOnes);
            total += entry.TripleDigitCount * settings.Penalties.GetCost(PenaltyType.Schnapszahl);
            total += entry.LostGameCount * settings.Penalties.GetCost(PenaltyType.LostGame);
        }

        if (entry.CustomPenaltyCounts != null && summary?.CustomPenaltySnapshot != null)
        {
            var byId = new Dictionary<string, CustomPenaltyDef>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var def in summary.CustomPenaltySnapshot)
            {
                if (def != null && !string.IsNullOrEmpty(def.id) && !byId.ContainsKey(def.id))
                    byId[def.id] = def;
            }

            foreach (var kvp in entry.CustomPenaltyCounts)
            {
                if (kvp.Value <= 0)
                    continue;
                if (byId.TryGetValue(kvp.Key, out var def) && def != null)
                    total += kvp.Value * def.cost;
            }
        }

        return total;
    }

    /// <summary>
    /// True, wenn der Snapshot ein aktiviertes Sofort-Custom für diesen
    /// Fehlwurf enthält (dann läuft alles über das Custom, Legacy schweigt).
    /// </summary>
    public static bool SnapshotCoversHitType(GameSummary summary, HitType hitType)
    {
        var snapshot = summary?.CustomPenaltySnapshot;
        if (snapshot == null)
            return false;

        foreach (var def in snapshot)
        {
            if (def == null || !def.enabled || def.condition == null)
                continue;
            if (def.condition.kind == PenaltyConditionKind.HitTypeMiss
                && def.condition.hitType == hitType)
                return true;
        }

        return false;
    }

    /// <summary>Eine Custom-Zeile mit aufgelöstem Namen (für Popups/Tabellen).</summary>
    public struct CustomCountEntry
    {
        public string Id;
        public string Name;
        public int Cost;
        public int Count;
    }

    /// <summary>
    /// Custom-Counts eines Entries mit Snapshot-Namen, sortiert nach
    /// Zeilensumme ( relevanteste zuerst), dann Name. Nur Counts > 0.
    /// Unbekannte IDs erscheinen mit ID als Name und Kosten 0.
    /// </summary>
    public static List<CustomCountEntry> OrderedCustomEntries(GameSummary summary, PlayerSummaryEntry entry)
    {
        var result = new List<CustomCountEntry>();
        if (entry?.CustomPenaltyCounts == null)
            return result;

        foreach (var kvp in entry.CustomPenaltyCounts)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            var def = summary?.CustomPenaltySnapshot?.Find(d => d != null && d.id == kvp.Key);
            result.Add(new CustomCountEntry
            {
                Id = kvp.Key,
                Name = def?.name ?? kvp.Key,
                Cost = def?.cost ?? 0,
                Count = kvp.Value
            });
        }

        result.Sort((a, b) =>
        {
            int cmp = (b.Count * b.Cost).CompareTo(a.Count * a.Cost);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    public class CustomPenaltyDisplayEntry
    {
        public string Id;
        public string Name;
        public int Cost;
        public int Count;
        public string TriggerText;
        public bool EnabledAtSave;
    }

    /// <summary>
    /// Löst die Custom-Counts eines Spielers gegen den Spiel-Snapshot auf.
    /// Ausschließlich Snapshot-Daten (delete-/rename-sicher); unbekannte IDs
    /// erscheinen mit ID als Name und Trigger "-".
    /// </summary>
    public static List<CustomPenaltyDisplayEntry> ResolveCustomEntries(GameSummary summary, System.Guid playerId)
    {
        var result = new List<CustomPenaltyDisplayEntry>();
        if (summary == null)
            return result;

        var entry = summary.PlayerEntries?.Find(e => e.PlayerId == playerId);
        if (entry?.CustomPenaltyCounts == null)
            return result;

        var snapshot = summary.CustomPenaltySnapshot;
        foreach (var kvp in entry.CustomPenaltyCounts)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            CustomPenaltyDef def = null;
            if (snapshot != null)
                def = snapshot.Find(d => d != null && d.id == kvp.Key);

            result.Add(new CustomPenaltyDisplayEntry
            {
                Id = kvp.Key,
                Name = def?.name ?? kvp.Key,
                Cost = def?.cost ?? 0,
                Count = kvp.Value,
                TriggerText = def?.condition != null ? SummarizeCondition(def.condition) : "-",
                EnabledAtSave = def?.enabled ?? false
            });
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>
    /// Alle im Spiel aktiven Custom-Defs (für Snapshot-Tabellen wie GameDetail).
    /// </summary>
    public static List<CustomPenaltyDisplayEntry> ResolveSnapshotDefs(GameSummary summary)
    {
        var result = new List<CustomPenaltyDisplayEntry>();
        if (summary?.CustomPenaltySnapshot == null)
            return result;

        foreach (var def in summary.CustomPenaltySnapshot)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            result.Add(new CustomPenaltyDisplayEntry
            {
                Id = def.id,
                Name = def.name,
                Cost = def.cost,
                Count = 0,
                TriggerText = def.condition != null ? SummarizeCondition(def.condition) : "-",
                EnabledAtSave = def.enabled
            });
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }
}
