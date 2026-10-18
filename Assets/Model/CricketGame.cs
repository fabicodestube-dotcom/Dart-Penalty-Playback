using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class CricketGame : Game
{
    // =========================================================
    // STATE
    // =========================================================

    [JsonProperty] private Turn currentTurn;

    [JsonProperty] private Dictionary<Guid, Dictionary<int, int>> playerHits =
        new Dictionary<Guid, Dictionary<int, int>>();

    [JsonProperty] private Dictionary<Guid, int> playerScores =
        new Dictionary<Guid, int>();


    [JsonProperty] private static readonly HashSet<int> numbers = new HashSet<int>
    { 15, 16, 17, 18, 19, 20, 25 };


    // =========================================================
    // EVENTS
    // =========================================================

    public new event Action<CricketTurnResult> OnTurnCompleted;

    [JsonProperty] private List<CricketHitEvent> currentTurnHitEvents = new();




    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public CricketGame(Guid id, CricketGameSettings settings, List<Guid> playerIDs)
    {
        gameMode = GameMode.Cricket;
        InitializeTimestampsOnCreate();

        this.id = id;
        this.settings = settings;
        
        this.playerIDs = playerIDs;

        if (settings != null && !settings.IsValid(out var reason))
            Debug.LogWarning(reason);

        var set = new Set();
        var leg = new Leg();
        set.AddLeg(leg);
        sets.Add(set);

        startingPlayerIndex = 0;
        currentPlayerIndex = startingPlayerIndex;

        ResetLegState();

        currentTurn = new Turn(GetCurrentPlayerId());
        leg.AddTurn(currentTurn);

        InitializeStats();
        //InitializeLegsPlayedAtStart();

        CaptureCustomizationSnapshot();
    }

    public Dictionary<Guid, int> GetPlayerScores()
    {
        return playerScores.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    public Dictionary<Guid, Dictionary<int, int>> GetPlayerHits()
    {
        return playerHits.ToDictionary(
            playerEntry => playerEntry.Key,
            playerEntry => playerEntry.Value.ToDictionary(
                hitEntry => hitEntry.Key,
                hitEntry => hitEntry.Value
            )
        );
    }

    private Set GetCurrentSet() => sets[^1];

    private void ResetLegState()
    {
        foreach (var pid in playerIDs)
        {
            playerScores[pid] = 0;

            playerHits[pid] = new Dictionary<int, int>();
            foreach (var n in numbers)
                playerHits[pid][n] = 0;
        }
    }
    public CricketGameSettings GetSettingsAsCricket()
    {
        return settings as CricketGameSettings;
    }


    // =========================================================
    // PUBLIC GETTERS
    // =========================================================

    public int GetScore(Guid playerId) => playerScores[playerId];

    public int GetHits(Guid playerId, int number) => playerHits[playerId][number];

    public bool IsClosed(Guid playerId, int number) =>
        playerHits[playerId][number] >= 3;

    public bool IsLocked(int number) =>
        numbers.Contains(number) && playerIDs.All(pid => playerHits[pid][number] >= 3);

    public IReadOnlyCollection<int> GetNumbers() => numbers;

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

    public int GetTotalDarts(Guid playerId) =>
        GetAllTurns(playerId).Sum(t => t.GetThrows().Count);
        
    public float GetMPR(Guid playerId)
    {
        float mpr = 0f;
        
        if (playerStats.TryGetValue(playerId, out var stats))
        {
            mpr = ((GameStatsCricket) stats).marksPerRound;        
        }
        return mpr;
    }

    // =========================================================
    // MAIN LOGIC
    // =========================================================

    public void AddThrow(Throw t)
    {
        if (IsFinished()) return;

        Guid playerId = GetCurrentPlayerId();
        var stats = playerStats[playerId] as GameStatsCricket;

        if (currentTurn.GetThrows().Count == 0)
            currentTurn.ScoreBeforeTurn = 0; 

        // Turn-Start-Snapshot aller Totals (für Schnapszahl-Übergangsregel:
        // nur veränderte Totals können neu erreicht worden sein).
        // Bleibt bei Undo/Reopen erhalten, neue Turns bekommen frische Snapshots.
        if (currentTurn.GetThrows().Count == 0 && currentTurn.TurnStartScores == null)
        {
            currentTurn.TurnStartScores = playerIDs.ToDictionary(
                pid => pid,
                pid => playerScores.TryGetValue(pid, out var sc) ? sc : 0);
        }

        currentTurn.AddThrow(t);
        TouchActivity();

        // =====================================================
        // CUSTOM PENALTIES (Sofort-Typ pro Wurf; Legacy stillgelegt)
        // =====================================================
        CheckCustomInstantPenalties(playerId, t, currentTurn);

        if (t.HitType != HitType.Board)
        {
            // Wichtig: Auch Fehlwürfe müssen registriert werden (0 Marks)
            stats?.RegisterThrow(t, 0);
            HandleTurnEnd(playerId);
            return;
        }

        // =====================================================
        // VALUE + MULTIPLIER NORMALIZATION
        // =====================================================
        int value = t.Value;
        int multiplier = ConvertMultiplier(t.Multiplier);

        if (value == 50) { value = 25; multiplier = 2; }

        // Ungültige Treffer (Triple Bull oder Zahlen außerhalb 15-20/Bull)
        if ((value == 25 && t.Multiplier == DartMultiplier.Triple) || !numbers.Contains(value))
        {
            stats?.RegisterThrow(t, 0); // Dart zählt, aber 0 Marks
            HandleTurnEnd(playerId);
            return;
        }

        // =====================================================
        // HIT PROCESSING
        // =====================================================
        int currentHits = playerHits[playerId][value];
        int newHits = currentHits + multiplier;
        int overflow = 0;

        if (newHits > 3)
        {
            overflow = newHits - 3;
            newHits = 3;
        }

        playerHits[playerId][value] = newHits;

        // =====================================================
        // MARKS BERECHNUNG
        // =====================================================
        int marksUsedToClose = multiplier - overflow;
        int marksEffectiveFromOverflow = 0;
        
        if (overflow > 0)
        {
            bool fieldStillOpenForSomeone = playerIDs
                .Where(p => p != playerId)
                .Any(p => playerHits[p][value] < 3);

            if (fieldStillOpenForSomeone)
                marksEffectiveFromOverflow = overflow;
        }

        int totalMarks = marksUsedToClose + marksEffectiveFromOverflow;

        // Regulären Treffer registrieren
        stats?.RegisterThrow(t, totalMarks);

        // =====================================================
        // SCORING
        // =====================================================
        var cricketSettings = GetSettingsAsCricket();
        bool pointsEnabled = cricketSettings == null || cricketSettings.pointsEnabled;
        bool cutThroat = cricketSettings != null && cricketSettings.cutThroatEnabled;

        if (pointsEnabled && overflow > 0)
        {
            if (cutThroat)
            {
                foreach (var opp in playerIDs.Where(p => p != playerId))
                {
                    if (playerHits[opp][value] < 3)
                        playerScores[opp] += overflow * value;
                }
            }
            else
            {
                bool opponentClosed = playerIDs.Where(p => p != playerId).All(p => playerHits[p][value] >= 3);
                if (newHits >= 3 && !opponentClosed)
                    playerScores[playerId] += overflow * value;
            }
        }

        // =====================================================
        // EVENT RECORDING
        // =====================================================
        bool wasClose = (currentHits < 3 && newHits == 3);
        int pointsForThisThrow = 0;
        if (pointsEnabled && overflow > 0)
        {
            if (cutThroat)
                pointsForThisThrow = playerIDs.Where(p => p != playerId && playerHits[p][value] < 3).Sum(p => overflow * value);
            else if (!playerIDs.Where(p => p != playerId).All(p => playerHits[p][value] >= 3))
                pointsForThisThrow = overflow * value;
        }

        currentTurnHitEvents.Add(new CricketHitEvent 
        { 
            Number = value, Multiplier = multiplier, ScoreGained = pointsForThisThrow,
            WasClose = wasClose, WasOverflow = overflow > 0, EffectiveMarks = totalMarks
        });

        HandleTurnEnd(playerId);
    }


    // Bot check handled via base Game.TriggerBotCheck()


    // =========================================================
    // TURN MANAGEMENT
    // =========================================================

    private void HandleTurnEnd(Guid playerId)
    {
        bool turnFinished = !currentTurn.HasSpace();
        bool legFinished = DidPlayerWin(playerId);

        if (!turnFinished && !legFinished)
            return;

        FinalizeTurn(playerId);

        if (legFinished)
        {
            Debug.Log($"Cricket: Spieler {playerId} gewinnt!");

            HandleLegWon(playerId);
            StartNewLeg();
            return;
        }

        NextPlayer();
    }

    private CricketTurnResult BuildTurnResult(Guid playerId, Turn turn)
    {
        var result = new CricketTurnResult
        {
            PlayerId = playerId,
            IsBust = false
        };

        result.Hits.AddRange(currentTurnHitEvents);

        // 🔥 FIX: TurnScore korrekt aggregieren
        result.TurnScore = currentTurnHitEvents.Sum(h => h.ScoreGained);

        return result;
    }

    private void NextPlayer()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % playerIDs.Count;
        currentTurn = new Turn(GetCurrentPlayerId());
        GetCurrentLeg().AddTurn(currentTurn);
        TouchActivity();
        TriggerBotCheck();
    }

    // ========================================================
    // TURN STATISTICS
    // ========================================================

    private void RegisterTurnStats(Guid playerId, GameStatsCricket stats, CricketTurnResult result)
    {
        if (stats == null) return;

        int turnIndex = stats.turnCount; // Nutze den aktuellen Turn-Index
        int turnScore = result.TurnScore;
        int totalMarks = 0;
        int totalOverkills = 0;

        // Summiere Marks und Overkills aus den Hit-Events
        foreach (var hit in result.Hits)
        {
            totalMarks += hit.EffectiveMarks;
            if (hit.WasOverflow && hit.ScoreGained > 0)
                totalOverkills += hit.ScoreGained;
        }

        // Aktualisiere TurnStats
        stats.UpdateTurnStats(turnIndex, turnScore, totalMarks, totalOverkills);

        // Erkenne Highscores
        CheckHighscores(playerId, stats, result);

        // Aktualisiere Field Preferences
        CheckFieldPreferences(playerId, stats, result);
    }

    private void CheckHighscores(Guid playerId, GameStatsCricket stats, CricketTurnResult result)
    {
        if (result.Hits.Count == 0) return;

        // 9 Marks: Check wenn genau 9 Marks in dieser Turn
        int totalMarks = result.Hits.Sum(h => h.EffectiveMarks);
        if (totalMarks == 9)
            stats.nineMarkTurns++;

        // White Horse: 3x verschiedene Triple in einer Runde
        if (result.Hits.Count >= 3)
        {
            var triples = result.Hits.Where(h => h.Multiplier == 3).ToList();
            if (triples.Count >= 3)
            {
                var uniqueNumbers = triples.Take(3).Select(h => h.Number).Distinct().Count();
                if (uniqueNumbers == 3)
                    stats.whiteHorseTurns++;
            }
        }

        // Same Triple: 3x dasselbe Triple mit Punkte-Wertung
        if (result.Hits.Count == 3)
        {
            var firstNumber = result.Hits[0].Number;
            var firstMultiplier = result.Hits[0].Multiplier;
            
            bool allSame = result.Hits.All(h => h.Number == firstNumber && h.Multiplier == firstMultiplier);
            if (allSame && firstMultiplier == 3 && result.Hits.All(h => h.ScoreGained > 0))
                stats.sameTripleTurns++;
        }
    }

    private void CheckFieldPreferences(Guid playerId, GameStatsCricket stats, CricketTurnResult result)
    {
        var cricketSettings = GetSettingsAsCricket();
        bool pointsEnabled = cricketSettings == null || cricketSettings.pointsEnabled;

        foreach (var hit in result.Hits)
        {
            int fieldNumber = hit.Number;
            int pointsGiven = hit.ScoreGained;
            bool isNewClosed = hit.WasClose;

            stats.UpdateFieldPreferences(fieldNumber, stats.turnCount, pointsGiven, isNewClosed);
        }
    }


    // =========================================================
    // WIN LOGIC
    // =========================================================

    private bool AllClosed(Guid playerId)
    {
        return numbers.All(n => playerHits[playerId][n] >= 3);
    }

    private bool HasHighestScore(Guid playerId)
    {
        int score = playerScores[playerId];
        return playerIDs.All(p => p == playerId || score >= playerScores[p]);
    }

    private bool HasLowestScore(Guid playerId)
    {
        int score = playerScores[playerId];
        return playerIDs.All(p => p == playerId || score <= playerScores[p]);
    }

    private Guid GetSetWinner(Set set)
    {
        var winners = set.GetLegs()
            .Select(l => l.WinnerPlayerId)
            .Where(w => w != Guid.Empty)
            .ToList();

        var groups = winners
            .GroupBy(p => p)
            .ToDictionary(g => g.Key, g => g.Count());

        int legsToWin = GetSettingsAsCricket().setsAndLegsMode == SetsAndLegs.FirstTo
            ? GetSettingsAsCricket().legCount
            : (GetSettingsAsCricket().legCount / 2) + 1;
        foreach (var kv in groups)
            if (kv.Value >= legsToWin)
                return kv.Key;

        return Guid.Empty;
    }

    public int GetWonLegsTotal(Guid playerId)
    {
        return sets.SelectMany(s => s.GetLegs())
            .Count(l => l.WinnerPlayerId == playerId);
    }

    private bool DidPlayerWin(Guid playerId)
    {
        bool pointsEnabled = GetSettingsAsCricket() == null ||
                            GetSettingsAsCricket().pointsEnabled;

        bool cutThroat = GetSettingsAsCricket() != null &&
                        GetSettingsAsCricket().cutThroatEnabled;

        return AllClosed(playerId) &&
            (pointsEnabled
                ? (cutThroat
                    ? HasLowestScore(playerId)
                    : HasHighestScore(playerId))
                : true);
    }

    /// <summary>Marks (EffectiveMarks-Summe) + Punkte des Turns für Audio-Trigger.</summary>
    protected override void CollectAudioTriggerValues(System.Guid playerId, Turn turn, AudioTriggerValues values)
    {
        base.CollectAudioTriggerValues(playerId, turn, values);

        if (currentTurnHitEvents != null)
        {
            values.marks = currentTurnHitEvents.Sum(h => h.EffectiveMarks);
            values.points = currentTurnHitEvents.Sum(h => h.ScoreGained);
        }
    }

    private void FinalizeTurn(Guid playerId)
    {
        CheckTurnPenalties(playerId, currentTurn);

        var result = BuildTurnResult(playerId, currentTurn);

        var stats = playerStats[playerId] as GameStatsCricket;

        RegisterTurnStats(playerId, stats, result);

        stats?.RegisterTurn();

        OnTurnCompleted?.Invoke(result);

        currentTurnHitEvents = new List<CricketHitEvent>();
    }

    private void StartNewLeg()
    {
        var currentSet = sets[^1];
        Guid setWinner = GetSetWinner(currentSet);

        if (setWinner != Guid.Empty)
        {
            HandleSetWon(setWinner);

            if (IsMatchFinished(setWinner))
            {
                HandleMatchEnd(setWinner);
                // Hier müsste eig. nochmal CheckFieldPreferences aufgerufen werden
                return;
            }

            // Falls das Match nicht vorbei ist: Neues Set erstellen und als aktuelles Set setzen
            currentSet = new Set();
            sets.Add(currentSet);
        }

        // Dieser Block gilt nun für BEIDE Fälle (neues Set oder neues Leg im alten Set)
        var nextLeg = new Leg();
        currentSet.AddLeg(nextLeg);

        IdentifyNextLegStarter();
        ResetToStartingPlayer();

        ResetLegState();
        StartFirstTurn(nextLeg);
        TriggerBotCheck();
    }

    private void StartFirstTurn(Leg leg)
    {
        //currentPlayerIndex = startingPlayerIndex;
        currentTurn = new Turn(GetCurrentPlayerId());
        leg.AddTurn(currentTurn);
    }

    // private void StartNewLegOrEndMatch(Guid legWinner)
    // {

    //     var currentSet = GetCurrentSet();
    //     var setWinner = GetSetWinner(currentSet);

    //     if (setWinner != Guid.Empty)
    //     {
    //         HandleSetWon(setWinner);

    //         if (IsMatchFinished(setWinner))
    //         {
    //             HandleMatchEnd(setWinner);
    //             return;
    //         }

    //         var newSet = new Set();
    //         sets.Add(newSet);
    //         var newLeg = new Leg();
    //         newSet.AddLeg(newLeg);

    //         ResetLegState();
    //         currentTurn = new Turn(GetCurrentPlayerId());
    //         newLeg.AddTurn(currentTurn);
    //         IdentifyNextLegStarter();
    //         TouchActivity();
            
    //         // Nach Set-Wechsel Bot prüfen
    //         TriggerBotCheck();
    //         return;
    //     }

    //     var leg = new Leg();
    //     currentSet.AddLeg(leg);

    //     ResetLegState();
    //     currentTurn = new Turn(GetCurrentPlayerId());
    //     IdentifyNextLegStarter();
    //     leg.AddTurn(currentTurn);
    //     TouchActivity();
    // }

    // =========================================================
    // UNDO (last dart)
    // =========================================================

    public void Undo()
    {
        if (IsEmpty())
            return;
            
        // 1. Basis-Logik nutzen (Löscht leere Legs/Sets, setzt Winner zurück, dekrementiert totalLegsCount/Won etc.)
        var undoInfo = base.UndoBase();

        // 2. Spezial-Logik für Match-Ende
        // if (undoInfo.MatchWinnerUndone)
        // {
        //     base.UndoMatchEnd();
        // }

        // 3. Den eigentlichen Wurf löschen
        // Hinweis: Block 3 aus deinem alten Code (Sieg-Zähler korrigieren) ist jetzt komplett weg, 
        // da base.UndoBase() das über UndoLegPlayed/Won erledigt.

        var leg = GetCurrentLeg();
        var allTurns = leg.GetTurns();
        
        // Den letzten Turn finden, der tatsächlich Würfe enthält
        Turn lastTurn = allTurns.LastOrDefault(t => t.GetThrows().Count > 0);
        
        if (lastTurn != null)
        {
            // Custom-Buchungen des Turns stornieren (Nachwurf + Re-Finalize
            // buchen danach sauber neu -> kein Doppelzählen).
            if (lastTurn.RecordedPenalties != null)
            {
                foreach (var rec in lastTurn.RecordedPenalties)
                {
                    if (!string.IsNullOrEmpty(rec.CustomPenaltyId))
                        RemoveCustomPenalty(rec.PlayerId, rec.CustomPenaltyId, rec.CustomCost);
                }
                lastTurn.RecordedPenalties.Clear();
            }

            // Custom-Instant-Buchungen der verbleibenden Würfe still neu buchen
            // (der letzte Wurf fliegt gleich; seine Buchung ist oben storniert).
            var remainingThrows = lastTurn.GetThrows();
            for (int i = 0; i < remainingThrows.Count - 1; i++)
                CheckCustomInstantPenalties(lastTurn.PlayerId, remainingThrows[i], lastTurn, true);

            // Den letzten Wurf entfernen
            lastTurn.RemoveLastThrow();

            // Pointer synchronisieren
            currentPlayerIndex = playerIDs.IndexOf(lastTurn.PlayerId);
            currentTurn = lastTurn;

            // Leere Folge-Turns im Leg löschen
            int lastTurnIndex = allTurns.IndexOf(lastTurn);
            if (lastTurnIndex < allTurns.Count - 1)
            {
                allTurns.RemoveRange(lastTurnIndex + 1, allTurns.Count - (lastTurnIndex + 1));
            }
        }

        // 4. ALLES NEU BERECHNEN
        // WICHTIG: Da base.UndoBase bereits die totalLegsWon etc. korrigiert hat,
        // stellt RebuildEverything jetzt den korrekten Spielfeld-Zustand (Marks/Scores) wieder her.
        RebuildEverything();
        
        TouchActivity();
    }

    private void RebuildEverything()
    {
        ResetLegState(); 
        //InitializeStats();
        currentTurnHitEvents.Clear();

        var settings = GetSettingsAsCricket();
        var leg = GetCurrentLeg();
        var activeTurn = currentTurn; 

        foreach (var turn in leg.GetTurns())
        {
            Guid pid = turn.PlayerId;
            foreach (var thr in turn.GetThrows())
            {
                // 1. Initialisierung für diesen Wurf
                int totalMarks = 0;
                int pts = 0;

                // 2. Nur wenn es ein Board-Treffer ist, berechnen wir Marks und Punkte
                if (thr.HitType == HitType.Board)
                {
                    int value = thr.Value;
                    int mult = ConvertMultiplier(thr.Multiplier);
                    if (value == 50) { value = 25; mult = 2; }

                    // Prüfen, ob es eine gültige Cricket-Zahl ist (15-20, Bull)
                    if (numbers.Contains(value) && !(value == 25 && thr.Multiplier == DartMultiplier.Triple))
                    {
                        int hitsBefore = playerHits[pid][value];
                        int overflow = Math.Max(0, (hitsBefore + mult) - 3);
                        int newHitsClamped = Math.Min(3, hitsBefore + mult);

                        bool othersOpen = playerIDs.Where(p => p != pid).Any(p => playerHits[p][value] < 3);
                        totalMarks = (newHitsClamped - hitsBefore) + (othersOpen ? overflow : 0);

                        if (settings.pointsEnabled && overflow > 0)
                        {
                            if (settings.cutThroatEnabled)
                                pts = playerIDs.Where(p => p != pid && playerHits[p][value] < 3).Sum(p => overflow * value);
                            else if (playerIDs.Where(p => p != pid).Any(p => playerHits[p][value] < 3))
                                pts = overflow * value;
                        }

                        // State Update für Spiellogik
                        playerHits[pid][value] = newHitsClamped;
                        if (pts > 0)
                        {
                            if (settings.cutThroatEnabled)
                                foreach (var opp in playerIDs.Where(p => p != pid && playerHits[p][value] < 3)) playerScores[opp] += (overflow * value);
                            else
                                playerScores[pid] += pts;
                        }

                        // Events für UI (nur wenn aktiver Turn)
                        if (turn == activeTurn)
                        {
                            currentTurnHitEvents.Add(new CricketHitEvent {
                                Number = value, Multiplier = mult, ScoreGained = pts,
                                WasClose = (hitsBefore < 3 && newHitsClamped == 3),
                                WasOverflow = overflow > 0, EffectiveMarks = totalMarks
                            });
                        }
                    }
                }

                // 3. WICHTIG: JEDEN Wurf in den Stats registrieren (egal ob Hit, Miss, Wall)
                // Bei ungültigem Treffer werden einfach 0 Marks gebucht, aber der Dart gezählt.
                (playerStats[pid] as GameStatsCricket)?.RegisterThrow(thr, totalMarks);
            }

            // 4. Turn-Abschluss (MPR)
            if (turn != activeTurn && (turn.GetThrows().Count == 3 || turn == leg.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0 && leg.WinnerPlayerId != null)))
            {
                var stats = playerStats[pid] as GameStatsCricket;
                stats?.RegisterTurn();
                
                // Auch neue Statistiken aufbauen falls während Rebuild
                if (turn == activeTurn || turn == leg.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0 && leg.WinnerPlayerId != null))
                {
                    // Während Rebuild können wir nicht die CricketTurnResult rekonstruieren,
                    // daher nur einfache Max MPR Aktualisierung
                    stats?.UpdateMaxMPR();
                }
            }
        }
        
        // Rebuild der Highscores und Preferences aus den aktuellen Spielfeld-Zuständen
        foreach (var pid in playerIDs)
        {
            var stats = playerStats[pid] as GameStatsCricket;
            if (stats != null)
            {
                // Preferences aus Spielfeld neu aufbauen
                for (int i = 0; i < leg.GetTurns().Count; i++)
                {
                    var turn = leg.GetTurns()[i];
                    if (turn.PlayerId == pid)
                    {
                        // Hierfür bräuchten wir die CricketTurnResult Daten, die wir nicht mehr haben
                        // Deshalb lassen wir die komplexen Statistiken beim Rebuild weg
                    }
                }
            }
        }
    }

    public override void InitializeAfterLoad()
    {
        base.InitializeAfterLoad();

        sets ??= new List<Set>();
        playerIDs ??= new List<Guid>();

        if (sets.Count == 0)
        {
            var set = new Set();
            var leg = new Leg();
            set.AddLeg(leg);
            sets.Add(set);
        }

        // currentTurn validieren / rekonstruieren
        var legNow = GetCurrentLeg();

        if (currentTurn == null ||
            currentTurn.PlayerId != GetCurrentPlayerId() ||
            !legNow.GetTurns().Contains(currentTurn))
        {
            currentTurn = GetCurrentTurnOfPlayer(GetCurrentPlayerId());

            if (currentTurn == null)
            {
                currentTurn = new Turn(GetCurrentPlayerId());
                legNow.AddTurn(currentTurn);
            }
        }

        // Derived state vollständig rekonstruieren
        RebuildEverything();
    }


    // =========================================================
    // PENALTIES
    // =========================================================

    /// <summary>
    /// Remaining-Auswertung über Totals: normal das eigene Total,
    /// Cutthroat die Gegner-Totals (Buchung an den Total-Inhaber —
    /// wie die frühere Schnapszahl-Regel). Vorher-Stand aus dem
    /// TurnStartScores-Snapshot des Turns (Übergangsregel).
    /// </summary>
    protected override Dictionary<Guid, (int before, int after)> GetRemainingTotals(Guid playerId, Turn turn)
    {
        if (turn == null)
            return null;

        var cricketSettings = GetSettingsAsCricket();
        bool cutThroat = cricketSettings != null && cricketSettings.cutThroatEnabled;

        var result = new Dictionary<Guid, (int, int)>();
        var owners = cutThroat
            ? playerIDs.Where(p => p != playerId).ToList()
            : new List<Guid> { playerId };

        foreach (var owner in owners)
        {
            int after = playerScores.TryGetValue(owner, out var sc) ? sc : 0;
            int before = after;
            if (turn.TurnStartScores != null && turn.TurnStartScores.TryGetValue(owner, out var b))
                before = b;
            result[owner] = (before, after);
        }

        return result;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private int ConvertMultiplier(DartMultiplier m)
    {
        switch (m)
        {
            case DartMultiplier.Single: return 1;
            case DartMultiplier.Double: return 2;
            case DartMultiplier.Triple: return 3;
            default: return 1;
        }
    }

    private int ApplyScore(
    Guid playerId,
    int value,
    int overflow,
    bool cutThroat,
    bool allOpponentsClosed)
    {
        if (overflow <= 0)
            return 0;

        if (cutThroat)
        {
            bool scored = false;

            foreach (var opp in playerIDs.Where(p => p != playerId))
            {
                if (playerHits[opp][value] < 3)
                {
                    playerScores[opp] += overflow * value;
                    scored = true;
                }
            }

            return scored ? overflow * value : 0;
        }
        else
        {
            if (!allOpponentsClosed)
            {
                int score = overflow * value;
                playerScores[playerId] += score;
                return score;
            }
        }

        return 0;
    }

    // =========================================================
    // STATISTICS FOR UI
    // =========================================================

    public float GetLegWinPercent(Guid playerId)
    {
        int played = playerStats.TryGetValue(playerId, out var stats) ? stats.totalLegsCount : 0;
        int won = GetWonLegsTotal(playerId);
        return played > 0 ? (float)won / played * 100 : 0;
    }

    public float GetHitTypePercent(Guid playerId, DartMultiplier multiplier)
    {
        var turns = GetAllTurns(playerId);
        int totalThrows = turns.Sum(t => t.GetThrows().Count);
        int matchingThrows = turns.Sum(t => t.GetThrows().Count(th => th.Multiplier == multiplier));
        return totalThrows > 0 ? (float)matchingThrows / totalThrows * 100 : 0;
    }

    public int GetTotalHits(Guid playerId)
    {
        return GetNumbers().Sum(num => GetHits(playerId, num));
    }

    public float GetHitPercent(Guid playerId)
    {
        int totalDarts = GetTotalDarts(playerId);
        if (totalDarts == 0)
            return 0;

        var numbersSet = new HashSet<int>(GetNumbers());
        int hitDarts = GetAllTurns(playerId)
            .SelectMany(t => t.GetThrows())
            .Count(th => th.HitType == HitType.Board
                && numbersSet.Contains(th.Value)
                && !(th.Value == 25 && th.Multiplier == DartMultiplier.Triple));

        return (float)hitDarts / totalDarts * 100;
    }

    protected override void InitializeStats()
    {
        if (playerStats != null)
            playerStats.Clear();
        playerStats = new Dictionary<Guid, GameStats>();
        foreach (var pid in playerIDs)
        {
            playerStats[pid] = new GameStatsCricket(pid);
        }
    }

    private void RebuildPlayerStats()
    {
        // reset stats
        foreach (var pid in playerIDs)
        {
            if (playerStats[pid] is GameStatsCricket stats)
            {
                stats.turnCount = 0;
                stats.markCount = 0;
                stats.marksPerRound = 0;
            }
        }

        // alle Turns neu durchgehen
        foreach (var set in sets)
        {
            foreach (var leg in set.GetLegs())
            {
                foreach (var turn in leg.GetTurns())
                {
                    var pid = turn.PlayerId;

                    if (!(playerStats[pid] is GameStatsCricket stats))
                        continue;

                    // nur volle Turns zählen (wie bei MPR üblich)
                    if (turn.GetThrows().Count == 0)
                        continue;

                    int marks = 0;

                    foreach (var thr in turn.GetThrows())
                    {
                        if (thr.HitType != HitType.Board)
                            continue;

                        int value = thr.Value;
                        int mult = ConvertMultiplier(thr.Multiplier);

                        if (value == 50)
                        {
                            value = 25;
                            mult = 2;
                        }

                        if (value == 25 && thr.Multiplier == DartMultiplier.Triple)
                            continue;

                        if (!numbers.Contains(value))
                            continue;

                        int hitsBefore = playerHits[pid][value];

                        bool locked = playerIDs.All(p => playerHits[p][value] >= 3);

                        if (locked)
                            continue;

                        int newHits = hitsBefore + mult;
                        int effectiveHits = Mathf.Min(newHits, 3);

                        marks += Mathf.Max(0, effectiveHits - hitsBefore);
                    }

                    stats.turnCount++;
                    stats.markCount += marks;
                }
            }
        }

        // MPR neu berechnen
        foreach (var pid in playerIDs)
        {
            if (playerStats[pid] is GameStatsCricket stats)
            {
                if (stats.turnCount > 0)
                    stats.marksPerRound = (float)Math.Round((float)stats.markCount / stats.turnCount, 2);
                else
                    stats.marksPerRound = 0;
            }
        }
    }

    public override void CalculatePlayerStatsOnSave()
    {
        // Base recomputet Legs/Sets/Games/Throws (idempotent).
        base.CalculatePlayerStatsOnSave();

        if (playerStats == null || playerIDs == null)
            return;

        var cricketSettings = GetSettingsAsCricket();
        bool pointsEnabled = cricketSettings == null || cricketSettings.pointsEnabled;
        bool cutThroat = cricketSettings != null && cricketSettings.cutThroatEnabled;

        // Cricket-Felder resetten (Base setzt nur Legs/Sets/Throws zurück).
        // Dart-Zähler (totalThrows/Double/Triple) und Penalties bleiben von der Base.
        foreach (var pid in playerIDs)
        {
            if (playerStats.TryGetValue(pid, out var s) && s is GameStatsCricket cs)
                ResetCricketStatsForRebuild(cs);
        }

        // Chronologische Simulation über alle Sets/Legs/Turns.
        // playerHits werden pro Leg zurückgesetzt (wie ResetLegState im Live-Spiel),
        // damit Marks/Overflow/Scores exakt wie in AddThrow berechnet werden.
        foreach (var set in sets)
        {
            foreach (var leg in set.GetLegs())
            {
                var simHits = playerIDs.ToDictionary(pid => pid, _ => numbers.ToDictionary(n => n, _ => 0));

                foreach (var turn in leg.GetTurns())
                {
                    if (turn.GetThrows().Count == 0)
                        continue;

                    Guid pid = turn.PlayerId;
                    if (!playerStats.TryGetValue(pid, out var s) || s is not GameStatsCricket stats)
                        continue;

                    var result = new CricketTurnResult { PlayerId = pid };
                    int turnScore = 0;

                    foreach (var thr in turn.GetThrows())
                    {
                        var sim = SimulateCricketThrow(thr, pid, simHits, pointsEnabled, cutThroat);
                        if (sim == null)
                            continue;

                        turnScore += sim.ScoreGained;

                        result.Hits.Add(new CricketHitEvent
                        {
                            Number = sim.Number,
                            Multiplier = sim.Multiplier,
                            ScoreGained = sim.ScoreGained,
                            WasClose = sim.WasClose,
                            WasOverflow = sim.WasOverflow,
                            EffectiveMarks = sim.EffectiveMarks
                        });
                    }

                    // 🔥 FIX: TurnScore korrekt aggregieren
                    result.TurnScore = turnScore;

                    // Nutzt bewusst die Live-Methoden (UpdateTurnStats, Highscores,
                    // FieldPreferences), damit Aggregate und Time-Ranges konsistent sind.
                    // Marks werden hier direkt addiert: live passiert das pro Dart via
                    // RegisterThrow (hier nicht aufrufbar, da die Base die Darts bereits
                    // zählt) – vor RegisterTurn, damit die MPR stimmt.
                    RegisterTurnStats(pid, stats, result);
                    stats.markCount += result.Hits.Sum(h => h.EffectiveMarks);
                    stats.RegisterTurn();
                }
            }
        }
    }

    private void ResetCricketStatsForRebuild(GameStatsCricket stats)
    {
        if (stats == null)
            return;

        stats.turnCount = 0;
        stats.markCount = 0;
        stats.marksPerRound = 0f;
        stats.maxMPR = 0f;

        stats.turnScores.Clear();
        stats.turnMarks.Clear();
        stats.turnOverkills.Clear();

        // tripleDigitCount (Legacy) wird nicht mehr rebuildet, sondern aus der
        // persistierten Zeile restauriert (alte Spiele behalten ihre Werte).
        stats.nineMarkTurns = 0;
        stats.whiteHorseTurns = 0;
        stats.sameTripleTurns = 0;

        stats.firstClosedField = -1;
        stats.lastClosedField = -1;
        stats.maxPointsField = -1;
        stats.maxPointsFieldValue = 0;
    }

    /// <summary>
    /// Simuliert einen Wurf wie AddThrow (Marks/Overflow/Scoring), ohne State zu mutieren.
    /// Gibt null zurück für ungültige Würfe (Miss/Wall/falsche Zahl) – wie im Live-Spiel
    /// erzeugen diese kein Hit-Event (nur 0 Marks via RegisterThrow, was die Base abdeckt).
    /// </summary>
    private SimulatedCricketThrow SimulateCricketThrow(
        Throw thr,
        Guid playerId,
        Dictionary<Guid, Dictionary<int, int>> simHits,
        bool pointsEnabled,
        bool cutThroat)
    {
        if (thr.HitType != HitType.Board)
            return null;

        int value = thr.Value;
        int mult = ConvertMultiplier(thr.Multiplier);
        if (value == 50) { value = 25; mult = 2; }

        if ((value == 25 && thr.Multiplier == DartMultiplier.Triple) || !numbers.Contains(value))
            return null;

        int hitsBefore = simHits[playerId][value];
        int newHits = hitsBefore + mult;
        int overflow = Math.Max(0, newHits - 3);
        int newHitsClamped = Math.Min(3, newHits);

        bool othersOpen = playerIDs.Where(p => p != playerId).Any(p => simHits[p][value] < 3);
        int totalMarks = (newHitsClamped - hitsBefore) + (othersOpen ? overflow : 0);

        int pts = 0;
        var dealtPoints = new Dictionary<Guid, int>();
        if (pointsEnabled && overflow > 0)
        {
            if (cutThroat)
            {
                // Punkte landen bei den offenen Gegnern (live-identisch) + pro
                // Gegner merken (für Schnapszahl-Übergangsregel im Rebuild).
                foreach (var opp in playerIDs.Where(p => p != playerId && simHits[p][value] < 3))
                {
                    int given = overflow * value;
                    pts += given;
                    dealtPoints[opp] = dealtPoints.TryGetValue(opp, out var d) ? d + given : given;
                }
            }
            else if (playerIDs.Where(p => p != playerId).Any(p => simHits[p][value] < 3))
                pts = overflow * value;
        }

        simHits[playerId][value] = newHitsClamped;

        return new SimulatedCricketThrow
        {
            Number = value,
            Multiplier = mult,
            ScoreGained = pts,
            WasClose = hitsBefore < 3 && newHitsClamped == 3,
            WasOverflow = overflow > 0,
            EffectiveMarks = totalMarks,
            DealtPoints = dealtPoints
        };
    }

    private class SimulatedCricketThrow
    {
        public int Number;
        public int Multiplier;
        public int ScoreGained;
        public bool WasClose;
        public bool WasOverflow;
        public int EffectiveMarks;
        public Dictionary<Guid, int> DealtPoints;
    }

    public GameStatsCricket GetPlayerStats(Guid playerId)
    {
        if (!playerStats.TryGetValue(playerId, out var stats))
            return null;

        return stats as GameStatsCricket;
    }
    
    private void ResetForReplay()
    {
        playerHits = new Dictionary<Guid, Dictionary<int, int>>();
        playerScores = new Dictionary<Guid, int>();

        InitializeStats(); // nur Container, keine Logik

        foreach (var pid in playerIDs)
        {
            playerScores[pid] = 0;
            playerHits[pid] = numbers.ToDictionary(n => n, n => 0);
        }

        currentTurnHitEvents = new List<CricketHitEvent>();
    }

    private void ReplayGame()
    {
        ResetForReplay();
        
        foreach (Set set in sets)
        {
            foreach (Leg leg in set.GetLegs())
            {
                foreach (Turn turn in leg.GetTurns())
                {
                    foreach (Throw t in turn.GetThrows())
                    {
                        AddThrow(t);
                    }
                }
            }
        }
    }   
}


public class CricketTurnResult
{
    public Guid PlayerId;

    public int TurnScore;

    public bool IsBust; // optional, falls du sowas hast

    public List<CricketHitEvent> Hits = new();
}

public class CricketHitEvent
{
    public int Number;        // z.B. 20, 19, 25
    public int Multiplier;    // 1,2,3
    public int ScoreGained;   // tatsächliche Punkte
    public bool WasClose;     // hat Feld geschlossen?
    public bool WasOverflow;  // overflow scoring
    public int EffectiveMarks;
}