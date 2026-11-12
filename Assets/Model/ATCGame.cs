using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class ATCGame : Game
{
    // =========================================================
    // STATE
    // =========================================================

    [JsonProperty] private Turn currentTurn;

    [JsonProperty] private List<int> targets = new List<int>();

    /// <summary>
    /// Required multiplier per step (parallel to targets).
    /// Combo modes repeat numbers (e.g. 1S,1D,2S,2D...).
    /// Old saves without this list are derived from targetType.
    /// </summary>
    [JsonProperty] private List<DartMultiplier> targetMultipliers = new List<DartMultiplier>();
    [JsonProperty] private Dictionary<Guid, int> playerCurrentIndex = new Dictionary<Guid, int>();
    [JsonProperty] private Dictionary<Guid, bool> playerStarted = new Dictionary<Guid, bool>();
    [JsonProperty] private Dictionary<Guid, bool> playerCarryOn = new Dictionary<Guid, bool>();

    [JsonProperty] private int validThrowsInTurn = 0;
    [JsonIgnore] public Action<Guid, int> OnATCTurnCompleted;
    [JsonIgnore] public Action<Guid> OnStreakStarted;

    [JsonProperty] private Dictionary<Guid, int> lastVisitHits = new();

    // Dartboard order clockwise (20 → 1 → 18 → 4 →...)
    private static readonly int[] DartboardOrderRight = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };
    // Dartboard order in an anticlockwise direction (20 → 5 → 12 →...)
    private static readonly int[] DartboardOrderLeft = { 20, 5, 12, 9, 14, 11, 8, 16, 7, 19, 3, 17, 2, 15, 10, 6, 13, 4, 18, 1 };

    public ATCGameSettings GetSettingsAsATC() => settings as ATCGameSettings;

    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public ATCGame(Guid id, ATCGameSettings settings, List<Guid> playerIDs)
    {
        gameMode = GameMode.ATC;
        InitializeTimestampsOnCreate();
        
        this.id = id;
        this.settings = settings;
        this.playerIDs = playerIDs;

        InitializeStats();
        //InitializeLegsPlayedAtStart();

        // Initialize targets based on order
        if (targets == null || targets.Count == 0)
        {
            InitializeTargets();
        }

        // Old saves only know targets (without repetitions/multipliers).
        EnsureTargetMultipliers();

        foreach (var pid in playerIDs)
        {
            playerCurrentIndex[pid] = 0;
            playerStarted[pid] = false;
            playerCarryOn[pid] = false;
        }

        var set = new Set();
        var leg = new Leg();

        set.AddLeg(leg);
        sets.Add(set);

        startingPlayerIndex = 0;
        currentPlayerIndex = startingPlayerIndex;

        currentTurn = new Turn(GetCurrentPlayerId());
        leg.AddTurn(currentTurn);

        CaptureCustomizationSnapshot();
    }

    private void InitializeTargets()
    {
        var s = GetSettingsAsATC();
        bool withBull = s.targetType.IncludesSingles() || s.targetType.IncludesDoubles();
        ExpandSteps(BuildNumberOrder(s, withBull), s, targets, targetMultipliers);
    }

    /// <summary>
    /// Fill in targetMultipliers from targetType if the list is missing or
    /// shorter (old savegames without repetitions/combi modes).
    /// </summary>
    private void EnsureTargetMultipliers()
    {
        if (targetMultipliers != null && targetMultipliers.Count == targets.Count)
            return;

        var fallback = DefaultMultiplierFor(GetSettingsAsATC().targetType);
        targetMultipliers = Enumerable.Repeat(fallback, targets.Count).ToList();
    }

    /// <summary>
    /// Recovers the progress after LoadGameById.
    /// Background: playerCurrentIndex /targets/ targetMultipliers have never been
    /// Persistent — after load, every game ran on fresh singles defaults
    /// (wrong targets after Undo/Continue, e.g. Single20 instead of T20).
    /// With saved lists (new saves) exact, otherwise deterministically new
    /// built + progress of the current leg from the turns replays.
    /// </summary>
    public void RestoreProgressAfterLoad(
        List<int> savedTargets,
        List<DartMultiplier> savedMultipliers,
        Dictionary<Guid, int> savedIndices)
    {
        bool listsOk = savedTargets != null && savedMultipliers != null
            && savedTargets.Count > 0
            && savedTargets.Count == savedMultipliers.Count;

        if (listsOk)
        {
            targets = new List<int>(savedTargets);
            targetMultipliers = new List<DartMultiplier>(savedMultipliers);
        }
        else
        {
            var s = GetSettingsAsATC();
            bool withBull = s.targetType.IncludesSingles() || s.targetType.IncludesDoubles();
            var nums = new List<int>();
            var mults = new List<DartMultiplier>();
            ExpandSteps(BuildNumberOrder(s, withBull, shuffleRandom: false), s, nums, mults);
            targets = nums;
            targetMultipliers = mults;
        }

        if (savedIndices != null)
        {
            foreach (var pid in playerIDs)
            {
                playerCurrentIndex[pid] = savedIndices.TryGetValue(pid, out int idx)
                    ? Math.Max(0, Math.Min(idx, targets.Count))
                    : 0;
            }
        }
        else if (sets != null && sets.Count > 0)
        {
            RebuildATCProgressStateFromTurns(GetCurrentLeg());
        }

        // currentTurn points to a discarded load after relational load
        // Turn (sets have been rebuilt): only then reattach, otherwise leave everything.
        var legNow = sets != null && sets.Count > 0 ? GetCurrentLeg() : null;
        if (legNow != null && (currentTurn == null || !legNow.GetTurns().Contains(currentTurn)))
        {
            Guid cur = GetCurrentPlayerId();
            var lastTurn = legNow.GetLastTurn();
            if (lastTurn != null && lastTurn.PlayerId == cur && lastTurn.HasSpace())
            {
                currentTurn = lastTurn;
            }
            else
            {
                currentTurn = new Turn(cur);
                legNow.AddTurn(currentTurn);
            }
        }
    }

    private static DartMultiplier DefaultMultiplierFor(ATCTargetType targetType)
    {
        return targetType switch
        {
            ATCTargetType.Doubles => DartMultiplier.Double,
            ATCTargetType.Triples => DartMultiplier.Triple,
            _ => DartMultiplier.Single
        };
    }

    /// <summary>Number string per order (bull position as before: Asc end, Desc beginning, Right/Left end, Random mixed).</summary>
    private static List<int> BuildNumberOrder(ATCGameSettings s, bool withBull, bool shuffleRandom = true)
    {
        var baseTargets = Enumerable.Range(1, 20).ToList();

        if (withBull)
        {
            baseTargets.Add(25);
        }

        switch (s.order)
        {
            case ATCOrder.Ascending:
                return baseTargets;

            case ATCOrder.Descending:
                return baseTargets.OrderByDescending(x => x).ToList();

            case ATCOrder.Right:
                var right = DartboardOrderRight.ToList();
                if (withBull)
                    right.Add(25);
                return right;

            case ATCOrder.Left:
                var left = DartboardOrderLeft.ToList();
                if (withBull)
                    left.Add(25);
                return left;

            case ATCOrder.Random:
                if (!shuffleRandom)
                    return baseTargets;

                return baseTargets
                    .OrderBy(x => UnityEngine.Random.value)
                    .ToList();

            default:
                return baseTargets;
        }
    }

    /// <summary>
    /// Expands numbers to steps in block order: first all singles
    /// (1..20 + 25S), then all doubles (D1..D20 + D25), then all triples
    /// (T1..T20, never T25) — only the activated sets, number string per order.
    /// </summary>
    private static void ExpandSteps(List<int> numbers, ATCGameSettings s, List<int> outNumbers, List<DartMultiplier> outMultipliers)
    {
        outNumbers.Clear();
        outMultipliers.Clear();

        if (s.targetType.IncludesSingles())
            AddSteps(numbers, DartMultiplier.Single, outNumbers, outMultipliers);

        if (s.targetType.IncludesDoubles())
            AddSteps(numbers, DartMultiplier.Double, outNumbers, outMultipliers);

        if (s.targetType.IncludesTriples())
            AddSteps(numbers, DartMultiplier.Triple, outNumbers, outMultipliers);
    }

    private static void AddSteps(List<int> numbers, DartMultiplier mult, List<int> outNumbers, List<DartMultiplier> outMultipliers)
    {
        foreach (int n in numbers)
        {
            if (n == 25 && mult == DartMultiplier.Triple)
                continue; // never T25

            outNumbers.Add(n);
            outMultipliers.Add(mult);
        }
    }

    private void StartNewLeg()
    {
        var currentSet = sets[^1];
        var newLeg = new Leg();
        currentSet.AddLeg(newLeg);

        // Starting player ALWAYS rotates to Leg-Win (even if the Leg ends in bonus phase)
        RotateStartingPlayer();
        ResetToStartingPlayer();
        currentTurn = new Turn(GetCurrentPlayerId());
        newLeg.AddTurn(currentTurn);
        validThrowsInTurn = 0;

        // Reset player indices for new leg
        foreach (var pid in playerIDs)
        {
            playerCurrentIndex[pid] = 0;
            playerStarted[pid] = false;
        }

        // New starting player is fixed (possibly a bot) → Bot-Check like X01/Cricket.
        // Without that, a bot will remain idle after leg gain because of the engine-
        // Handler to the LegWon event still sees the old player.
        TriggerBotCheck();
    }

    public void NextPlayer()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % playerIDs.Count;
        currentTurn = new Turn(GetCurrentPlayerId());
        GetCurrentLeg().AddTurn(currentTurn);
        validThrowsInTurn = 0;

        TouchActivity();
        TriggerBotCheck();
    }

    // =========================================================
    // PUBLIC GETTERS
    // =========================================================

    public int GetCurrentTarget(Guid playerId)
    {
        int index = playerCurrentIndex[playerId];
        return index < targets.Count ? targets[index] : -1;
    }

    /// <summary>Required multiplier of the current step (combi modes).</summary>
    public DartMultiplier GetCurrentTargetMultiplier(Guid playerId)
    {
        int index = playerCurrentIndex[playerId];
        if (targetMultipliers != null && index >= 0 && index < targetMultipliers.Count)
            return targetMultipliers[index];

        return DefaultMultiplierFor(GetSettingsAsATC().targetType);
    }

    public bool IsPlayerFinished(Guid playerId)
    {
        return playerCurrentIndex[playerId] >= targets.Count;
    }

    public bool HasPlayerStarted(Guid playerId)
    {
        return playerStarted[playerId];
    }

    public int GetTotalTargets()
    {
        return targets.Count;
    }

    /// <summary>Copies for persistence (game_meta): Step list + progress.</summary>
    public List<int> GetTargetNumbers() => new List<int>(targets);

    /// <summary>Copies for persistence (game_meta): step multipliers.</summary>
    public List<DartMultiplier> GetTargetMultipliers() => new List<DartMultiplier>(targetMultipliers);

    /// <summary>Copies for persistence (game_meta): Target progress per player.</summary>
    public Dictionary<Guid, int> GetTargetIndices() => new Dictionary<Guid, int>(playerCurrentIndex);

    public int GetWonSetsTotal(Guid playerId)
    {
        return sets.Count(s => s.WinnerPlayerId == playerId);
    }

    public int GetTargetsHit(Guid playerId)
    {
        return playerCurrentIndex.TryGetValue(playerId, out var index) ? index : 0;
    }

    public int GetHitsInCurrentRound(Guid playerId)
    {
        if (GetCurrentPlayerId() == playerId)
            return validThrowsInTurn;

        return lastVisitHits.TryGetValue(playerId, out var hits)
            ? hits
            : 0;
    }

    public int GetTotalThrows(Guid playerId)
    {
        return sets
            .SelectMany(s => s.GetLegs())
            .SelectMany(l => l.GetTurns())
            .Where(t => t.PlayerId == playerId)
            .Sum(t => t.GetThrows().Count);
    }

    public List<Turn> GetAllTurns(Guid playerId)
    {
        var turns = new List<Turn>();

        foreach (var set in sets)
            foreach (var leg in set.GetLegs())
                turns.AddRange(leg.GetTurns().Where(t => t.PlayerId == playerId));

        return turns;
    }

    public List<Throw> GetAllThrows(Guid playerId) =>
        GetAllTurns(playerId).SelectMany(t => t.GetThrows()).ToList();


    public int GetTotalHits(Guid playerId)
    {
        return GetAllThrows(playerId).Count(t => t.IsTargetHit);
    }

    public void Undo()
    {
        if (IsEmpty())
            return;

        var undoInfo = base.UndoBase();
        // if (undoInfo.MatchWinnerUndone) base.UndoMatchEnd();

        var leg = GetCurrentLeg();
        Turn lastTurnWithThrows = leg.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0);
        
        if (lastTurnWithThrows == null)
        {
            TouchActivity();
            return;
        }

        Guid undoPlayerId = lastTurnWithThrows.PlayerId;
        var throws = lastTurnWithThrows.GetThrows();
        var lastThrow = throws.Last();

        int currentIndex = playerCurrentIndex[undoPlayerId];
        bool isValid = lastThrow.IsTargetHit;

        // Reconstructing the goal for throw time
        int targetAtTimeOfThrow = isValid && currentIndex > 0 && currentIndex <= targets.Count
            ? targets[currentIndex - 1]
            : GetCurrentTarget(undoPlayerId);

        if (playerStats.TryGetValue(undoPlayerId, out var stats))
        {
            var sATC = (GameStatsATC)stats;
            int streakBefore = CalculateStreakBeforeUndo(undoPlayerId);
            bool wasFirstOfVisit = throws.Count == 1 && leg.GetTurns().Count(tn => tn.PlayerId == undoPlayerId) == 1;

            Debug.Log("ATCGame  targetAtTimeOfThrow: " + targetAtTimeOfThrow);

            // Correct stats
            sATC.RemoveThrow(lastThrow, targetAtTimeOfThrow, isValid, wasFirstOfVisit, streakBefore);
        }

        // Turn back progress in the game state
        if (isValid)
        {
            playerCurrentIndex[undoPlayerId] = Math.Max(0, currentIndex - 1);
        }

        // --- END OF CORRECTION ---

        lastTurnWithThrows.RemoveLastThrow();
        CleanupTrailingEmptyTurns(leg, keepPlayerId: undoPlayerId);
        RebuildATCProgressStateFromTurns(leg);

        currentTurn = leg.GetLastTurn() ?? new Turn(GetCurrentPlayerId());
        if (leg.GetLastTurn() == null) leg.AddTurn(currentTurn);

        currentPlayerIndex = playerIDs.IndexOf(currentTurn.PlayerId);
        TouchActivity();
    }

    /// <summary>
    /// Removes at the end of the leg all empty turns that do not belong to the keepPlayerId.
    /// These turns are typically made by NextPlayer() and have to go away at Undo,
    /// When we actually jump back before the change of players.
    /// </summary>
    private void CleanupTrailingEmptyTurns(Leg leg, Guid keepPlayerId)
    {
        if (leg == null) return;

        var turns = leg.GetTurns();
        while (turns.Count > 0)
        {
            var last = turns[^1];
            if (last == null) break;

            // Never remove the single turn
            if (turns.Count == 1) break;

            // Only empty turns are candidates
            if (last.GetThrows().Count > 0) break;

            if (last.PlayerId == keepPlayerId)
            {
                // Special case: An empty turn of the same player can be a "synthetic" slot,
                // generated by CompleteTurn (..., switchPlayer:false).
                // If we now go into the previous turn (and which has <3 darts again),
                // This empty slot must be removed again, otherwise the bonus phase is no longer correct.
                var prev = turns[^2];
                if (prev.PlayerId == keepPlayerId && prev.GetThrows().Count < 3)
                {
                    turns.RemoveAt(turns.Count - 1);
                    continue;
                }

                // otherwise it is a legitimate bonus slot (player stay tuned)
                break;
            }

            turns.RemoveAt(turns.Count - 1);
        }
    }

    /// <summary>
    /// Reconstructed playerCurrentIndex, playerStarted and validThrowsInTurn exclusively from the turns
    /// of the current leg (without re-initializing PlayerStats).
    /// </summary>
    private void RebuildATCProgressStateFromTurns(Leg leg)
    {
        if (leg == null) return;

        // InitializeTargets();

        playerCurrentIndex ??= new Dictionary<Guid, int>();
        playerStarted ??= new Dictionary<Guid, bool>();
        playerCarryOn ??= new Dictionary<Guid, bool>();

        foreach (var pid in playerIDs)
        {
            playerCurrentIndex[pid] = 0;
            playerStarted[pid] = false;
            if (!playerCarryOn.ContainsKey(pid))
                playerCarryOn[pid] = false;
        }

        // Replay: we need to check validity against the target "at throw time".
        // In addition, we determine validThrowsInTurn as the number of valid hits in the current visit
        // (= sum of valid hits over all consecutive turns of the last player).
        Guid? visitPlayerId = null;
        int visitValidHits = 0;

        foreach (var turn in leg.GetTurns())
        {
            Guid pid = turn.PlayerId;

            if (visitPlayerId != pid)
            {
                visitPlayerId = pid;
                visitValidHits = 0;
            }

            foreach (var thr in turn.GetThrows())
            {
                bool valid = IsValidThrowRebuild(thr, pid);
                if (valid)
                {
                    playerCurrentIndex[pid]++;
                    visitValidHits++;
                }
            }
        }

        validThrowsInTurn = visitPlayerId != null ? visitValidHits : 0;
    }

    private int CalculateStreakBeforeUndo(Guid playerId)
    {
        var playerThrows = GetCurrentLeg()
            .GetTurns()
            .Where(t => t.PlayerId == playerId)
            .SelectMany(t => t.GetThrows())
            .ToList();

        if (playerThrows.Count <= 1)
            return 0;

        // Ignore last throw
        playerThrows.RemoveAt(playerThrows.Count - 1);

        int targetIndex = 0;

        // Forward reconstruction
        foreach (var thr in playerThrows)
        {
            if (targetIndex >= targets.Count)
                break;

            if (IsValidThrowForStep(thr, targets[targetIndex], GetTargetMultiplierAt(targetIndex)))
                targetIndex++;
        }

        // Now counting backwards streak
        int streak = 0;

        for (int i = playerThrows.Count - 1; i >= 0 && targetIndex > 0; i--)
        {
            int expectedIndex = targetIndex - 1;
            int expectedTarget = targets[expectedIndex];

            if (!IsValidThrowForStep(playerThrows[i], expectedTarget, GetTargetMultiplierAt(expectedIndex)))
                break;

            streak++;
            targetIndex--;
        }

        return streak;
    }

    private DartMultiplier GetTargetMultiplierAt(int index)
    {
        if (targetMultipliers != null && index >= 0 && index < targetMultipliers.Count)
            return targetMultipliers[index];

        return DefaultMultiplierFor(GetSettingsAsATC().targetType);
    }

    // =========================================================
    // GAME LOGIC
    // =========================================================

    public void AddThrow(Throw t)
    {
        Guid playerId = GetCurrentPlayerId();
        int currentTarget = GetCurrentTarget(playerId);
        if (currentTarget == -1) return;

        bool isValidThrow = IsValidThrow(t, playerId);
        bool wasFirstDartOfVisit = currentTurn.GetThrows().Count == 0 && validThrowsInTurn == 0;

        if (playerStats.TryGetValue(playerId, out var stats))
        {
            if (stats is GameStatsATC)
            {
                ((GameStatsATC) stats).AddThrow(t, currentTarget, isValidThrow, wasFirstDartOfVisit);
            }
        }

        currentTurn.AddThrow(t);

        // Sector sound right at input (before instant penalties).
        NotifySectorHit(playerId, t);

        if (isValidThrow)
        {
            playerStarted[playerId] = true;   // MISSING LINE
            if (playerStarted[playerId])
            {
                playerCurrentIndex[playerId]++;
            }
            validThrowsInTurn++;
        }

        // Custom penalties (instant type) per throw; legacy paths are
        // decommissioned (all about customization snapshots).
        CheckCustomInstantPenalties(playerId, t, currentTurn);

        if (IsPlayerFinished(playerId))
        {
            int legWinHits = validThrowsInTurn;
            FinalizeVisit(playerId, false);
            HandleLegWon(playerId);
            var currentSet = sets[^1];
            Guid setWinner = GetSetWinner(currentSet);
            if (setWinner != Guid.Empty)
            {
                currentSet.SetWinner(setWinner);
                HandleSetWon(setWinner); 
                if (IsMatchFinished(setWinner))
                {
                    validThrowsInTurn = 0;
                    HandleMatchEnd(setWinner);
                    return;
                }

                AnnounceVisitCompleted(playerId, legWinHits);
                TryTriggerManualAudioEvent(SeededAudioEvents.LegEndId);
                TryTriggerManualAudioEvent(SeededAudioEvents.SetEndId);
                validThrowsInTurn = 0;
                
                var newSet = new Set();
                var newLeg = new Leg();
                newSet.AddLeg(newLeg);
                sets.Add(newSet);
                RotateStartingPlayer();
                ResetToStartingPlayer();
                currentTurn = new Turn(GetCurrentPlayerId());
                newLeg.AddTurn(currentTurn);
                foreach (var pid in playerIDs) { playerCurrentIndex[pid] = 0; playerStarted[pid] = false; }
                
                // Check for Set-Change Bot
                TriggerBotCheck();
            }
            else
            {
                AnnounceVisitCompleted(playerId, legWinHits);
                TryTriggerManualAudioEvent(SeededAudioEvents.LegEndId);
                StartNewLeg();
            }
            validThrowsInTurn = 0;
            TouchActivity();
            return;
        }

        // =========================================================
        // ATC FLOW LOGIC (KORRIGIERT)
        // =========================================================
        int throwCountInCurrentSlot = currentTurn.GetThrows().Count;
        
        // Logic: If we already have 3 hits in the current visit, we are in the bonus.
        // Exception: If we threw exactly 3 darts AND the 3rd dart just led to the 3rd hit,
        // We are still technically in the transition to the bonus slot.
        bool isBonusPhase = validThrowsInTurn >= 3 && throwCountInCurrentSlot <= 3 && !(validThrowsInTurn == 3 && throwCountInCurrentSlot == 3);
        
        // Simpler, more stable testing: Were we already in the bonus before this throw or are we beyond Dart 3?
        if (validThrowsInTurn > 3 || (validThrowsInTurn == 3 && throwCountInCurrentSlot > 0 && throwCountInCurrentSlot < 3))
        {
            isBonusPhase = true;
        }

        if (isBonusPhase)
        {
            if (!isValidThrow)
                CompleteTurn(playerId, true); // Mistake in the bonus -> CHANGE
            else
            {
                if (throwCountInCurrentSlot == 3)
                    CompleteTurn(playerId, false); // Slot full -> NEW SLOT
                else
                    TouchActivity();
            }
            return;
        }

        // Normal-Phase (Darts 1-3)
        if (throwCountInCurrentSlot < 3)
        {
            TouchActivity();
        }
        else
        {
            if (validThrowsInTurn == 3)
            {
                OnStreakStarted?.Invoke(playerId);
                CompleteTurn(playerId, false);
            }
            else
            {
                CompleteTurn(playerId, true);
            }
        }
    }


    private void CompleteTurn(Guid playerId, bool switchPlayer)
    {
        FinalizeVisit(playerId, switchPlayer);

        if (switchPlayer)
        {
            validThrowsInTurn = 0;
            NextPlayer();
        }
        else
        {
            currentTurn = new Turn(playerId);
            GetCurrentLeg().AddTurn(currentTurn);
            TouchActivity();
        }
    }

    /// <summary>Hit of the visit + cross-throw streak for audio trigger.</summary>
    protected override void CollectAudioTriggerValues(System.Guid playerId, Turn turn, AudioTriggerValues values)
    {
        base.CollectAudioTriggerValues(playerId, turn, values);

        values.hits = validThrowsInTurn;

        if (playerStats.TryGetValue(playerId, out var stats) && stats is GameStatsATC atc)
            values.streak = atc.currentHitStreak;
    }

    private void FinalizeVisit(Guid playerId, bool isPlayerSwitch)
    {
        EvaluateCustomTurnPenalties(playerId, currentTurn);
        EvaluateCustomRemainingPenalties(playerId, currentTurn);
        EvaluateCustomAudioEvents(playerId, currentTurn, IsNonHitsAudioEvent);

        lastVisitHits[playerId] = validThrowsInTurn;

        if (isPlayerSwitch)
            AnnounceVisitCompleted(playerId, validThrowsInTurn);
    }

    /// <summary>
    /// Number → Hits sound. Runs on player change and leg change
    /// (not at the end of the match) so that the hits sound is never ahead of the number.
    /// </summary>
    private void AnnounceVisitCompleted(Guid playerId, int hits)
    {
        OnATCTurnCompleted?.Invoke(playerId, hits);
        EvaluateCustomAudioEvents(playerId, currentTurn, IsHitsAudioEvent);
    }

    private static bool IsHitsAudioEvent(AudioEventDef def)
        => def?.triggers != null && def.triggers.Count > 0 && def.triggers.FirstOrDefault().kind == AudioEventTriggerKind.HitsInTurnThreshold;

    private static bool IsNonHitsAudioEvent(AudioEventDef def)
        => !IsHitsAudioEvent(def);

    private bool IsValidThrow(Throw thr, Guid playerId)
    {
        int currentTarget = GetCurrentTarget(playerId);
        if (currentTarget == -1)
            return false;

        return IsValidThrowForStep(thr, currentTarget, GetCurrentTargetMultiplier(playerId));
    }

    /// <summary>
    /// Checks a roll against (number + required multiplier) of a step.
    /// Single steps accept any board hit (old singles semantics).
    /// </summary>
    private static bool IsValidThrowForStep(Throw thr, int target, DartMultiplier required)
    {
        if (thr.Value != target)
            return false;

        if (required == DartMultiplier.Single)
        {
            return thr.Multiplier == DartMultiplier.Single
                || thr.Multiplier == DartMultiplier.Double
                || thr.Multiplier == DartMultiplier.Triple;
        }

        return thr.Multiplier == required;
    }

    private Guid GetSetWinner(Set set)
    {
        var legWinners = set.GetLegs()
            .Select(l => l.WinnerPlayerId)
            .Where(w => w != Guid.Empty)
            .ToList();

        var groups = legWinners
            .GroupBy(p => p)
            .ToDictionary(g => g.Key, g => g.Count());

        var s = GetSettingsAsATC();
        int legsToWin = s.setsAndLegsMode == SetsAndLegs.FirstTo
            ? s.legCount
            : (s.legCount / 2) + 1;

        foreach (var kv in groups)
            if (kv.Value >= legsToWin)
                return kv.Key;

        return Guid.Empty;
    }

    protected override void InitializeStats()
    {
        if (playerStats != null)
            playerStats.Clear();

        playerStats = new Dictionary<Guid, GameStats>();

        foreach (var pid in playerIDs)
        {
            playerStats[pid] = new GameStatsATC(pid);
        }
    }


    public override void CalculatePlayerStatsOnSave()
    {
        // Base recomputes legs/sets/games/throws (idempotent).
        base.CalculatePlayerStatsOnSave();

        if (playerStats == null || playerIDs == null)
            return;

        var atcSettings = GetSettingsAsATC();
        if (atcSettings == null)
            return;

        BuildStepListsForRebuild(atcSettings, out var targetList, out var targetMults);
        if (targetList == null || targetList.Count == 0)
            return;

        // Reset ATC fields (base only resets legs/sets/throws).
        // Dart counters (totalThrows /Double/Triple) and penalties remain off base.
        foreach (var pid in playerIDs)
        {
            if (playerStats.TryGetValue(pid, out var s) && s is GameStatsATC atc)
                ResetATCStatsForRebuild(atc);
        }

        // Chronological simulation over all sets/legs/turns.
        // Target index is reset per leg (like StartNewLeg in live play).
        // A "visit" is a successive turn of the same player
        // (Bonus slots keep validThrowsInTurn – like RebuildATCProgressStateFromTurns).
        foreach (var set in sets)
        {
            foreach (var leg in set.GetLegs())
            {
                var simIndex = playerIDs.ToDictionary(pid => pid, _ => 0);

                Guid? visitPlayerId = null;
                int visitValidHits = 0;

                foreach (var turn in leg.GetTurns())
                {
                    var throws = turn.GetThrows();
                    if (throws.Count == 0)
                        continue;

                    Guid pid = turn.PlayerId;
                    if (!playerStats.TryGetValue(pid, out var s) || s is not GameStatsATC stats)
                        continue;

                    if (visitPlayerId != pid)
                    {
                        visitPlayerId = pid;
                        visitValidHits = 0;
                    }

                    for (int i = 0; i < throws.Count; i++)
                    {
                        var thr = throws[i];

                        int idx = simIndex[pid];
                        if (idx >= targetList.Count)
                            continue; // Players had all targets – like live-early return at -1.

                        int target = targetList[idx];
                        bool valid = IsValidThrowForTarget(thr, target, targetMults[idx]);
                        bool wasFirstDartOfVisit = (i == 0 && visitValidHits == 0);

                        stats.AddThrow(thr, target, valid, wasFirstDartOfVisit);

                        if (valid)
                        {
                            simIndex[pid]++;
                            visitValidHits++;
                        }
                    }
                }
            }
        }

        // Trailing Streaks flush-en: A played-through end-streak (no)
        // Misthrow more until end of play would never be registered otherwise.
        // Uses the exact miss branch logic (no live behavior changed).
        foreach (var pid in playerIDs)
        {
            if (!playerStats.TryGetValue(pid, out var s) || s is not GameStatsATC stats)
                continue;

            stats.FlushCurrentStreak();
        }
    }

    private void ResetATCStatsForRebuild(GameStatsATC stats)
    {
        if (stats == null)
            return;

        stats.targetsHit = 0;
        stats.totalTargets = 0;
        stats.hitPercentage = 0f;

        stats.firstDartHits = 0;
        stats.firstDartAttempts = 0;
        stats.firstDartHitPercentage = 0f;

        stats.hasStreak = false;
        stats.currentHitStreak = 0;
        stats.longestHitStreak = 0;
        stats.completedStreaks.Clear();

        if (stats.throwsPerTarget != null)
            foreach (var k in stats.throwsPerTarget.Keys.ToList())
                stats.throwsPerTarget[k] = 0;

        // Sector counters are rebuilt by simulation (AddThrow )
        stats.throwSectorCounts?.Clear();

        if (stats.hitsPerTarget != null)
            foreach (var k in stats.hitsPerTarget.Keys.ToList())
                stats.hitsPerTarget[k] = 0;

        stats.streak3Plus = 0;
        stats.streak6Plus = 0;
        stats.streak9Plus = 0;
        stats.streak12Plus = 0;
    }

    /// <summary>
    /// Builds the Target order from the settings (like InitializeTargets).
    /// Random is not reconstructable from the settings (is not persisted)
    /// and falls back to the stored list.
    /// </summary>
    private void BuildStepListsForRebuild(ATCGameSettings s, out List<int> numbers, out List<DartMultiplier> multipliers)
    {
        // Random is not reconstructable from the settings (will not be)
        // persists) and falls back to the stored list.
        if (s.order == ATCOrder.Random
            && targets != null && targets.Count > 0
            && targetMultipliers != null && targetMultipliers.Count == targets.Count)
        {
            numbers = new List<int>(targets);
            multipliers = new List<DartMultiplier>(targetMultipliers);
            return;
        }

        bool withBull = s.targetType.IncludesSingles() || s.targetType.IncludesDoubles();

        numbers = new List<int>();
        multipliers = new List<DartMultiplier>();
        ExpandSteps(BuildNumberOrder(s, withBull, shuffleRandom: false), s, numbers, multipliers);
    }

    /// <summary>
    /// Like IsValidThrow, but against the target at throw time (rebuild simulation),
    /// Not against the live current target.
    /// </summary>
    private bool IsValidThrowForTarget(Throw thr, int target, DartMultiplier required)
    {
        return IsValidThrowForStep(thr, target, required);
    }

    private bool IsValidThrowRebuild(Throw thr, Guid playerId)
    {
        int currentTarget = GetCurrentTarget(playerId);
        if (currentTarget == -1)
            return false;

        return IsValidThrowForStep(thr, currentTarget, GetCurrentTargetMultiplier(playerId));
    }
}
