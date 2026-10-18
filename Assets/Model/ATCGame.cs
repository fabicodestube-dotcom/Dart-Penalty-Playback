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
    /// Geforderter Multiplikator pro Schritt (parallel zu targets).
    /// Kombi-Modi wiederholen Zahlen (z.B. 1S,1D,2S,2D...).
    /// Alte Saves ohne diese Liste werden aus targetType abgeleitet.
    /// </summary>
    [JsonProperty] private List<DartMultiplier> targetMultipliers = new List<DartMultiplier>();
    [JsonProperty] private Dictionary<Guid, int> playerCurrentIndex = new Dictionary<Guid, int>();
    [JsonProperty] private Dictionary<Guid, bool> playerStarted = new Dictionary<Guid, bool>();
    [JsonProperty] private Dictionary<Guid, bool> playerCarryOn = new Dictionary<Guid, bool>();

    [JsonProperty] private int validThrowsInTurn = 0;
    [JsonIgnore] public Action<Guid, int> OnATCTurnCompleted;
    [JsonIgnore] public Action<Guid> OnStreakStarted;

    [JsonProperty] private Dictionary<Guid, int> lastVisitHits = new();

    // Dartboard-Reihenfolge im Uhrzeigersinn (20 → 1 → 18 → 4 → …)
    private static readonly int[] DartboardOrderRight = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };
    // Dartboard-Reihenfolge gegen den Uhrzeigersinn (20 → 5 → 12 → …)
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

        // Alte Saves kennen nur targets (ohne Wiederholungen/Multiplikatoren).
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
    /// Füllt targetMultipliers aus targetType auf, falls die Liste fehlt oder
    /// kürzer ist (alte Savegames ohne Wiederholungen/Kombi-Modi).
    /// </summary>
    private void EnsureTargetMultipliers()
    {
        if (targetMultipliers != null && targetMultipliers.Count == targets.Count)
            return;

        var fallback = DefaultMultiplierFor(GetSettingsAsATC().targetType);
        targetMultipliers = Enumerable.Repeat(fallback, targets.Count).ToList();
    }

    /// <summary>
    /// Stellt nach LoadGameById den Zielfortschritt wieder her.
    /// Hintergrund: playerCurrentIndex/targets/targetMultipliers wurden nie
    /// persistiert — nach Load lief jedes Spiel auf frischen Singles-Defaults
    /// (falsche Targets nach Undo/Continue, z.B. Single20 statt T20).
    /// Mit gespeicherten Listen (neue Saves) exakt, sonst deterministisch neu
    /// aufgebaut + Fortschritt des aktuellen Legs aus den Turns replayt.
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

        // currentTurn zeigt nach relationalem Load ggf. auf einen verworfenen
        // Turn (Sets wurden neu aufgebaut): nur dann neu anhängen, sonst alles lassen.
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

    /// <summary>Zahlenfolge je Order (Bull-Position wie bisher: Asc Ende, Desc Anfang, Right/Left Ende, Random gemischt).</summary>
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
    /// Expandiert Zahlen zu Schritten in Block-Reihenfolge: erst alle Singles
    /// (1..20 + 25S), dann alle Doubles (D1..D20 + D25), dann alle Triples
    /// (T1..T20, nie T25) — jeweils nur die aktivierten Sets, Zahlenfolge je Order.
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
                continue; // nie T25

            outNumbers.Add(n);
            outMultipliers.Add(mult);
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

        // Neuer Startspieler steht fest (ggf. ein Bot) → Bot-Check wie X01/Cricket.
        // Ohne das bleibt ein Bot nach Leg-Gewinn untätig, weil der Engine-
        // Handler zum LegWon-Event noch den alten Spieler sieht.
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

    /// <summary>Geforderter Multiplikator des aktuellen Schritts (Kombi-Modi).</summary>
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

    /// <summary>Kopien für die Persistenz (game_meta): Schrittliste + Fortschritt.</summary>
    public List<int> GetTargetNumbers() => new List<int>(targets);

    /// <summary>Kopien für die Persistenz (game_meta): Schritt-Multiplikatoren.</summary>
    public List<DartMultiplier> GetTargetMultipliers() => new List<DartMultiplier>(targetMultipliers);

    /// <summary>Kopien für die Persistenz (game_meta): Zielfortschritt pro Spieler.</summary>
    public Dictionary<Guid, int> GetTargetIndices() => new Dictionary<Guid, int>(playerCurrentIndex);

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

        // Custom-Penalties (Sofort-Typ) pro Wurf; Legacy-Pfade sind
        // stillgelegt (alles über Customization-Snapshots).
        CheckCustomInstantPenalties(playerId, t, currentTurn);

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

    /// <summary>Treffer des Turns + wurfübergreifender Streak für Audio-Trigger.</summary>
    protected override void CollectAudioTriggerValues(System.Guid playerId, Turn turn, AudioTriggerValues values)
    {
        base.CollectAudioTriggerValues(playerId, turn, values);

        if (turn != null)
            values.hits = turn.GetThrows().Count(t => t.IsTargetHit);

        if (playerStats.TryGetValue(playerId, out var stats) && stats is GameStatsATC atc)
            values.streak = atc.currentHitStreak;
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
        if (currentTarget == -1)
            return false;

        return IsValidThrowForStep(thr, currentTarget, GetCurrentTargetMultiplier(playerId));
    }

    /// <summary>
    /// Prüft einen Wurf gegen (Zahl + geforderter Multiplikator) eines Schritts.
    /// Single-Schritte akzeptieren jeden Board-Treffer (alte Singles-Semantik).
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
        // Base recomputet Legs/Sets/Games/Throws (idempotent).
        base.CalculatePlayerStatsOnSave();

        if (playerStats == null || playerIDs == null)
            return;

        var atcSettings = GetSettingsAsATC();
        if (atcSettings == null)
            return;

        BuildStepListsForRebuild(atcSettings, out var targetList, out var targetMults);
        if (targetList == null || targetList.Count == 0)
            return;

        // ATC-Felder resetten (Base setzt nur Legs/Sets/Throws zurück).
        // Dart-Zähler (totalThrows/Double/Triple) und Penalties bleiben von der Base.
        foreach (var pid in playerIDs)
        {
            if (playerStats.TryGetValue(pid, out var s) && s is GameStatsATC atc)
                ResetATCStatsForRebuild(atc);
        }

        // Chronologische Simulation über alle Sets/Legs/Turns.
        // Ziel-Index wird pro Leg zurückgesetzt (wie StartNewLeg im Live-Spiel).
        // Ein "Visit" sind aufeinanderfolgende Turns desselben Spielers
        // (Bonus-Slots behalten validThrowsInTurn – wie RebuildATCProgressStateFromTurns).
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
                            continue; // Spieler hatte alle Targets – wie Live-Early-Return bei -1.

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

        // Trailing Streaks flush-en: Eine durchgespielte End-Streak (kein
        // Fehlwurf mehr bis Spielende) würde sonst nie registriert.
        // Nutzt exakt die Miss-Branch-Logik (kein Live-Verhalten geändert).
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

        // Sektor-Zähler werden per Simulation (AddThrow) neu aufgebaut
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
    /// Baut die Target-Reihenfolge aus den Settings (wie InitializeTargets).
    /// Random ist nicht aus den Settings rekonstruierbar (wird nicht persistiert)
    /// und fällt auf die gespeicherte Liste zurück.
    /// </summary>
    private void BuildStepListsForRebuild(ATCGameSettings s, out List<int> numbers, out List<DartMultiplier> multipliers)
    {
        // Random ist nicht aus den Settings rekonstruierbar (wird nicht
        // persistiert) und fällt auf die gespeicherte Liste zurück.
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
    /// Wie IsValidThrow, aber gegen das Target zur Wurfzeit (Rebuild-Simulation),
    /// nicht gegen das live-aktuelle Target.
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
