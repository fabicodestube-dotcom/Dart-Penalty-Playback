using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq.Expressions;

[Serializable]
public abstract class Game
{
    // =========================================================
    // CORE STATE (SERIALIZED)
    // =========================================================

    [JsonProperty] protected List<Set> sets = new();
    [JsonProperty] protected GameSettings settings;
    [JsonProperty] protected Guid id;

    [JsonProperty] protected List<Guid> playerIDs;
    [JsonProperty] protected int currentPlayerIndex;
    [JsonProperty] protected int startingPlayerIndex;

    [JsonProperty] protected DateTime createdAt;
    [JsonProperty] protected DateTime lastActivityAt;
    [JsonProperty] protected DateTime? finishedAt;


    // =========================================================
    // EVENTS (NOT SERIALIZED)
    // =========================================================

    [JsonIgnore] public Action<Guid> OnMatchWon;
    [JsonIgnore] public Action<Guid> OnBotCheck;
    [JsonIgnore] public Action<Guid> OnLegWon;
    [JsonIgnore] public Action<Guid?> OnSetWon;
    [JsonIgnore] public Action<Guid, Turn> OnTurnCompleted;

    /// <summary>Custom-Penalty ausgelöst (Audio + Anzeige; Zähler/Kosten folgen separat).</summary>
    [JsonIgnore] public Action<string, Guid, Turn> OnCustomPenaltyTriggered;

    /// <summary>Custom-Audio-Event ausgelöst (nur Abspielen).</summary>
    [JsonIgnore] public Action<string> OnCustomAudioEvent;

    /// <summary>Sektor-Eingabe (Wall/Ceiling/Custom) für den Sektor-Sound.</summary>
    [JsonIgnore] public Action<Guid, string> OnSectorHit;


    // =========================================================
    // ABSTRACT CONTRACT
    // =========================================================

    [JsonProperty] protected GameMode gameMode;


    // =========================================================
    // PLAYER STATS
    // =========================================================

    [JsonProperty] protected Dictionary<Guid, GameStats> playerStats;

    [JsonProperty] protected Guid? matchWinnerId;


    // =========================================================
    // BASIC ACCESSORS (READ-ONLY STATE)
    // =========================================================

    public Guid GetID() => id;
    public GameMode GetGameMode()
    {
        return gameMode;
    }
    public List<Guid> GetPlayerIDs() => playerIDs;
    public List<Set> GetSets() => sets;
    public GameSettings GetSettings()
    {
        return settings.Clone();
    }

    public Guid GetCurrentPlayerId() => playerIDs[currentPlayerIndex];

    public bool IsFinished() => finishedAt.HasValue;
    public DateTime? GetFinishedAt() => finishedAt;
    public Guid? GetMatchWinnerId() => matchWinnerId;

    public DateTime GetCreatedAt() => createdAt;
    public DateTime GetLastActivityAt() => lastActivityAt;

    /// <summary>
    /// Sorting timestamp: finishedAt > lastActivityAt
    /// </summary>
    public DateTime GetSortTimestamp() => finishedAt ?? lastActivityAt;

    public void SetCreatedAt(DateTime createdAt) => this.createdAt = createdAt;
    public void SetLastActivityAt(DateTime lastActivityAt) => this.lastActivityAt = lastActivityAt;
    public void SetFinishedAt(DateTime? finishedAt) => this.finishedAt = finishedAt;


    // =========================================================
    // LIFECYCLE & TIMESTAMPS
    // =========================================================

    public virtual void InitializeAfterLoad()
    {
        // Wenn das Jahr kleiner als 2000 ist, wurde es nicht korrekt geladen/gesetzt
        if (createdAt.Year < 2000)
            createdAt = finishedAt ?? DateTime.Now;

        if (lastActivityAt.Year < 2000)
            lastActivityAt = finishedAt ?? createdAt;

        // Normales deutsches Format für leichtere Lesbarkeit im Log
        Debug.Log("Created at: " + createdAt.ToString("dd.MM.yyyy HH:mm:ss") + 
                ", Last activity at: " + lastActivityAt.ToString("dd.MM.yyyy HH:mm:ss"));
    }


    protected void InitializeTimestampsOnCreate()
    {
        createdAt = DateTime.Now;
        lastActivityAt = createdAt;
    }

    protected void TouchActivity()
    {
        lastActivityAt = DateTime.Now;
    }

    public void MarkSavedNow()
    {
        lastActivityAt = DateTime.Now;
    }


    // =========================================================
    // PLAYER ROTATION
    // =========================================================

    protected void IdentifyNextLegStarter()
    {
        // 1. Berechne, welcher Spieler dieses Set anfangen durfte
        // sets.Count ist die Anzahl der derzeitigen Sets. Der Index des aktuellen Sets ist sets.Count-1 (0-basiert).
        int setIndex = (sets.Count - 1 + playerIDs.Count) % playerIDs.Count; // sicherstellen, dass nicht negativ
        int setStarter = setIndex % playerIDs.Count;

        // 2. Berechne ausgehend vom Set-Starter, wer das aktuelle Leg anfängt
        // Die Legs-Liste enthält das aktuelle Leg als letztes Element, daher ist der 0-basierte Leg-Index legs.Count-1.
        int legIndex = Math.Max(0, sets[^1].GetLegs().Count - 1);

        startingPlayerIndex = (setStarter + legIndex) % playerIDs.Count;
    }

    protected void RotateStartingPlayer()
    {
        IdentifyNextLegStarter();
        // Bei Bedarf könnte hier zusätzlich hochgezählt werden, je nach Aufrufsemantik.
        //startingPlayerIndex = (startingPlayerIndex + 1) % playerIDs.Count;
    }

    protected void ResetToStartingPlayer()
    {
        currentPlayerIndex = startingPlayerIndex;
    }

    /// <summary>
    /// Zentrale Methode, um den Bot-Check auszulösen. Subklassen sollten
    /// diese Methode verwenden anstatt direkt das Event zu feuern.
    /// </summary>
    protected void TriggerBotCheck()
    {
        OnBotCheck?.Invoke(GetCurrentPlayerId());
    }


    // =========================================================
    // CURRENT CONTEXT HELPERS
    // =========================================================

    protected Leg GetCurrentLeg() => sets[^1].GetCurrentLeg();

    public Turn GetCurrentTurnOfPlayer(Guid playerId)
    {
        var turns = GetCurrentLeg().GetTurns();

        for (int i = turns.Count - 1; i >= 0; i--)
            if (turns[i].PlayerId == playerId)
                return turns[i];

        return null;
    }

    public Turn GetLastTurnForPlayer(Guid playerId)
    {
        foreach (var set in sets.AsEnumerable().Reverse())
        foreach (var leg in set.GetLegs().AsEnumerable().Reverse())
        {
            var turns = leg.GetTurns()
                .Where(t => t.PlayerId == playerId)
                .ToList();

            if (turns.Count > 0)
            {
                var turn = turns[^1];

                return turn;
            }
        }

        return null;
    }


    // =========================================================
    // CUSTOMIZATION SNAPSHOT (PER-GAME)
    // =========================================================

    /// <summary>
    /// Custom-Penalty-Defs zum Spielstart (alle nicht-built-in, inkl.
    /// enabled-Stand als Deep-Copy). Die Auswertung nutzt ausschließlich
    /// diesen Snapshot: Mid-Game-Toggles beeinflussen laufende Spiele
    /// nicht, und die History kann rückverfolgen, was aktiv war.
    /// Persistenz: game_meta (siehe Save/RestoreRuntimeGameMeta).
    /// </summary>
    protected List<CustomPenaltyDef> penaltySnapshot;

    /// <summary>Audio-Event-Defs zum Spielstart (analog, inkl. enabled).</summary>
    protected List<AudioEventDef> audioEventSnapshot;

    /// <summary>
    /// Friert die Customization zum Spielstart ein. Aus den Konstruktoren
    /// aller Modi aufrufen (nach playerIDs/settings).
    /// </summary>
    protected void CaptureCustomizationSnapshot()
    {
        penaltySnapshot = ClonePenaltyDefs(CustomizationStore.GetPenalties());
        audioEventSnapshot = CloneAudioEventDefs(CustomizationStore.GetAudioEvents());
    }

    private static List<CustomPenaltyDef> ClonePenaltyDefs(List<CustomPenaltyDef> source)
    {
        var result = new List<CustomPenaltyDef>();
        if (source == null)
            return result;

        foreach (var def in source)
        {
            if (def == null)
                continue;

            try
            {
                var clone = JsonConvert.DeserializeObject<CustomPenaltyDef>(
                    JsonConvert.SerializeObject(def, Formatting.None));
                if (clone != null && !string.IsNullOrEmpty(clone.id))
                    result.Add(clone);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Game] Penalty-Snapshot fehlerhaft: " + e.Message);
            }
        }

        return result;
    }

    private static List<AudioEventDef> CloneAudioEventDefs(List<AudioEventDef> source)
    {
        var result = new List<AudioEventDef>();
        if (source == null)
            return result;

        foreach (var def in source)
        {
            if (def == null)
                continue;

            try
            {
                var clone = JsonConvert.DeserializeObject<AudioEventDef>(
                    JsonConvert.SerializeObject(def, Formatting.None));
                if (clone != null && !string.IsNullOrEmpty(clone.id))
                    result.Add(clone);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Game] AudioEvent-Snapshot fehlerhaft: " + e.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Snapshot-Defs für die Auswertung (Lazy-Fallback für alte Saves ohne
    /// Snapshot: einmalig aus dem Live-Store aufbauen).
    /// </summary>
    protected List<CustomPenaltyDef> GetSnapshotPenalties()
    {
        if (penaltySnapshot == null)
            penaltySnapshot = ClonePenaltyDefs(CustomizationStore.GetPenalties());

        return penaltySnapshot;
    }

    /// <summary>Analog für Audio-Events (Lazy-Fallback für alte Saves).</summary>
    protected List<AudioEventDef> GetSnapshotAudioEvents()
    {
        if (audioEventSnapshot == null)
            audioEventSnapshot = CloneAudioEventDefs(CustomizationStore.GetAudioEvents());

        return audioEventSnapshot;
    }

    /// <summary>Öffentlicher Snapshot-Zugriff (Summary/History/Statistik).</summary>
    public List<CustomPenaltyDef> GetPenaltySnapshot() => new List<CustomPenaltyDef>(GetSnapshotPenalties());

    /// <summary>Öffentlicher AudioEvent-Snapshot (Persistenz/Summary).</summary>
    public List<AudioEventDef> GetAudioEventSnapshot() => new List<AudioEventDef>(GetSnapshotAudioEvents());

    /// <summary>Stellt den Penalty-Snapshot wieder her (Load-Pfad).</summary>
    public void RestorePenaltySnapshot(List<CustomPenaltyDef> snapshot)
    {
        penaltySnapshot = snapshot != null
            ? ClonePenaltyDefs(snapshot)
            : null;
    }

    /// <summary>Stellt den AudioEvent-Snapshot wieder her (Load-Pfad).</summary>
    public void RestoreAudioEventSnapshot(List<AudioEventDef> snapshot)
    {
        audioEventSnapshot = snapshot != null
            ? CloneAudioEventDefs(snapshot)
            : null;
    }

    /// <summary>
    /// Zentraler Aktiv-Guard für Custom-Penalties: enabled + mit Bedingung
    /// werden je ausgewertet/gebucht. builtIn heißt nur „nicht löschbar“.
    /// </summary>
    protected static bool IsPenaltyDefActive(CustomPenaltyDef def)
    {
        return def != null && def.enabled && def.condition != null;
    }

    /// <summary>
    /// Zentraler Aktiv-Guard für Audio-Events (Auto-Trigger + Manuale).
    /// builtIn heißt nur „nicht löschbar“.
    /// </summary>
    protected static bool IsAudioEventActive(AudioEventDef def)
    {
        return def != null && def.enabled && def.trigger != null;
    }

    /// <summary>
    /// Spielt ein manuelles Audio-Event nur, wenn es im Spiel-Snapshot aktiv
    /// und für den aktuellen Modus freigegeben ist. So wirken enabled-/
    /// Kategorie-Einstellungen auch auf die bisher hart verdrahteten Sounds.
    /// </summary>
    public bool TryTriggerManualAudioEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
            return false;

        var defs = GetSnapshotAudioEvents();
        if (defs == null)
            return false;

        AudioEventDef def = null;
        foreach (var candidate in defs)
        {
            if (candidate != null
                && string.Equals(candidate.id, eventId, StringComparison.OrdinalIgnoreCase))
            {
                def = candidate;
                break;
            }
        }

        if (!IsAudioEventActive(def)
            || def.trigger.kind != AudioEventTriggerKind.Manual
            || !IsEventCategoryApplicable(def.category))
        {
            return false;
        }

        OnCustomAudioEvent?.Invoke(def.id);
        return true;
    }

    /// <summary>
    /// Meldet einen Sektor-Wurf (Wall/Ceiling/Custom) für den Sektor-Sound.
    /// Board-Würfe und Würfe ohne Sektor-ID bleiben stumm.
    /// </summary>
    protected void NotifySectorHit(Guid playerId, Throw throwItem)
    {
        string sectorId = CustomHitDef.GetSectorId(throwItem);
        if (string.IsNullOrEmpty(sectorId))
            return;

        OnSectorHit?.Invoke(playerId, sectorId);
    }


    // =========================================================
    // PENALTY SYSTEM (nur Custom; Legacy-Buchung ist stillgelegt)
    // =========================================================

    public float GetPenaltyCost(PenaltyType type)
    {
        return settings != null ? settings.Penalties.GetCost(type) : 0f;
    }

    public float GetTotalPenaltyCost(Guid playerId)
    {
        float sum = 0;

        sum += GetPenaltyCost(PenaltyType.Wall) * playerStats[playerId].wallCount;
        sum += GetPenaltyCost(PenaltyType.Ceiling) * playerStats[playerId].ceilingCount;
        sum += GetPenaltyCost(PenaltyType.AllMiss) * playerStats[playerId].allMissCount;
        sum += GetPenaltyCost(PenaltyType.ThreeOnes) * playerStats[playerId].tripleOnesCount;
        sum += GetPenaltyCost(PenaltyType.LostGame) * playerStats[playerId].lostGame;
        sum += GetPenaltyCost(PenaltyType.Schnapszahl) * playerStats[playerId].tripleDigitCount;
        sum += playerStats[playerId].customPenaltyCost;

        return sum;
    }


    // =========================================================
    // PENALTY RULES (TURN BASED)
    // =========================================================

    protected void CheckTurnPenalties(Guid playerId, Turn turn)
    {
        EvaluateCustomTurnPenalties(playerId, turn);
        EvaluateCustomRemainingPenalties(playerId, turn);
        EvaluateCustomAudioEvents(playerId, turn);
    }

    /// <summary>
    /// Wertet Custom-Penalties (Turn-Ende-Typen) gegen den Turn aus.
    /// Bucht Kosten ins separate Custom-Konto + feuert Audio/Anzeige.
    /// Game-Events (2.3) laufen nicht hier, sondern an Leg-/Set-/Game-Ende.
    /// </summary>
    protected void EvaluateCustomTurnPenalties(Guid playerId, Turn turn, bool silent = false)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null || turn == null)
            return;

        var throws = turn.GetThrows();
        int turnScore = turn.GetTurnScore();

        foreach (var def in defs)
        {
            if (!IsPenaltyDefActive(def))
                continue;

            if (CustomPenaltyDef.IsGameEventKind(def.condition.kind))
                continue;

            if (PenaltyEvaluator.MatchesTurn(def.condition, throws, turnScore))
                AddCustomPenalty(playerId, def, turn, false, silent);
        }
    }

    /// <summary>
    /// Wertet Rest-basierte Custom-Penalties (RemainingExactScore) aus:
    /// X01-Rest bzw. Cricket-Totals (Cutthroat: Gegner-Totals, Buchung an
    /// den Total-Inhaber — wie die frühere Schnapszahl-Regel). Modi ohne
    /// Rest-Begriff (ATC) liefern keine Totals und feuern nie.
    /// Läuft in CheckTurnPenalties NACH den Turn-Penalties und VOR den
    /// Audio-Events (stabile Abspiel-Reihenfolge).
    /// </summary>
    protected void EvaluateCustomRemainingPenalties(Guid playerId, Turn turn, bool silent = false)
    {
        if (turn == null)
            return;

        var defs = GetSnapshotPenalties();
        if (defs == null)
            return;

        var totals = GetRemainingTotals(playerId, turn);
        if (totals == null)
            return;

        foreach (var def in defs)
        {
            if (!IsPenaltyDefActive(def))
                continue;

            if (def.condition.kind != PenaltyConditionKind.RemainingExactScore)
                continue;

            foreach (var kvp in totals)
            {
                if (PenaltyEvaluator.MatchesRemaining(def.condition, kvp.Value.before, kvp.Value.after))
                    AddCustomPenalty(kvp.Key, def, turn, false, silent);
            }
        }
    }

    /// <summary>
    /// (Spieler → vorher/nachher)-Totals für die Remaining-Auswertung.
    /// Base: keine (ATC). X01: eigener Rest; Cricket: selbst/Gegner.
    /// </summary>
    protected virtual Dictionary<Guid, (int before, int after)> GetRemainingTotals(Guid playerId, Turn turn)
    {
        return null;
    }

    /// <summary>Instant-Prüfung eines einzelnen Wurfs (HitTypeMiss-Typ).</summary>
    protected void CheckCustomInstantPenalties(Guid playerId, Throw throwItem, Turn turn, bool silent = false)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null || throwItem == null)
            return;

        foreach (var def in defs)
        {
            if (!IsPenaltyDefActive(def))
                continue;

            if (def.condition.kind != PenaltyConditionKind.HitTypeMiss)
                continue;

            if (PenaltyEvaluator.MatchesInstant(def.condition, throwItem))
                AddCustomPenalty(playerId, def, turn, true, silent);
        }
    }

    /// <summary>
    /// Löst ein Custom-Penalty aus: Kosten ins Custom-Konto buchen, für Undo
    /// im Turn vermerken, Event für Audio/Anzeige. Silent = ohne Event
    /// (Undo-Re-Finalize). Game-Events (turn=null) werden nicht vermerkt.
    /// </summary>
    protected void AddCustomPenalty(Guid playerId, CustomPenaltyDef def, Turn turn, bool instant = false, bool silent = false)
    {
        if (def == null || !def.enabled)
            return;

        if (playerStats.TryGetValue(playerId, out var stats) && stats != null)
            stats.AddCustomPenaltyBooking(def.id, def.cost);

        if (turn != null)
        {
            turn.RecordedPenalties ??= new List<TurnPenaltyRecord>();
            turn.RecordedPenalties.Add(new TurnPenaltyRecord
            {
                PlayerId = playerId,
                CustomPenaltyId = def.id,
                CustomCost = def.cost,
                CustomInstant = instant
            });
        }

        if (!silent)
            OnCustomPenaltyTriggered?.Invoke(def.id, playerId, turn);
    }

    /// <summary>Nimmt eine Custom-Buchung zurück (Undo).</summary>
    protected void RemoveCustomPenalty(Guid playerId, string customPenaltyId, float cost)
    {
        if (string.IsNullOrEmpty(customPenaltyId) || cost <= 0f)
            return;

        if (playerStats.TryGetValue(playerId, out var stats) && stats != null)
        {
            stats.RemoveCustomPenaltyCost(cost);

            if (stats.customPenaltyCounts.TryGetValue(customPenaltyId, out int current))
            {
                if (current <= 1)
                    stats.customPenaltyCounts.Remove(customPenaltyId);
                else
                    stats.customPenaltyCounts[customPenaltyId] = current - 1;
            }
        }
    }

    /// <summary>
    /// Undo-Helfer: alle Custom-Buchungen des Turns stornieren, dann die
    /// Instant-Buchungen der verbleibenden Würfe (alle außer dem letzten,
    /// der gerade entfernt wird) still neu buchen. Turn-Penalties bleiben
    /// storniert (Turn ist nach Undo unvollständig).
    /// </summary>
    protected void RefundAndRefireCustoms(Turn turn)
    {
        if (turn == null)
            return;

        if (turn.RecordedPenalties != null)
        {
            foreach (var rec in turn.RecordedPenalties)
            {
                if (!string.IsNullOrEmpty(rec.CustomPenaltyId))
                    RemoveCustomPenalty(rec.PlayerId, rec.CustomPenaltyId, rec.CustomCost);
            }

            turn.RecordedPenalties.RemoveAll(rec => !string.IsNullOrEmpty(rec.CustomPenaltyId));
        }

        var throws = turn.GetThrows();
        for (int i = 0; i < throws.Count - 1; i++)
            CheckCustomInstantPenalties(turn.PlayerId, throws[i], turn, true);
    }

    /// <summary>
    /// Wertet Custom-Audio-Events (Auto-Trigger, je Modus-Kategorie) aus.
    /// Manuelle Events (Undo, ...) feuern weiter direkt im Code.
    /// </summary>
    protected void EvaluateCustomAudioEvents(Guid playerId, Turn turn)
    {
        var allEvents = GetSnapshotAudioEvents();
        if (allEvents == null || turn == null)
            return;

        var values = new AudioTriggerValues();
        CollectAudioTriggerValues(playerId, turn, values);

        foreach (var def in allEvents)
        {
            if (!IsAudioEventActive(def))
                continue;

            if (!IsEventCategoryApplicable(def.category))
                continue;

            if (AudioEventEngine.Matches(def.trigger, values))
                OnCustomAudioEvent?.Invoke(def.id);
        }
    }

    /// <summary>
    /// Nur Events der eigenen Modus-Kategorie auswerten (Global immer).
    /// Verhindert z.B., dass ein X01-Score-Event bei Cricket-Punkten feuert.
    /// </summary>
    private bool IsEventCategoryApplicable(AudioEventCategory category)
    {
        if (category == AudioEventCategory.Global)
            return true;

        return GetGameMode() switch
        {
            GameMode.X01 => category == AudioEventCategory.X01,
            GameMode.Cricket => category == AudioEventCategory.Cricket,
            GameMode.ATC => category == AudioEventCategory.ATC,
            _ => true
        };
    }

    /// <summary>
    /// Befüllt Trigger-Kennzahlen. Basis: TurnScore + Würfe; Modi ergänzen
    /// Marks/Punkte (Cricket) bzw. Hits/Streak (ATC) per Override.
    /// </summary>
    protected virtual void CollectAudioTriggerValues(Guid playerId, Turn turn, AudioTriggerValues values)
    {
        values.turnScore = turn.GetTurnScore();
        values.throws = turn.GetThrows();
    }

    // =========================================================
    // MATCH FLOW (STATE TRANSITIONS)
    // =========================================================
    protected void HandleLegWon(Guid playerId)
    {
        GetCurrentLeg().SetWinner(playerId);

        EvaluateCustomGameEventPenalties(PenaltyConditionKind.LegLost, playerId);

        OnLegWon?.Invoke(playerId);
    }

    protected void HandleSetWon(Guid playerId)
    {
        sets[^1].SetWinner(playerId);

        EvaluateCustomGameEventPenalties(PenaltyConditionKind.SetLost, playerId);

        OnSetWon?.Invoke(playerId);
    }

    protected void HandleMatchEnd(Guid winnerId)
    {
        finishedAt = DateTime.Now;
        matchWinnerId = winnerId;

        TouchActivity();

        playerStats[winnerId].RegisterGameWon();

        EvaluateCustomGameEventPenalties(PenaltyConditionKind.GameLost, winnerId);

        OnMatchWon?.Invoke(winnerId);
    }

    /// <summary>
    /// Wertet Custom-Game-Events (2.3) aus: Kosten tragen alle Spieler außer
    /// dem Gewinner. Keine Turn-Vermerkung (kein Throw-Undo-Bezug).
    /// </summary>
    protected void EvaluateCustomGameEventPenalties(PenaltyConditionKind kind, Guid winnerId)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null)
            return;

        foreach (var def in defs)
        {
            if (!IsPenaltyDefActive(def))
                continue;

            if (def.condition.kind != kind)
                continue;

            foreach (var pid in playerIDs)
            {
                if (pid == winnerId) continue;

                AddCustomPenalty(pid, def, null);
            }
        }
    }

    /// <summary>Storniert Game-Event-Buchungen einer Art für alle Nicht-Gewinner.
    /// Bewusst OHNE enabled-Filter: Storno muss auch nach Deaktivieren
    /// funktionieren, sonst bleiben Geisterbuchungen zurück.</summary>
    protected void RefundCustomGameEventPenalties(PenaltyConditionKind kind, Guid winnerId)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null)
            return;

        foreach (var def in defs)
        {
            if (def == null || def.condition == null)
                continue;

            if (def.condition.kind != kind)
                continue;

            foreach (var pid in playerIDs)
            {
                if (pid == winnerId) continue;

                RemoveCustomPenalty(pid, def.id, def.cost);
            }
        }
    }
    protected bool IsMatchFinished(Guid? playerId)
    {
        int setsWon = GetWonSets(playerId);

        GameSettings settings = GetSettings();

        int setsRequiredToWin;

        switch (settings.setsAndLegsMode)
        {
            case SetsAndLegs.FirstTo:
                setsRequiredToWin = settings.setCount;
                break;

            case SetsAndLegs.BestOf:
            default:
                setsRequiredToWin = (settings.setCount / 2) + 1;
                break;
        }

        return setsWon >= setsRequiredToWin;
    }


    // =========================================================
    // MATCH / SET / LEG EVALUATION
    // =========================================================

    public int GetWonSets(Guid? playerId)
    {
        int count = 0;

        foreach (var set in sets)
            if (set.WinnerPlayerId == playerId)
                count++;

        return count;
    }

    public int GetWonLegs(Guid? playerId)
    {
        if (sets.Count == 0) return 0;

        var currentSet = sets[^1];
        int count = 0;

        foreach (var leg in currentSet.GetLegs())
            if (GetLegWinner(leg) == playerId)
                count++;

        return count;
    }

    private Guid? GetLegWinner(Leg leg) => leg.WinnerPlayerId;

    public Turn GetLastTurnInCurrentLeg(Guid playerId)
    {
        return GetCurrentLeg()
            .GetTurns()
            .Where(t => t.PlayerId == playerId)
            .LastOrDefault(t => t.GetThrows().Count > 0 || t.IsBust);
    }


    // =========================================================
    // STATISTICS
    // =========================================================

    public int GetDartsInCurrentLeg(Guid playerId)
    {
        return GetCurrentLeg().GetTurns()
            .Where(t => t.PlayerId == playerId)
            .Sum(t => t.GetThrows().Count);
    }

    protected virtual void InitializeStats()
    {
        playerStats.Clear();

        foreach (var pid in playerIDs)
        {
            playerStats[pid] = new GameStats(pid);
        }
    }

    public virtual IReadOnlyDictionary<Guid, GameStats> GetPlayerStats() => playerStats;

    public virtual void CalculatePlayerStatsOnSave()
    {
        StatsResetGameParticipation();

        StatsRegisterGameParticipation();

        foreach (Set set in sets)
        {
            var legs = set.GetLegs();

            StatsRegisterSet(set);

            foreach (Leg leg in legs)
            {
                StatsRegisterLeg(leg);

                var turns = leg.GetTurns();
                foreach (Turn t in turns)
                {
                    StatsRegisterTurn(t);
                }
            }

            Guid? setWinnerID = set.WinnerPlayerId;
            if (setWinnerID.HasValue)
            {
                playerStats[setWinnerID.Value].currentLegCount = 0;
            }
        }

        // Legacy-Penalty-Counter werden NICHT rebuildet: Sie stammen für alte
        // Spiele aus der persistierten Zeile (RestorePersistedLegacyPenalties),
        // für neue Spiele bleiben sie 0. Customs dito (Turn-Records + Zeile).
        // Keine Events hier (still, sonst Audio-/UI-Spam bei Load/Save).
    }

    protected bool IsEmpty()
    {
        // Es darf nur ein Set und ein Leg existieren
        if (sets.Count != 1)
            return false;

        var legs = sets[0].GetLegs();

        if (legs.Count != 1)
            return false;

        // Im ersten Leg darf es keinerlei Aktion geben
        return legs[0].GetTurns().All(t => t.GetThrows().Count == 0 && !t.IsBust);
    }

    private void StatsResetGameParticipation()
    {
        foreach (Guid playerGUID in playerIDs)
        {
            playerStats[playerGUID].ResetForStatCalculation();
            playerStats[playerGUID].gamesWon = 0;
        }
    }

    private void StatsRegisterGameParticipation()
    {
        if (!finishedAt.HasValue)
            return;

        foreach (Guid playerGUID in playerIDs)
        {
            playerStats[playerGUID].gameCount = 1;

            if (matchWinnerId.HasValue && matchWinnerId.Value != Guid.Empty && playerGUID == matchWinnerId.Value)
            {
                playerStats[playerGUID].RegisterGameWon();
            }
        }
    }

    private void StatsRegisterSet(Set s)
    {
        if (s == null)
            return;
        
        foreach (Guid id in playerIDs)
        {
            playerStats[id].RegisterSetPlayed();

            if (id == s.WinnerPlayerId)
            {
                playerStats[id].RegisterSetWon();
            }
        }
    }

    private void StatsRegisterLeg(Leg l)
    {
        if (l == null)
            return;
        
        foreach (Guid id in playerIDs)
        {
            playerStats[id].RegisterLegPlayed();

            if (id == l.WinnerPlayerId)
            {
                playerStats[id].RegisterLegWon();
            }
        }
    }

    private void StatsRegisterTurn(Turn turn)
    {
        if (turn == null)
            return;

        Guid playerID = turn.PlayerId;
        
        foreach (Throw t in turn.GetThrows())
        {
            playerStats[playerID].AddThrow(t);
        }
    }

    public virtual UndoResult UndoBase()
    {
        var result = new UndoResult();
        var currentSet = sets[^1];
        var currentLeg = currentSet.GetCurrentLeg();

        // 1. Match-Status zurücksetzen
        if (finishedAt.HasValue)
        {
            result.LegWinnerUndone = true;

            Guid? exWinner = matchWinnerId;

            if (matchWinnerId.HasValue)
            {
                matchWinnerId = null;
            }

            // Custom GameLost-Buchungen des beendeten Matches stornieren.
            // (Legacy-LostGame ist stillgelegt; alte Spiele behalten ihre
            // Counts aus der persistierten Zeile.)
            if (exWinner.HasValue)
                RefundCustomGameEventPenalties(PenaltyConditionKind.GameLost, exWinner.Value);

            finishedAt = null;
            currentSet.ClearWinner();
            currentLeg.ClearWinner();
        }

        // Prüfen, ob wir am Anfang eines leeren Legs stehen (was gerade durch einen Sieg erzeugt wurde)
        bool legHasNoAction = currentLeg.GetTurns().All(t => t.GetThrows().Count == 0);

        if (legHasNoAction)
        {
            if (currentSet.GetLegs().Count > 1)
            {
                // Leg innerhalb des Sets entfernen
                currentSet.GetLegs().RemoveAt(currentSet.GetLegs().Count - 1);
                result.LegWinnerUndone = true;
            }
            else if (sets.Count > 1)
            {
                sets.RemoveAt(sets.Count - 1);
                currentSet = sets[^1];
                currentSet.ClearWinner();
                result.SetWinnerUndone = true;
                result.LegWinnerUndone = true;
            }

            if (result.LegWinnerUndone)
            {
                Debug.Log("if zwei");
                currentLeg = currentSet.GetCurrentLeg();
                result.UndoneLeg = currentLeg;
                result.PreviousWinnerId = currentLeg.WinnerPlayerId;

                currentLeg.ClearWinner();
                // Start-Rotation neu berechnen (sicherer als manuelles Dekrement)
                IdentifyNextLegStarter();

                // Custom LegLost-Buchungen des rückgängig gemachten Legs stornieren.
                // (Set/Game-Storni sind nicht abgedeckt: Set-Win-Undo ist final.)
                if (result.PreviousWinnerId != Guid.Empty)
                    RefundCustomGameEventPenalties(PenaltyConditionKind.LegLost, result.PreviousWinnerId);
            }
        }

        // Den letzten relevanten Turn finden, um currentPlayerIndex zu korrigieren
        Turn lastTurn = currentLeg.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0 || t.IsBust);
        if (lastTurn != null)
        {
            currentPlayerIndex = playerIDs.IndexOf(lastTurn.PlayerId);
            RemoveBasePenalties(lastTurn);
        }

        TouchActivity();
        TriggerBotCheck();

        return result;
    }


    private void RemoveBasePenalties(Turn t)
    {
        // Nur Custom-Penalties: Buchungen stornieren, Instant-Buchungen
        // der verbleibenden Würfe still neu buchen (der letzte Wurf fliegt
        // gleich). Legacy-Pfade sind stillgelegt.
        RefundAndRefireCustoms(t);
    }
}



