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
    [JsonProperty] private Dictionary<Guid, int> playerCurrentIndex = new Dictionary<Guid, int>();
    [JsonProperty] private Dictionary<Guid, bool> playerStarted = new Dictionary<Guid, bool>();
    [JsonProperty] private Dictionary<Guid, bool> playerCarryOn = new Dictionary<Guid, bool>();

    [JsonProperty] private int validThrowsInTurn = 0;
    [JsonIgnore] public Action<Guid, int> OnATCTurnCompleted;
    [JsonIgnore] public Action<Guid> OnStreakStarted;

    [JsonProperty] private Dictionary<Guid, int> lastVisitHits = new();


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
    }

    private void InitializeTargets()
    {
        var baseTargets = Enumerable.Range(1, 20).ToList();

        // Bull nur wenn sinnvoll
        if (GetSettingsAsATC().targetType != ATCTargetType.Triples)
        {
            baseTargets.Add(25);
        }

        switch (GetSettingsAsATC().order)
        {
            case ATCOrder.Ascending:
                targets = baseTargets;
                break;

            case ATCOrder.Descending:
                targets = baseTargets.OrderByDescending(x => x).ToList();
                break;

            case ATCOrder.Random:
                targets = baseTargets
                    .OrderBy(x => UnityEngine.Random.value)
                    .ToList();
                break;
        }
    }

    private void StartNewLeg()
    {
        var currentSet = sets[^1];
        var newLeg = new Leg();
        currentSet.AddLeg(newLeg);

        // Startspieler rotiert IMMER nach Leg-Win (auch wenn das Leg in Bonusphase endet)
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

    public int GetWonLegsTotal(Guid playerId)
    {
        return sets
            .SelectMany(s => s.GetLegs())
            .Count(l => l.WinnerPlayerId == playerId);
    }

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

        // Ziel zur Wurfzeit rekonstruieren
        int targetAtTimeOfThrow;

        if (isValid)
        {
            targetAtTimeOfThrow = targets[Math.Max(0, currentIndex - 1)];
        }
        else
        {
            targetAtTimeOfThrow = GetCurrentTarget(undoPlayerId);
        }

        if (playerStats.TryGetValue(undoPlayerId, out var stats))
        {
            var sATC = (GameStatsATC)stats;
            int streakBefore = CalculateStreakBeforeUndo(undoPlayerId);
            bool wasFirstOfVisit = throws.Count == 1 && leg.GetTurns().Count(tn => tn.PlayerId == undoPlayerId) == 1;

            Debug.Log("ATCGame  targetAtTimeOfThrow: " + targetAtTimeOfThrow);

            // Stats korrigieren
            sATC.RemoveThrow(lastThrow, targetAtTimeOfThrow, isValid, wasFirstOfVisit, streakBefore);
        }

        // 3. Fortschritt im Spiel-State zurückdrehen
        if (isValid)
        {
            playerCurrentIndex[undoPlayerId] = Math.Max(0, currentIndex - 1);
        }

        // --- KORREKTUR ENDE ---

        lastTurnWithThrows.RemoveLastThrow();
        CleanupTrailingEmptyTurns(leg, keepPlayerId: undoPlayerId);
        RebuildATCProgressStateFromTurns(leg);

        currentTurn = leg.GetLastTurn() ?? new Turn(GetCurrentPlayerId());
        if (leg.GetLastTurn() == null) leg.AddTurn(currentTurn);

        currentPlayerIndex = playerIDs.IndexOf(currentTurn.PlayerId);
        TouchActivity();
    }

    /// <summary>
    /// Entfernt am Ende des Legs alle leeren Turns, die nicht zum keepPlayerId gehören.
    /// Diese Turns entstehen typischerweise durch NextPlayer() und müssen bei Undo wieder weg,
    /// wenn wir eigentlich vor dem Spielerwechsel zurückspringen.
    /// </summary>
    private void CleanupTrailingEmptyTurns(Leg leg, Guid keepPlayerId)
    {
        if (leg == null) return;

        var turns = leg.GetTurns();
        while (turns.Count > 0)
        {
            var last = turns[^1];
            if (last == null) break;

            // niemals den einzigen Turn entfernen
            if (turns.Count == 1) break;

            // nur leere Turns sind Kandidaten
            if (last.GetThrows().Count > 0) break;

            if (last.PlayerId == keepPlayerId)
            {
                // Sonderfall: Ein leerer Turn des selben Spielers kann ein "synthetischer" Slot sein,
                // der durch CompleteTurn(..., switchPlayer:false) erzeugt wurde.
                // Wenn wir nun in den vorherigen Turn hinein-undoen (und der damit wieder <3 Darts hat),
                // muss dieser leere Slot wieder entfernt werden, sonst stimmt die Bonusphase nicht mehr.
                var prev = turns[^2];
                if (prev.PlayerId == keepPlayerId && prev.GetThrows().Count < 3)
                {
                    turns.RemoveAt(turns.Count - 1);
                    continue;
                }

                // ansonsten ist es ein legitimer Bonus-Slot (Spieler bleibt dran)
                break;
            }

            turns.RemoveAt(turns.Count - 1);
        }
    }

    /// <summary>
    /// Rekonstruiert playerCurrentIndex, playerStarted und validThrowsInTurn ausschließlich aus den Turns
    /// des aktuellen Legs (ohne PlayerStats neu zu initialisieren).
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

        // Replay: wir müssen Validität gegen das Target "zur Wurfzeit" prüfen.
        // Zusätzlich bestimmen wir validThrowsInTurn als die Anzahl gültiger Treffer im aktuellen Visit
        // (= Summe gültiger Treffer über alle aufeinanderfolgenden Turns des letzten Spielers).
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

        // letzten Wurf ignorieren
        playerThrows.RemoveAt(playerThrows.Count - 1);

        int targetIndex = 0;

        // Vorwärts rekonstruieren
        foreach (var thr in playerThrows)
        {
            if (targetIndex < targets.Count &&
                thr.Value == targets[targetIndex])
            {
                targetIndex++;
            }
        }

        // Jetzt rückwärts streak zählen
        int streak = 0;

        for (int i = playerThrows.Count - 1; i >= 0; i--)
        {
            int expectedTarget = targets[targetIndex - 1];

            if (playerThrows[i].Value == expectedTarget)
            {
                streak++;
                targetIndex--;
            }
            else
            {
                break;
            }
        }

        return streak;
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

        if (isValidThrow)
        {
            playerStarted[playerId] = true;   // 🔥 MISSING LINE
            if (playerStarted[playerId])
            {
                playerCurrentIndex[playerId]++;
            }
            validThrowsInTurn++;
        }

        if (t.HitType == HitType.Wall)
            AddPenalty(playerId, PenaltyType.Wall, currentTurn);
        else if (t.HitType == HitType.Ceiling)
            AddPenalty(playerId, PenaltyType.Ceiling, currentTurn);

        if (IsPlayerFinished(playerId))
        {
            FinalizeVisit(playerId, false);
            validThrowsInTurn = 0;
            HandleLegWon(playerId);
            var currentSet = sets[^1];
            Guid setWinner = GetSetWinner(currentSet);
            if (setWinner != Guid.Empty)
            {
                currentSet.SetWinner(setWinner);
                HandleSetWon(setWinner); 
                if (IsMatchFinished(setWinner)) { HandleMatchEnd(setWinner); return; }
                
                var newSet = new Set();
                var newLeg = new Leg();
                newSet.AddLeg(newLeg);
                sets.Add(newSet);
                RotateStartingPlayer();
                ResetToStartingPlayer();
                currentTurn = new Turn(GetCurrentPlayerId());
                newLeg.AddTurn(currentTurn);
                foreach (var pid in playerIDs) { playerCurrentIndex[pid] = 0; playerStarted[pid] = false; }
                
                // Nach Set-Wechsel Bot prüfen
                TriggerBotCheck();
            }
            else { StartNewLeg(); }
            validThrowsInTurn = 0;
            TouchActivity();
            return;
        }

        // =========================================================
        // ATC FLOW LOGIC (KORRIGIERT)
        // =========================================================
        int throwCountInCurrentSlot = currentTurn.GetThrows().Count;
        
        // Logik: Wenn wir schon 3 Treffer im aktuellen Besuch haben, sind wir im Bonus.
        // Ausnahme: Wenn wir exakt 3 Darts geworfen haben UND der 3. Dart gerade zum 3. Treffer führte, 
        // befinden wir uns technisch noch im Übergang zum Bonus-Slot.
        bool isBonusPhase = validThrowsInTurn >= 3 && throwCountInCurrentSlot <= 3 && !(validThrowsInTurn == 3 && throwCountInCurrentSlot == 3);
        
        // Einfachere, stabilere Prüfung: Waren wir vor diesem Wurf schon im Bonus oder sind wir über Dart 3 hinaus?
        if (validThrowsInTurn > 3 || (validThrowsInTurn == 3 && throwCountInCurrentSlot > 0 && throwCountInCurrentSlot < 3))
        {
            isBonusPhase = true;
        }

        if (isBonusPhase)
        {
            if (!isValidThrow)
                CompleteTurn(playerId, true); // Fehlwurf im Bonus -> WECHSEL
            else
            {
                if (throwCountInCurrentSlot == 3)
                    CompleteTurn(playerId, false); // Slot voll -> NEUER SLOT
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

    private void FinalizeVisit(Guid playerId, bool isPlayerSwitch)
    {
        CheckTurnPenalties(playerId, currentTurn);

        lastVisitHits[playerId] = validThrowsInTurn;

        if (isPlayerSwitch)
        {
            OnATCTurnCompleted?.Invoke(playerId, validThrowsInTurn);
        }
    }

    private bool IsValidThrow(Throw thr, Guid playerId)
    {
        int currentTarget = GetCurrentTarget(playerId);
        var targetType = GetSettingsAsATC().targetType;

        // Grundvoraussetzung: Die Zahl muss stimmen
        bool correctNumber = (thr.Value == currentTarget);
        
        // Hilfsvariable: Hat er überhaupt irgendwas auf der Scheibe getroffen?
        // (In manchen Systemen ist ein Miss ein Multiplier von 0 oder ein spezieller HitType)
        bool isAnyHit = thr.Multiplier == DartMultiplier.Single || 
                        thr.Multiplier == DartMultiplier.Double || 
                        thr.Multiplier == DartMultiplier.Triple;

        switch (targetType)
        {
            case ATCTargetType.Singles:
                // Hit, wenn die Zahl stimmt, egal ob S, D oder T
                return correctNumber && isAnyHit;

            case ATCTargetType.Doubles:
                return correctNumber && thr.Multiplier == DartMultiplier.Double;

            case ATCTargetType.Triples:
                return correctNumber && thr.Multiplier == DartMultiplier.Triple;

            default:
                return false;
        }
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

        int legsToWin = (GetSettingsAsATC().legCount / 2) + 1;

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


    private bool IsValidThrowRebuild(Throw thr, Guid playerId)
    {
        int currentTarget = GetCurrentTarget(playerId);
        var targetType = GetSettingsAsATC().targetType;

        bool correctNumber = thr.Value == currentTarget;

        bool isAnyHit =
            thr.Multiplier == DartMultiplier.Single ||
            thr.Multiplier == DartMultiplier.Double ||
            thr.Multiplier == DartMultiplier.Triple;

        switch (targetType)
        {
            case ATCTargetType.Singles:
                return correctNumber && isAnyHit;

            case ATCTargetType.Doubles:
                return correctNumber && thr.Multiplier == DartMultiplier.Double;

            case ATCTargetType.Triples:
                return correctNumber && thr.Multiplier == DartMultiplier.Triple;
        }

        return false;
    }
}
