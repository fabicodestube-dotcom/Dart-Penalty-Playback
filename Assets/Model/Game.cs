using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

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

    /// <summary>Custom-Penalty triggered (audio + display; counter/costs follow separately).</summary>
    [JsonIgnore] public Action<string, Guid, Turn> OnCustomPenaltyTriggered;

    /// <summary>Custom audio event triggered (play only).</summary>
    [JsonIgnore] public Action<string> OnCustomAudioEvent;

    /// <summary>sector input (wall/ceiling/custom) for the sector sound.</summary>
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
        // If the year is less than 2000, it was not loaded/set correctly
        if (createdAt.Year < 2000)
            createdAt = finishedAt ?? DateTime.Now;

        if (lastActivityAt.Year < 2000)
            lastActivityAt = finishedAt ?? createdAt;

        // Normal German format for easier readability in the log
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
        // Calculate which player was allowed to start this set
        // sets.Count is the number of current sets. The index of the current set is sets.Count -1 (0-based).
        int setIndex = (sets.Count - 1 + playerIDs.Count) % playerIDs.Count; // Ensure that it is not negative
        int setStarter = setIndex % playerIDs.Count;

        // Based on the set starter, calculate who starts the current leg
        // The Legs list contains the current Leg as the last element, so the 0-based Leg Index is legs.Count -1.
        int legIndex = Math.Max(0, sets[^1].GetLegs().Count - 1);

        startingPlayerIndex = (setStarter + legIndex) % playerIDs.Count;
    }

    protected void RotateStartingPlayer()
    {
        IdentifyNextLegStarter();
        // If necessary, this could be incremented, depending on the call semantics.
        //startingPlayerIndex = (startingPlayerIndex + 1) % playerIDs.Count;
    }

    protected void ResetToStartingPlayer()
    {
        currentPlayerIndex = startingPlayerIndex;
    }

    /// <summary>
    /// Central method to trigger the bot check. subclasses should
    /// Use this method instead of directly firing the event.
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
    /// Custom penalty defs at game start (all non-built-in, incl.
    /// enabled state as a deep copy).
    /// Mid-game toggles affect running games but not this snapshot,
    /// and history can trace what was active.
    /// Persistence: game_runtime_state (see Save/RestoreRuntimeGameState).
    /// </summary>
    protected List<CustomPenaltyDef> penaltySnapshot;

    /// <summary>Audio event defs for game start (analogous, incl. enabled).</summary>
    protected List<AudioEventDef> audioEventSnapshot;

    /// <summary>Hit sector defs for game start (analogous, incl. enabled/builtIn).</summary>
    protected List<CustomHitDef> hitSectorSnapshot;

    /// <summary>
    /// Freezes customization at game start.
    /// call for all modes (after playerIDs/settings).
    /// </summary>
    protected void CaptureCustomizationSnapshot()
    {
        penaltySnapshot = ClonePenaltyDefs(CustomizationStore.GetPenalties());
        audioEventSnapshot = CloneAudioEventDefs(CustomizationStore.GetAudioEvents());
        hitSectorSnapshot = CloneHitDefs(CustomizationStore.GetHitSectors());
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

    private static List<CustomHitDef> CloneHitDefs(List<CustomHitDef> source)
    {
        var result = new List<CustomHitDef>();
        if (source == null)
            return result;

        foreach (var def in source)
        {
            if (def == null)
                continue;

            try
            {
                var clone = JsonConvert.DeserializeObject<CustomHitDef>(
                    JsonConvert.SerializeObject(def, Formatting.None));
                if (clone != null && !string.IsNullOrEmpty(clone.id))
                    result.Add(clone);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Game] HitSector-Snapshot fehlerhaft: " + e.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Snapshot defs for evaluation (Lazy fallback for old saves without
    /// Snapshot: build uniquely from the live store.
    /// </summary>
    protected List<CustomPenaltyDef> GetSnapshotPenalties()
    {
        if (penaltySnapshot == null)
            penaltySnapshot = ClonePenaltyDefs(CustomizationStore.GetPenalties());

        return penaltySnapshot;
    }

    /// <summary>Analogue for audio events (lazy fallback for old saves).</summary>
    protected List<AudioEventDef> GetSnapshotAudioEvents()
    {
        if (audioEventSnapshot == null)
            audioEventSnapshot = CloneAudioEventDefs(CustomizationStore.GetAudioEvents());

        return audioEventSnapshot;
    }

    /// <summary>Analog for Hit-Sectors (Lazy-Fallback for old saves).</summary>
    protected List<CustomHitDef> GetSnapshotHitSectors()
    {
        if (hitSectorSnapshot == null)
            hitSectorSnapshot = CloneHitDefs(CustomizationStore.GetHitSectors());

        return hitSectorSnapshot;
    }

    /// <summary>Public snapshot access (Summary/History/Statistics).</summary>
    public List<CustomPenaltyDef> GetPenaltySnapshot() => new List<CustomPenaltyDef>(GetSnapshotPenalties());

    /// <summary>Public AudioEvent -Snapshot (Persistence/Summary).</summary>
    public List<AudioEventDef> GetAudioEventSnapshot() => new List<AudioEventDef>(GetSnapshotAudioEvents());

    /// <summary>Public Hit-Sector-Snapshot (Persistence/Gameplay).</summary>
    public List<CustomHitDef> GetHitSectorSnapshot() => new List<CustomHitDef>(GetSnapshotHitSectors());

    /// <summary>Restore the penalty snapshot (load path).</summary>
    public void RestorePenaltySnapshot(List<CustomPenaltyDef> snapshot)
    {
        penaltySnapshot = snapshot != null
            ? ClonePenaltyDefs(snapshot)
            : null;
    }

    /// <summary>Restore the AudioEvent snapshot (load path).</summary>
    public void RestoreAudioEventSnapshot(List<AudioEventDef> snapshot)
    {
        audioEventSnapshot = snapshot != null
            ? CloneAudioEventDefs(snapshot)
            : null;
    }

    /// <summary>Recovers the hit-sector snapshot (load path).</summary>
    public void RestoreHitSectorSnapshot(List<CustomHitDef> snapshot)
    {
        hitSectorSnapshot = snapshot != null
            ? CloneHitDefs(snapshot)
            : null;
    }

    /// <summary>
    /// Central Active Guard for Custom Penalties: enabled + with condition
    /// are always evaluated/booked. builtIn means only "not erasable".
    /// </summary>
    protected static bool IsPenaltyDefActive(CustomPenaltyDef def)
    {
        return def != null && def.enabled && def.conditions != null && def.conditions.Count > 0;
    }

    /// <summary>
    /// Central active guard for audio events (auto-trigger + manual).
    /// builtIn means only "not erasable".
    /// </summary>
    protected static bool IsAudioEventActive(AudioEventDef def)
    {
        return def != null && def.enabled && def.triggers != null && def.triggers.Count > 0;
    }

    /// <summary>
    /// Plays a manual audio event only when it is active in the game snapshot
    /// and is released for the current mode.
    /// Category settings also on the previously hard-wired sounds.
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
            || def.triggers.FirstOrDefault()?.kind != AudioEventTriggerKind.Manual
            || !IsEventCategoryApplicable(def.category))
        {
            return false;
        }

        OnCustomAudioEvent?.Invoke(def.id);
        return true;
    }

    /// <summary>
    /// Reports a sector roll (wall/ceiling/custom) for the sector sound.
    /// Board rolls and rolls without sector ID remain muted.
    /// </summary>
    protected void NotifySectorHit(Guid playerId, Throw throwItem)
    {
        string sectorId = CustomHitDef.GetSectorId(throwItem);
        if (string.IsNullOrEmpty(sectorId))
            return;

        OnSectorHit?.Invoke(playerId, sectorId);
    }


    // =========================================================
    // PENALTY SYSTEM (custom only; legacy booking is decommissioned)
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
    /// Evaluates custom penalties (turn-end types) against the turn.
    /// Books costs into the separate custom account + fires audio/display.
    /// Game events (2.3) do not run here, but at the leg/set/game end.
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

            if (CustomPenaltyDef.IsGameEventKind(def.conditions.FirstOrDefault()?.kind ?? PenaltyConditionKind.HitTypeMiss))
                continue;

            if (PenaltyEvaluator.MatchesAnyTurn(def.conditions, throws, turnScore))
                AddCustomPenalty(playerId, def, turn, false, silent);
        }
    }

    /// <summary>
    /// Evaluates remaining-based custom penalties (RemainingExactScore):
    /// X01 rest or cricket totals (cutthroat: opponent totals, booking to)
    /// Total holder — like the previous repdigit rule.
    /// Rest term (ATC) does not deliver totals and never fire.
    /// Runs in CheckTurnPenalties AFTER the turn-penalties and BEFORE the
    /// Audio events (stable playback order).
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

            if (def.conditions.All(c => c.kind != PenaltyConditionKind.RemainingExactScore))
                continue;

            foreach (var kvp in totals)
            {
                if (PenaltyEvaluator.MatchesAnyRemaining(def.conditions, kvp.Value.before, kvp.Value.after))
                    AddCustomPenalty(kvp.Key, def, turn, false, silent);
            }
        }
    }

    /// <summary>
    /// (Players → before/after) totals for the Remaining evaluation.
    /// Base: none (ATC). X01: own remaining; Cricket: own/opponent.
    /// </summary>
    protected virtual Dictionary<Guid, (int before, int after)> GetRemainingTotals(Guid playerId, Turn turn)
    {
        return null;
    }

    /// <summary>Instant check of a single litter (HitTypeMiss type).</summary>
    protected void CheckCustomInstantPenalties(Guid playerId, Throw throwItem, Turn turn, bool silent = false)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null || throwItem == null)
            return;

        foreach (var def in defs)
        {
            if (!IsPenaltyDefActive(def))
                continue;

            if (def.conditions.All(c => c.kind != PenaltyConditionKind.HitTypeMiss))
                continue;

            if (PenaltyEvaluator.MatchesAnyInstant(def.conditions, throwItem))
                AddCustomPenalty(playerId, def, turn, true, silent);
        }
    }

    /// <summary>
    /// Triggers a custom penalty: book costs into the custom account, for Undo
    /// Turn note, event for audio/display. Silent = without event
    /// (Undo-Re-Finalize). Game events (turn=null) are not recorded.
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

    /// <summary>Takes back a custom booking (Undo).</summary>
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
    /// Undo-Helper: cancel all custom bookings of the turn, then the
    /// Then the scriptures are written, except for the last one.
    /// which is just removed) quietly rebook. Turn-Penalties remain
    /// canceled (turn is incomplete after Undo).
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
    /// Evaluates custom audio events (auto trigger, per mode category).
    /// Manual events (Undo,...) continue to fire directly in the code.
    /// </summary>
    protected void EvaluateCustomAudioEvents(Guid playerId, Turn turn, Predicate<AudioEventDef> filter = null)
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

            if (filter != null && !filter(def))
                continue;

            if (AudioEventEngine.MatchesAny(def.triggers, values))
                OnCustomAudioEvent?.Invoke(def.id);
        }
    }

    /// <summary>
    /// Only evaluate events of your own mode category (always global).
    /// Prevent e.g.. that an X01 score event fires at cricket points.
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
    /// Fills trigger metrics. Base: TurnScore + throws; add modes
    /// Marks/points (cricket) or hits/streak (ATC) per override.
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
    /// Evaluates custom game events (2.3 ): Costs are borne by all players except
    /// No turn-note (no Throw-Undo reference).
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

            if (def.conditions.All(c => c.kind != kind))
                continue;

            foreach (var pid in playerIDs)
            {
                if (pid == winnerId) continue;

                AddCustomPenalty(pid, def, null);
            }
        }
    }

    /// <summary>Cancels game event bookings of one kind for all non-winners.
    /// Consciously WITHOUT enabled filter: Cancellation must also be completed after deactivation
    /// It works, otherwise ghost bookings will be left behind.</summary>
    protected void RefundCustomGameEventPenalties(PenaltyConditionKind kind, Guid winnerId)
    {
        var defs = GetSnapshotPenalties();
        if (defs == null)
            return;

        foreach (var def in defs)
        {
            if (def == null || def.conditions == null || def.conditions.Count == 0)
                continue;

            if (def.conditions.All(c => c.kind != kind))
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
        return GetWonSets(playerId) >= GetRequiredSetsToWin();
    }

    /// <summary>Sets necessary to win the match (FirstTo or BestOf ).</summary>
    protected int GetRequiredSetsToWin()
    {
        GameSettings settings = GetSettings();

        switch (settings.setsAndLegsMode)
        {
            case SetsAndLegs.FirstTo:
                return settings.setCount;

            case SetsAndLegs.BestOf:
            default:
                return (settings.setCount / 2) + 1;
        }
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

    public int GetWonLegsTotal(Guid playerId)
    {
        return sets
            .SelectMany(set => set.GetLegs())
            .Count(leg => leg.WinnerPlayerId == playerId);
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

        // Legacy-Penalty-Counters Are NOT Rebuilt: They Are for Old
        // Games from the persistent line (RestorePersistedLegacyPenalties ),
        // for new games they remain 0. Custom likewise (turn records + line).
        // No events here (quiet, otherwise audio/UI spam at load/save).
    }

    protected bool IsEmpty()
    {
        // Only one set and one leg must exist
        if (sets.Count != 1)
            return false;

        var legs = sets[0].GetLegs();

        if (legs.Count != 1)
            return false;

        // In the first leg there must be no action
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

        // Reset the match status
        if (finishedAt.HasValue)
        {
            result.LegWinnerUndone = true;

            Guid? exWinner = matchWinnerId;

            if (matchWinnerId.HasValue)
            {
                matchWinnerId = null;
            }

            // Cancel custom GameLost bookings of the finished match.
            // (Legacy LostGame is decommissioned; old games keep their
            // counts from the persistent line.
            if (exWinner.HasValue)
                RefundCustomGameEventPenalties(PenaltyConditionKind.GameLost, exWinner.Value);

            finishedAt = null;
            currentSet.ClearWinner();
            currentLeg.ClearWinner();
        }

        // Check if we are at the beginning of an empty leg (which was just created by a victory)
        bool legHasNoAction = currentLeg.GetTurns().All(t => t.GetThrows().Count == 0);

        if (legHasNoAction)
        {
            if (currentSet.GetLegs().Count > 1)
            {
                // Remove the leg within the set
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
                // Recalculate start rotation (more secure than manual decrement)
                IdentifyNextLegStarter();

                // Custom LegLost cancel bookings of the undone leg.
                // (Set/Game cancellations are not covered: Set-Win-Undo is final.)
                if (result.PreviousWinnerId != Guid.Empty)
                    RefundCustomGameEventPenalties(PenaltyConditionKind.LegLost, result.PreviousWinnerId);
            }
        }

        // Finding the Last Relevant Turn to Correct currentPlayerIndex
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
        // Custom penalties only: cancel bookings, instant bookings
        // Then the remaining pitches are quietly rebooked (the last pitch flies).
        // Legacy paths are closed.
        RefundAndRefireCustoms(t);
    }
}



