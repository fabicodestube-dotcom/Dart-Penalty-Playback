using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// SQLite-gestützter Speicher für Custom-Penalties und Audio-Events
/// (Customization-Screens). Cached im RAM, Write-Through bei Save/Delete,
/// Changed-Event für UI-Refresh. Seeds werden beim ersten Start angelegt:
/// Penalties und freie Sektoren sind löschbar (builtIn=false). Wall/Ceiling
/// sowie die meisten Audio-Seeds sind nicht löschbar (builtIn=true),
/// einzelne user-ähnliche Seeds werden einmalig im Custom-Paket angelegt.
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

    /// <summary>True, wenn die Sektor-ID eingebaut oder im Store vorhanden ist.</summary>
    public static bool IsKnownHitSector(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return false;

        if (CustomHitDef.IsBuiltInSector(sectorId))
            return true;

        EnsureLoaded();

        foreach (var def in hitSectors)
        {
            if (def != null && string.Equals(def.id, sectorId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
            if (PenaltyReferencesSector(def, sectorId))
                count++;
        }

        return count;
    }

    private static bool PenaltyReferencesSector(CustomPenaltyDef def, string sectorId)
    {
        if (def?.conditions == null || def.conditions.Count == 0 || string.IsNullOrEmpty(sectorId))
            return false;

        // Instant-Penalty (HitTypeMiss) auf genau diesen Custom-Sektor.
        foreach (var c in def.conditions)
        {
            if (c == null)
                continue;

            if (c.kind == PenaltyConditionKind.HitTypeMiss
                && c.hitType == HitType.Custom
                && string.Equals(c.customHitId, sectorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (c.pattern == null)
                continue;

            foreach (var slot in c.pattern)
            {
                if (slot != null && !slot.anyHitType
                    && slot.hitType == HitType.Custom
                    && string.Equals(slot.customHitId, sectorId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
            if (def != null && PenaltyReferencesSector(def, sectorId))
                ids.Add(def.id);
        }

        return ids;
    }

    /// <summary>Alle Penalties, die den Sektor referenzieren.</summary>
    public static List<CustomPenaltyDef> GetPenaltiesUsingSector(string sectorId)
    {
        var result = new List<CustomPenaltyDef>();

        if (string.IsNullOrEmpty(sectorId))
            return result;

        EnsureLoaded();

        foreach (var def in penalties)
        {
            if (def != null && PenaltyReferencesSector(def, sectorId))
                result.Add(def);
        }

        return result;
    }

    /// <summary>Anzeigenamen aller Penalties, die den Sektor referenzieren.</summary>
    public static List<string> GetPenaltyNamesUsingSector(string sectorId)
    {
        var names = new List<string>();

        foreach (var def in GetPenaltiesUsingSector(sectorId))
        {
            if (def == null)
                continue;

            names.Add(!string.IsNullOrWhiteSpace(def.name) ? def.name.Trim() : def.id);
        }

        return names;
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

    /// <summary>Analog für Audio-Events (Tab 3).</summary>
    public static bool IsAudioEventNameTaken(string name, string excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        EnsureLoaded();

        string cleaned = name.Trim();
        foreach (var def in audioEvents)
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

        def.info = CustomizationInfo.BuildPenalty(def);
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
            AudioLibrary.Instance?.DeleteAllCustomClips(id);

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

        if (CustomHitDef.IsBuiltInSector(def.id))
            def.builtIn = true;

        if (IsHitSectorNameTaken(def.name, def.id))
            return false;

        def.info = CustomizationInfo.BuildSector(def);
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
        if (string.IsNullOrEmpty(id) || CustomHitDef.IsBuiltInSector(id))
            return false;

        EnsureLoaded();
        var existing = hitSectors.Find(x => x != null && x.id == id);
        if (existing != null && existing.builtIn)
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

    /// <summary>
    /// Löscht einen Custom-Sektor zusammen mit allen Penalties, die ihn
    /// referenzieren, und deren aufgenommenen Audio-Clips.
    /// </summary>
    public static bool DeleteHitSectorWithPenalties(string id)
    {
        if (string.IsNullOrEmpty(id) || CustomHitDef.IsBuiltInSector(id))
            return false;

        EnsureLoaded();

        var existing = hitSectors.Find(x => x != null && x.id == id);
        if (existing == null || existing.builtIn)
            return false;

        foreach (var penaltyId in GetPenaltyIdsUsingSector(id))
            DeletePenalty(penaltyId);

        // Clips der Area selbst (Recorder in der Sektor-Zeile).
        AudioLibrary.Instance?.DeleteAllCustomClips(id);

        return DeleteHitSector(id);
    }

    /// <summary>False, wenn der Name bereits vergeben ist (kein Speichern).</summary>
    public static bool SaveAudioEvent(AudioEventDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return false;

        if (IsAudioEventNameTaken(def.name, def.id))
            return false;

        def.info = CustomizationInfo.BuildAudioEvent(def);
        SqliteDatabaseRepository.SaveAudioEvent(def);

        EnsureLoaded();
        int index = audioEvents.FindIndex(x => x.id == def.id);
        if (index >= 0)
            audioEvents[index] = def;
        else
            audioEvents.Add(def);

        Changed?.Invoke(CustomizationChangeKind.AudioEvents);
        return true;
    }

    public static bool DeleteAudioEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        bool removed = SqliteDatabaseRepository.DeleteAudioEvent(id);
        if (removed)
        {
            AudioLibrary.Instance?.DeleteAllCustomClips(id);

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
        Migrate420ToGlobal();
        MigrateScoreSeedsToRemaining();
        BackfillMissingInfo();
        MigrateBuiltInHitSectors();
    }

    /// <summary>
    /// Fills empty info texts for entries loaded from older databases.
    /// Seeds/new saves already set info; this only heals legacy rows.
    /// </summary>
    private static void BackfillMissingInfo()
    {
        foreach (var def in penalties)
        {
            if (def == null || !string.IsNullOrEmpty(def.info))
                continue;
            def.info = CustomizationInfo.BuildPenalty(def);
            SqliteDatabaseRepository.SaveCustomPenalty(def);
        }

        foreach (var def in hitSectors)
        {
            if (def == null || !string.IsNullOrEmpty(def.info))
                continue;
            def.info = CustomizationInfo.BuildSector(def);
            SqliteDatabaseRepository.SaveCustomHitSector(def);
        }

        foreach (var def in audioEvents)
        {
            if (def == null || !string.IsNullOrEmpty(def.info))
                continue;
            def.info = CustomizationInfo.BuildAudioEvent(def);
            SqliteDatabaseRepository.SaveAudioEvent(def);
        }
    }

    /// <summary>
    /// Legt fehlende Audio-Event-Seeds an. Built-in-Seeds sind nicht löschbar
    /// (builtIn=true); user-ähnliche Seeds sind löschbar, werden nur im
    /// Custom-Paket einmalig angelegt und über einen app_meta-Marker vor
    /// Wiederanlage geschützt. Ein Changed pro Seed wäre Rebuild-Spam beim
    /// ersten Start, daher ein Sammel-Event am Ende.
    /// </summary>
    private static void SeedMissingAudioEvents()
    {
        bool added = false;

        foreach (var seed in SeededAudioEvents.All)
        {
            if (seed == null || string.IsNullOrEmpty(seed.Id))
                continue;

            if (seed.OnlyCustomAudioSet)
            {
                if (!AudioLibrary.IsCustomSetActive)
                    continue;

                string markerKey = SeededAudioEvents.SeedMarkerKey(seed.Id);
                if (!string.IsNullOrEmpty(SqliteDatabaseRepository.GetAppMeta(markerKey)))
                    continue;

                var existingUserSeed = FindAudioEvent(seed.Id);
                if (existingUserSeed == null)
                {
                    var def = seed.ToDef();
                    def.info = CustomizationInfo.BuildAudioEvent(def);
                    SqliteDatabaseRepository.SaveAudioEvent(def);
                    audioEvents.Add(def);
                    added = true;
                }

                SqliteDatabaseRepository.SetAppMeta(markerKey, "1");
                continue;
            }

            var existing = FindAudioEvent(seed.Id);
            if (existing != null)
            {
                bool changed = false;
                if (seed.BuiltIn && !existing.builtIn)
                {
                    existing.builtIn = true;
                    changed = true;
                }
                if (string.IsNullOrEmpty(existing.info))
                {
                    existing.info = CustomizationInfo.BuildAudioEvent(existing);
                    changed = true;
                }
                if (changed)
                {
                    SqliteDatabaseRepository.SaveAudioEvent(existing);
                    added = true;
                }
                continue;
            }

            var newDef = seed.ToDef();
            newDef.info = CustomizationInfo.BuildAudioEvent(newDef);
            SqliteDatabaseRepository.SaveAudioEvent(newDef);
            audioEvents.Add(newDef);
            added = true;
        }

        if (added)
            Changed?.Invoke(CustomizationChangeKind.AudioEvents);
    }

    private static AudioEventDef FindAudioEvent(string id)
    {
        return audioEvents.Find(x => x != null &&
            string.Equals(x.id, id, System.StringComparison.OrdinalIgnoreCase));
    }

    private static void Migrate420ToGlobal()
    {
        bool changed = false;

        foreach (var def in audioEvents)
        {
            if (def == null || !def.builtIn)
                continue;

            if (!string.Equals(def.id, SeededAudioEvents.Score420Id, StringComparison.OrdinalIgnoreCase))
                continue;

            if (def.category != AudioEventCategory.X01)
                continue;

            if (def.triggers == null || def.triggers.Count == 0
                || def.triggers.FirstOrDefault().kind != AudioEventTriggerKind.ScoreReached
                || def.triggers.FirstOrDefault().score != 420)
                continue;

            def.category = AudioEventCategory.Global;
            SqliteDatabaseRepository.SaveAudioEvent(def);
            changed = true;
        }

        if (changed)
            Changed?.Invoke(CustomizationChangeKind.AudioEvents);
    }

    private static void SeedBuiltInPenalties()
    {
        // Starter-Set (Tab 2): Turn-Patterns (3x1, No board hit), Schnapszahl
        // (Alle Repdigit-Rest-Scores 111–999 als einzelne Seed mit mehreren
        // Bedingungen, Kosten 50), LegLost (Kosten 50) sowie die
        // Sofort-Typen Wall (10) / Ceiling (20), die die Sektor-Kosten tragen.
        // Alle Seeds sind evaluierbar und löschbar (builtIn=false); gelöschte
        // Seeds werden nie wiederhergestellt (Seeding nur bei leerer Tabelle,
        // Erststart).
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
            SeedPenalty("builtin:schnapszahl", "Schnapszahl", 50, PenaltyConditionKind.RemainingExactScore,
                c => { }, multiple: new[]
                {
                    111, 222, 333, 444, 555, 666, 777, 888, 999
                }),
            SeedPenalty("builtin:leglost", "Leg lost", 50, PenaltyConditionKind.LegLost,
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
            if (def?.conditions == null || def.conditions.Count == 0)
                continue;
            if (def.conditions[0].kind != PenaltyConditionKind.ExactScore)
                continue;
            if (string.IsNullOrEmpty(def.id)
                || !def.id.StartsWith("builtin:score", StringComparison.OrdinalIgnoreCase))
                continue;

            def.conditions[0] = new PenaltyCondition
            {
                kind = PenaltyConditionKind.RemainingExactScore,
                score = def.conditions[0].score
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
            if (def == null || !def.enabled || def.conditions == null || def.conditions.Count == 0)
                continue;
            if (def.conditions.All(c => c.kind != PenaltyConditionKind.HitTypeMiss))
                continue;
            if (def.conditions.Any(c => c.hitType == hitType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True, wenn ein AKTIVES Custom-Penalty (Sofort-Typ) diesen Sektor
    /// abdeckt. Für Wall/Ceiling zählen deren HitTypes; freie Sektoren
    /// matchen über customHitId (leer = jeder Custom-Sektor).
    /// </summary>
    public static bool HasActiveInstantPenalty(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return false;

        EnsureLoaded();
        HitType hitType = CustomHitDef.HitTypeForSector(sectorId);

        foreach (var def in penalties)
        {
            if (def == null || !def.enabled || def.conditions == null || def.conditions.Count == 0)
                continue;
            if (def.conditions.All(c => c.kind != PenaltyConditionKind.HitTypeMiss))
                continue;
            foreach (var c in def.conditions)
            {
                if (c == null || c.hitType != hitType)
                    continue;

                if (hitType != HitType.Custom
                    || string.IsNullOrEmpty(c.customHitId)
                    || string.Equals(c.customHitId, sectorId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static CustomPenaltyDef SeedPenalty(string id, string name, int cost,
        PenaltyConditionKind kind, Action<PenaltyCondition> fill, int[] multiple = null)
    {
        var conditions = new List<PenaltyCondition>();

        if (multiple != null)
        {
            foreach (var score in multiple)
            {
                var c = new PenaltyCondition { kind = kind, score = score };
                fill?.Invoke(c);
                conditions.Add(c);
            }
        }
        else
        {
            var condition = new PenaltyCondition { kind = kind };
            fill?.Invoke(condition);
            conditions.Add(condition);
        }

        var def = new CustomPenaltyDef
        {
            id = id,
            name = name,
            enabled = true,
            cost = cost,
            builtIn = false,
            conditions = conditions
        };
        def.info = CustomizationInfo.BuildPenalty(def);
        return def;
    }

    /// <summary>Seed-Bereiche Wand/Decke (built-in, nicht löschbar).</summary>
    private static void SeedBuiltInHitSectors()
    {
        var seeds = new List<CustomHitDef>
        {
            new CustomHitDef { id = CustomHitDef.WallId, name = "Wall", enabled = true, builtIn = true },
            new CustomHitDef { id = CustomHitDef.CeilingId, name = "Ceiling", enabled = true, builtIn = true }
        };

        foreach (var seed in seeds)
        {
            seed.info = CustomizationInfo.BuildSector(seed);
            SqliteDatabaseRepository.SaveCustomHitSector(seed);
            hitSectors.Add(seed);
        }
    }

    private static void MigrateBuiltInHitSectors()
    {
        bool changed = false;

        foreach (var def in hitSectors)
        {
            if (def == null || def.builtIn || !CustomHitDef.IsBuiltInSector(def.id))
                continue;

            def.builtIn = true;
            SqliteDatabaseRepository.SaveCustomHitSector(def);
            changed = true;
        }

        if (changed)
            Changed?.Invoke(CustomizationChangeKind.Sectors);
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
