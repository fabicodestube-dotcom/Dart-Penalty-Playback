using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SQLite-gestützter Speicher für Custom-Penalties und Audio-Events
/// (Customization-Screens). Cached im RAM, Write-Through bei Save/Delete,
/// Changed-Event für UI-Refresh. Seeds werden beim ersten Start angelegt:
/// Penalties/Sektoren löschbar (builtIn=false), Audio-Events nicht löschbar
/// (builtIn=true, aber enabled-/trigger-editierbar und evaluiert).
/// </summary>
/// <summary>
/// Was sich geändert hat (für gezielte UI-Refreshes statt Komplett-Rebuild).
/// </summary>
public enum CustomizationChangeKind
{
    All,
    Penalties,
    Sectors,
    AudioEvents
}

public static class CustomizationStore
{
    /// <summary>
    /// Feuert pro Save/Delete genau einmal, mit der betroffenen Kategorie.
    /// Subscriber bauen nur ihre eigene Liste neu (kein RefreshAll mehr).
    /// </summary>
    public static event Action<CustomizationChangeKind> Changed;

    private static List<CustomPenaltyDef> penalties;
    private static List<AudioEventDef> audioEvents;
    private static List<CustomHitDef> hitSectors;
    private static bool loaded;

    public static List<CustomPenaltyDef> GetPenalties()
    {
        EnsureLoaded();
        return penalties;
    }

    public static List<AudioEventDef> GetAudioEvents()
    {
        EnsureLoaded();
        return audioEvents;
    }

    public static List<CustomHitDef> GetHitSectors()
    {
        EnsureLoaded();
        return hitSectors;
    }

    /// <summary>Zählt, wie viele (Custom-)Penalties den Sektor referenzieren.</summary>
    public static int GetPenaltyUsageCount(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return 0;

        EnsureLoaded();

        int count = 0;
        foreach (var def in penalties)
        {
            if (def?.condition?.pattern == null)
                continue;

            foreach (var slot in def.condition.pattern)
            {
                if (slot != null && !slot.anyHitType
                    && slot.hitType == HitType.Custom
                    && string.Equals(slot.customHitId, sectorId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    /// <summary>Löscht alle Penalties, die den Sektor referenzieren. Gibt die Anzahl zurück.</summary>
    public static int DeletePenaltiesUsingSector(string sectorId)
    {
        var ids = GetPenaltyIdsUsingSector(sectorId);

        foreach (var id in ids)
            DeletePenalty(id);

        return ids.Count;
    }

    /// <summary>IDs aller Penalties, die den Sektor referenzieren.</summary>
    public static List<string> GetPenaltyIdsUsingSector(string sectorId)
    {
        var ids = new List<string>();

        if (string.IsNullOrEmpty(sectorId))
            return ids;

        EnsureLoaded();

        foreach (var def in penalties)
        {
            if (def?.condition?.pattern == null)
                continue;

            foreach (var slot in def.condition.pattern)
            {
                if (slot != null && !slot.anyHitType
                    && slot.hitType == HitType.Custom
                    && string.Equals(slot.customHitId, sectorId, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(def.id);
                    break;
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// True, wenn der Penalty-Name (getrimmt, case-insensitiv) bereits von
    /// einer ANDEREN Def belegt ist. Die eigene ID ist ausgenommen, damit
    /// Editieren ohne Umbenennen funktioniert.
    /// </summary>
    public static bool IsPenaltyNameTaken(string name, string excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        EnsureLoaded();

        string cleaned = name.Trim();
        foreach (var def in penalties)
        {
            if (def == null || string.Equals(def.id, excludeId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(def.name?.Trim(), cleaned, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Analog für Treffer-Bereiche (Tab 1).</summary>
    public static bool IsHitSectorNameTaken(string name, string excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        EnsureLoaded();

        string cleaned = name.Trim();
        foreach (var def in hitSectors)
        {
            if (def == null || string.Equals(def.id, excludeId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(def.name?.Trim(), cleaned, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>False, wenn der Name bereits vergeben ist (kein Speichern).</summary>
    public static bool SavePenalty(CustomPenaltyDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return false;

        if (IsPenaltyNameTaken(def.name, def.id))
            return false;

        SqliteDatabaseRepository.SaveCustomPenalty(def);

        EnsureLoaded();
        int index = penalties.FindIndex(x => x.id == def.id);
        if (index >= 0)
            penalties[index] = def;
        else
            penalties.Add(def);

        Changed?.Invoke(CustomizationChangeKind.Penalties);
        return true;
    }

    public static bool DeletePenalty(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        bool removed = SqliteDatabaseRepository.DeleteCustomPenalty(id);
        if (removed)
        {
            EnsureLoaded();
            penalties.RemoveAll(x => x.id == id);
            Changed?.Invoke(CustomizationChangeKind.Penalties);
        }

        return removed;
    }

    /// <summary>False, wenn der Name bereits vergeben ist (kein Speichern).</summary>
    public static bool SaveHitSector(CustomHitDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return false;

        if (IsHitSectorNameTaken(def.name, def.id))
            return false;

        SqliteDatabaseRepository.SaveCustomHitSector(def);

        EnsureLoaded();
        int index = hitSectors.FindIndex(x => x.id == def.id);
        if (index >= 0)
            hitSectors[index] = def;
        else
            hitSectors.Add(def);

        Changed?.Invoke(CustomizationChangeKind.Sectors);
        return true;
    }

    public static bool DeleteHitSector(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        bool removed = SqliteDatabaseRepository.DeleteCustomHitSector(id);
        if (removed)
        {
            EnsureLoaded();
            hitSectors.RemoveAll(x => x.id == id);
            Changed?.Invoke(CustomizationChangeKind.Sectors);
        }

        return removed;
    }

    public static void SaveAudioEvent(AudioEventDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return;

        SqliteDatabaseRepository.SaveAudioEvent(def);

        EnsureLoaded();
        int index = audioEvents.FindIndex(x => x.id == def.id);
        if (index >= 0)
            audioEvents[index] = def;
        else
            audioEvents.Add(def);

        Changed?.Invoke(CustomizationChangeKind.AudioEvents);
    }

    public static bool DeleteAudioEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        bool removed = SqliteDatabaseRepository.DeleteAudioEvent(id);
        if (removed)
        {
            EnsureLoaded();
            audioEvents.RemoveAll(x => x.id == id);
            Changed?.Invoke(CustomizationChangeKind.AudioEvents);
        }

        return removed;
    }

    public static void Reload()
    {
        loaded = false;
        EnsureLoaded();
        Changed?.Invoke(CustomizationChangeKind.All);
    }

    private static void EnsureLoaded()
    {
        if (loaded && penalties != null && audioEvents != null && hitSectors != null)
            return;

        penalties = SqliteDatabaseRepository.LoadCustomPenalties();
        audioEvents = SqliteDatabaseRepository.LoadAudioEvents();
        hitSectors = SqliteDatabaseRepository.LoadCustomHitSectors();

        // Flag VOR dem Seeden setzen: Seed-Changed-Events können synchron
        // Subscriber-Refreshes auslösen, die GetPenalties() aufrufen — ohne
        // gesetztes Flag würden die Listen dabei neu gebunden (Reentrancy).
        loaded = true;

        if (penalties.Count == 0)
            SeedBuiltInPenalties();

        if (hitSectors.Count == 0)
            SeedBuiltInHitSectors();

        SeedMissingAudioEvents();
        MigrateScoreSeedsToRemaining();
    }

    /// <summary>
    /// Legt fehlende Audio-Event-Seeds an (nicht löschbar, builtIn=true,
    /// aber enabled-/trigger-editierbar und von der Engine ausgewertet).
    /// Nur fehlende IDs — daneben werden Alt-Seeds (builtIn=false) auf
    /// nicht-löschbar hochgestuft. Ein Changed pro Seed wäre Rebuild-Spam
    /// beim ersten Start, daher ein Sammel-Event am Ende.
    /// </summary>
    private static void SeedMissingAudioEvents()
    {
        bool added = false;

        foreach (var seed in SeededAudioEvents.All)
        {
            if (seed == null || string.IsNullOrEmpty(seed.Id))
                continue;

            var existing = audioEvents.Find(x => x != null &&
                string.Equals(x.id, seed.Id, System.StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!existing.builtIn)
                {
                    existing.builtIn = true;
                    SqliteDatabaseRepository.SaveAudioEvent(existing);
                    added = true;
                }
                continue;
            }

            var def = seed.ToDef();
            SqliteDatabaseRepository.SaveAudioEvent(def);
            audioEvents.Add(def);
            added = true;
        }

        if (added)
            Changed?.Invoke(CustomizationChangeKind.AudioEvents);
    }

    private static void SeedBuiltInPenalties()
    {
        // Starter-Set (Tab 2): Turn-Patterns (3x1, No board hit), exakte
        // Rest-Scores (Schnapszahlen 111–999, Kosten je 50), LegLost
        // (Kosten 50) sowie die Sofort-Typen Wall (10) / Ceiling (20), die
        // die Sektor-Kosten tragen. Alle Seeds sind evaluierbar und
        // löschbar (builtIn=false); gelöschte Seeds werden nie
        // wiederhergestellt (Seeding nur bei leerer Tabelle, Erststart).
        var seeds = new List<CustomPenaltyDef>
        {
            SeedPenalty("builtin:threeones", "Three Ones", 100, PenaltyConditionKind.TurnPattern,
                c =>
                {
                    c.matchOrder = false;
                    c.pattern = new List<TurnPatternEntry>
                    {
                        SingleSlot(1), SingleSlot(1), SingleSlot(1)
                    };
                }),
            SeedPenalty("builtin:allmiss", "No board hit", 100, PenaltyConditionKind.TurnPattern,
                c =>
                {
                    c.matchOrder = false;
                    c.pattern = new List<TurnPatternEntry>
                    {
                        MissSlot(), MissSlot(), MissSlot()
                    };
                }),
            SeedPenalty("builtin:score111", "111", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 111),
            SeedPenalty("builtin:score222", "222", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 222),
            SeedPenalty("builtin:score333", "333", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 333),
            SeedPenalty("builtin:score444", "444", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 444),
            SeedPenalty("builtin:score555", "555", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 555),
            SeedPenalty("builtin:score666", "666", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 666),
            SeedPenalty("builtin:score777", "777", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 777),
            SeedPenalty("builtin:score888", "888", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 888),
            SeedPenalty("builtin:score999", "999", 50, PenaltyConditionKind.RemainingExactScore,
                c => c.score = 999),
            SeedPenalty("builtin:leglost", "Leg verloren", 50, PenaltyConditionKind.LegLost,
                c => { }),
            SeedPenalty("custom_wall", "Wall", 10, PenaltyConditionKind.HitTypeMiss,
                c => c.hitType = HitType.Wall),
            SeedPenalty("custom_ceiling", "Ceiling", 20, PenaltyConditionKind.HitTypeMiss,
                c => c.hitType = HitType.Ceiling)
        };

        foreach (var seed in seeds)
        {
            SqliteDatabaseRepository.SaveCustomPenalty(seed);
            penalties.Add(seed);
        }
    }

    /// <summary>
    /// Stellt Seeds builtin:score* (111–999) von Turn- auf Rest-Semantik um
    /// (RemainingExactScore). Name/Kosten/enabled bleiben (User-Werte), nur
    /// die Bedingung wird ersetzt + zurückgespeichert. Idempotent (greift
    /// nur bei kind==ExactScore). User-Customs haben GUID-IDs und sind nie
    /// betroffen. Nur aus EnsureLoaded aufrufen.
    /// </summary>
    private static void MigrateScoreSeedsToRemaining()
    {
        bool changed = false;

        foreach (var def in penalties)
        {
            if (def?.condition == null)
                continue;
            if (def.condition.kind != PenaltyConditionKind.ExactScore)
                continue;
            if (string.IsNullOrEmpty(def.id)
                || !def.id.StartsWith("builtin:score", StringComparison.OrdinalIgnoreCase))
                continue;

            def.condition = new PenaltyCondition
            {
                kind = PenaltyConditionKind.RemainingExactScore,
                score = def.condition.score
            };

            SqliteDatabaseRepository.SaveCustomPenalty(def);
            changed = true;
        }

        if (changed)
            Changed?.Invoke(CustomizationChangeKind.Penalties);
    }

    /// <summary>
    /// True, wenn ein AKTIVES Custom-Penalty (Sofort-Typ) diesen Fehlwurf
    /// abdeckt. Läuft über den enabled-Stand im Store (reine Anzeige-Hilfe
    /// für gedimmte Input-Buttons).
    /// </summary>
    public static bool HasActiveInstantPenalty(HitType hitType)
    {
        if (hitType != HitType.Wall && hitType != HitType.Ceiling)
            return false;

        EnsureLoaded();

        foreach (var def in penalties)
        {
            if (def == null || !def.enabled || def.condition == null)
                continue;
            if (def.condition.kind != PenaltyConditionKind.HitTypeMiss)
                continue;
            if (def.condition.hitType == hitType)
                return true;
        }

        return false;
    }

    private static CustomPenaltyDef SeedPenalty(string id, string name, int cost,
        PenaltyConditionKind kind, Action<PenaltyCondition> fill)
    {
        var condition = new PenaltyCondition { kind = kind };
        fill?.Invoke(condition);

        return new CustomPenaltyDef
        {
            id = id,
            name = name,
            enabled = true,
            cost = cost,
            builtIn = false,
            condition = condition
        };
    }

    /// <summary>Seed-Bereiche Wand/Decke (löschbar, Enabled editierbar).</summary>
    private static void SeedBuiltInHitSectors()
    {
        var seeds = new List<CustomHitDef>
        {
            new CustomHitDef { id = "builtin:wall", name = "Wall", enabled = true, builtIn = false },
            new CustomHitDef { id = "builtin:ceiling", name = "Ceiling", enabled = true, builtIn = false }
        };

        foreach (var seed in seeds)
        {
            SqliteDatabaseRepository.SaveCustomHitSector(seed);
            hitSectors.Add(seed);
        }
    }

    private static TurnPatternEntry MissSlot()
    {
        return new TurnPatternEntry
        {
            anyValue = true,
            anyMultiplier = true,
            anyHitType = false,
            anyMissHitType = true
        };
    }

    private static TurnPatternEntry SingleSlot(int value)
    {
        return new TurnPatternEntry
        {
            anyValue = false,
            value = value,
            anyMultiplier = true,
            anyHitType = false,
            hitType = HitType.Board
        };
    }
}
