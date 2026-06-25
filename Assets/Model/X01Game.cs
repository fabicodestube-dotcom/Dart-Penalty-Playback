using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System;


[System.Serializable]
public class X01Game : Game
{
    // =========================================================
    // STATE
    // =========================================================

    [JsonProperty] private Turn currentTurn;
    [JsonProperty] private Dictionary<Guid, int> playerScores = new Dictionary<Guid, int>();
    [JsonProperty] private Dictionary<Guid, bool> playerCheckedIn = new();
    
    [JsonProperty] private bool suddenDeathTriggered = false;


    // =========================================================
    // EVENTS
    // =========================================================


    [JsonIgnore] public System.Action<Guid, string> OnCheckoutAvailable;
    [JsonIgnore] public System.Action<Guid> OnFourTwenty;

    [JsonIgnore] public System.Action OnSuddenDeath;



    // =========================================================
    // CONSTRUCTION / INITIALIZATION
    // =========================================================

    public X01Game(Guid id, X01GameSettings settings, List<Guid> playerIDs)
    {
        gameMode = GameMode.X01;
        InitializeTimestampsOnCreate();
        
        this.id = id;
        this.settings = settings;
        this.playerIDs = playerIDs;

        InitializeStats();
        //InitializeLegsPlayedAtStart();

        foreach (var pid in playerIDs)
        {
            playerScores[pid] = settings.pointTarget;

            playerCheckedIn[pid] =
                GetSettingsAsX01().checkinType == CheckinType.StraightIn;
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

    public override void InitializeAfterLoad()
    {
        base.InitializeAfterLoad();

        // Defensive null protection
        sets ??= new List<Set>();
        playerScores ??= new Dictionary<Guid, int>();
        playerIDs ??= new List<Guid>();

        playerCheckedIn ??= new Dictionary<Guid, bool>();

        foreach (var pid in playerIDs)
        {
            if (!playerCheckedIn.ContainsKey(pid))
            {
                playerCheckedIn[pid] =
                    GetSettingsAsX01().checkinType == CheckinType.StraightIn;
            }
        }

        // Ensure minimum structure exists
        if (sets.Count == 0)
        {
            Debug.LogWarning("No sets found -> creating default structure");

            var set = new Set();
            var leg = new Leg();

            set.AddLeg(leg);
            sets.Add(set);
        }

        // Ensure every player has a score,
        // but NEVER overwrite serialized scores
        int startScore = GetSettingsAsX01()?.pointTarget ?? 0;

        foreach (var pid in playerIDs)
        {
            if (!playerScores.ContainsKey(pid))
            {
                Debug.LogWarning($"Missing score for player {pid}, creating with {startScore}");
                playerScores[pid] = startScore;
            }
        }

        var legNow = GetCurrentLeg();

        // Rebuild currentTurn ONLY if invalid/missing
        if (currentTurn == null ||
            currentTurn.PlayerId != GetCurrentPlayerId() ||
            !legNow.GetTurns().Contains(currentTurn))
        {
            Debug.Log("currentTurn invalid -> repairing");

            var existing = GetCurrentTurnOfPlayer(GetCurrentPlayerId());

            if (existing != null)
            {
                currentTurn = existing;
            }
            else
            {
                currentTurn = new Turn(GetCurrentPlayerId());
                legNow.AddTurn(currentTurn);
            }
        }
        TriggerCheckout();
    }


    // =========================================================
    // BASIC SCORE ACCESS
    // =========================================================

    public int GetScore(Guid playerId) => playerScores[playerId];

    public bool IsCheckedIn(Guid playerId)
    {
        return playerCheckedIn[playerId];
    }

    // =========================================================
    // PLAYER FLOW
    // =========================================================

    public void NextPlayer()
    {
        Guid oldPlayerId = GetCurrentPlayerId();

        if (currentTurn != null && currentTurn.GetThrows().Count > 0)
        {
            if (!currentTurn.IsBust)
            {
                if (playerStats.TryGetValue(oldPlayerId, out var stats))
                {
                    ((GameStatsX01) stats).RegisterTurn(currentTurn);
                }
            }
        }

        currentPlayerIndex = (currentPlayerIndex + 1) % playerIDs.Count;
        currentTurn = new Turn(GetCurrentPlayerId());
        GetCurrentLeg().AddTurn(currentTurn);

        TriggerCheckout();
        TouchActivity();
        TriggerBotCheck();
    }


    // =========================================================
    // MAIN GAME LOGIC (THROW HANDLING)
    // =========================================================

    public void AddThrow(Throw t)
    {
        Guid playerId = GetCurrentPlayerId();


        if (!playerCheckedIn[playerId])
        {
            Debug.Log("Player war noch nicht eingecheckt");

            // Falls es der erste Dart der Aufnahme ist,
            // merken wir uns den Score vor dem gesamten Turn
            if (currentTurn.GetThrows().Count == 0)
            {
                currentTurn.ScoreBeforeTurn = playerScores[playerId];
            }

            currentTurn.AddThrow(t);

            if (IsValidCheckin(t))
            {
                Debug.Log("Checkin erfolgt");

                currentTurn.CheckinThrowIndex =
                    currentTurn.GetThrows().Count - 1;

                playerCheckedIn[playerId] = true;

                // Check-In Dart zählt sofort voll
                playerScores[playerId] -= t.GetScore();

                // WICHTIG:
                // ScoreBeforeTurn NICHT verändern!
                // Er bleibt der Score VOR dem ersten Dart der Aufnahme.
            }

            // Aufnahme beendet?
            if (!currentTurn.HasSpace())
            {
                OnTurnCompleted?.Invoke(playerId, currentTurn);
                NextPlayer();
            }

            TriggerCheckout();
            TouchActivity();

            return;
        }

        // 1. Initialisiere den Turn-Snapshot beim ALLERERSTEN Dart der Aufnahme
        if (currentTurn.GetThrows().Count == 0)
            currentTurn.ScoreBeforeTurn = playerScores[playerId];

        // 2. Berechne die Restpunkte, auf die dieser AKTULLE Dart geworfen wird
        // (Punkte vor dem Turn minus das, was in DIESEM Turn bisher getroffen wurde)
        int pointsBeforeThisThrow = currentTurn.ScoreBeforeTurn - currentTurn.GetTurnScore(includeBustDarts: true);

        // 3. JETZT prüfen wir, ob dieser spezifische Pfeil auf ein Checkout geworfen werden kann
        bool isCheckable = IsCheckable(pointsBeforeThisThrow, GetSettingsAsX01().checkoutType);
        if (isCheckable)
        {
            RegisterCheckoutAttempt(playerId);
        }

        // 4. Füge den Wurf dem Turn hinzu (wichtig für Penalties und spätere Berechnungen)
        currentTurn.AddThrow(t);

        // 5. Berechne den neuen Punktestand NACH dem Wurf
        int newScore = currentTurn.ScoreBeforeTurn - currentTurn.GetTurnScore(includeBustDarts: true);

        var x01Settings = settings as X01GameSettings;

        // 6. Bust-Validierung
        bool bust = false;

        if (newScore < 0)
            bust = true;

        if (newScore == 1 && x01Settings.checkoutType != CheckoutType.Single)
            bust = true;

        if (newScore == 0 && x01Settings.checkoutType == CheckoutType.Double && !t.IsDouble())
            bust = true;

        // (Zusätzliche Bust-Prüfung für deine neue Triple-Regel, falls gewünscht)
        if (newScore == 0 && x01Settings.checkoutType == CheckoutType.Triple && !t.IsTriple())
            bust = true;

        if (t.HitType == HitType.Wall)
            AddPenalty(playerId, PenaltyType.Wall, currentTurn);
        else if (t.HitType == HitType.Ceiling)
            AddPenalty(playerId, PenaltyType.Ceiling, currentTurn);

        if (bust)
        {
            currentTurn.MarkAsBust();
            Debug.Log($"Bust! Spieler {playerId}");

            OnTurnCompleted?.Invoke(playerId, currentTurn);

            // Bei Bust fällt der Score auf den Stand VOR dem gesamten Turn zurück
            playerScores[playerId] = currentTurn.ScoreBeforeTurn;

            NextPlayer();
            TouchActivity();
            return;
        }

        playerScores[playerId] = newScore;

        if (newScore == 0)
        {
            if (playerStats.TryGetValue(playerId, out var stats))
            {
                var sX01 = (GameStatsX01)stats;
                sX01.RegisterTurn(currentTurn); 
                sX01.RegisterCheckout(currentTurn);
            }

            CheckTurnPenalties(playerId, currentTurn);
            //OnTurnCompleted?.Invoke(playerId, currentTurn);

            HandleLegWon(playerId);
            StartNewLeg();
            TouchActivity();
            return;
        }

        TriggerCheckout();

        if (!currentTurn.HasSpace())
        {
            CheckTurnPenalties(playerId, currentTurn);
            OnTurnCompleted?.Invoke(playerId, currentTurn);

            if (newScore == 420)
            {
                OnFourTwenty?.Invoke(playerId);
            }

            CheckSuddenDeath();

            NextPlayer();
            TouchActivity();
        }
    }



    // =========================================================
    // LEG / SET FLOW
    // =========================================================

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

        ResetScores();
        StartFirstTurn(nextLeg);
        TriggerBotCheck();
    }



    // =========================================================
    // UNDO SYSTEM
    // =========================================================

    public void Undo()
    {
        bool isEmpty = IsEmpty();

        Debug.Log("Leer: " + isEmpty);

        if (isEmpty)
            return;

        // 1. Basis-Struktur zurückrollen (Legs, Sets, Win-Stats & Counter)
        var undoInfo = base.UndoBase();

        // 3. X01-spezifische Korrekturen bei Sieg-Rücknahme
        if (undoInfo.LegWinnerUndone && undoInfo.PreviousWinnerId != null)
        {
            if (playerStats.TryGetValue(undoInfo.PreviousWinnerId, out var s))
            {
                // Nur das Checkout unregistrieren (Counters wurden in base erledigt)
                var finishingTurn = undoInfo.UndoneLeg?.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0);
                if (finishingTurn != null)
                {
                    ((GameStatsX01)s).UnregisterCheckout(finishingTurn);
                }
            }
        }

        // 4. Den eigentlichen Turn-Inhalt (Score & Averages) zurücksetzen
        var leg = GetCurrentLeg();

        // Leeren Platzhalter-Turn am Ende entfernen
        var turns = leg.GetTurns();

        if (turns.Count > 0)
        {
            Turn lastTurnInLeg = turns[^1];

            if (lastTurnInLeg.GetThrows().Count == 0 && !lastTurnInLeg.IsBust)
            {
                turns.RemoveAt(turns.Count - 1);
            }
        }

        Turn lastTurn = leg.GetTurns().LastOrDefault(t => t.GetThrows().Count > 0 || t.IsBust);

        if (lastTurn != null)
        {
            Guid undoPlayerId = lastTurn.PlayerId;

            if (playerStats.TryGetValue(undoPlayerId, out var stats))
            {
                // Averages/Darts-Zähler im X01-Stat-Objekt korrigieren
                ((GameStatsX01)stats).UnregisterTurn(lastTurn);
                
                if (IsSchnapszahl(lastTurn.ScoreBeforeTurn))
                {
                    RemovePenalty(undoPlayerId, PenaltyType.Schnapszahl);
                }
            }

            // --- LOGIK FÜR CHECKOUT ATTEMPT UNDO ---
            // 1. Wir merken uns, ob der Turn vor dem Undo ein Bust war
            bool wasBustBeforeUndo = lastTurn.IsBust;

            // 2. Jetzt löschen wir den letzten Wurf aus dem Turn-Objekt
            Throw removedThrow = lastTurn.RemoveLastThrow();
            
            // 2a. Prüfen, ob der entfernte Dart ein Check-In Dart war
            if (lastTurn.CheckinThrowIndex >= 0)
            {
                if (lastTurn.GetThrows().Count == lastTurn.CheckinThrowIndex)
                {
                    playerCheckedIn[undoPlayerId] = false;
                    lastTurn.CheckinThrowIndex = -1;
                }
            }
            
            lastTurn.ClearBust();

            // 3. Punktestand berechnen, den der Spieler VOR diesem gelöschten Wurf hatte
            // (Punkte vor dem Turn minus die Summe aller Darts, die davor in diesem Turn geworfen wurden)
            int pointsBeforeThisThrow = lastTurn.ScoreBeforeTurn - lastTurn.GetTurnScore(includeBustDarts: true);


            var x01Settings = GetSettingsAsX01();
            // 4. War dieser gelöschte Wurf ein valider Checkout-Versuch?
            // Wichtig: Wenn es ein Bust war, stand der Spieler mathematisch IMMER auf einem checkbaren Feld.
            if (wasBustBeforeUndo || IsCheckable(pointsBeforeThisThrow, x01Settings.checkoutType))
            {
                UnregisterCheckoutAttempt(undoPlayerId);
            }
            // ----------------------------------------

            // Score im Game-State für den Spieler wiederherstellen
            playerScores[undoPlayerId] = lastTurn.ScoreBeforeTurn - lastTurn.GetTurnScore();

            CheckSuddenDeathAfterUndo();
            currentTurn = lastTurn;
        }

        TriggerCheckout();
        TouchActivity();
    }




    // =========================================================
    // QUERY API (STATS)
    // =========================================================
    public List<Turn> GetAllTurns(Guid playerId)
    {
        var turns = new List<Turn>();

        foreach (var set in sets)
            foreach (var leg in set.GetLegs())
                turns.AddRange(leg.GetTurns().Where(t => t.PlayerId == playerId));

        return turns;
    }

    public override void CalculatePlayerStatsOnSave()
    {
        base.CalculatePlayerStatsOnSave();

        // Hier X01-spezifische Kacke
    } 


    // =========================================================
    // WIN RESOLUTION HELPERS
    // =========================================================

    private Guid GetLegWinner(Leg leg) => leg.WinnerPlayerId;

    private Guid GetSetWinner(Set set)
    {
        var legWinners = set.GetLegs()
            .Select(l => GetLegWinner(l))
            .Where(w => w != null)
            .ToList();

        var groups = legWinners
            .GroupBy(p => p)
            .ToDictionary(g => g.Key, g => g.Count());

        int legsToWin = (GetSettingsAsX01().legCount / 2) + 1;

        foreach (var kv in groups)
            if (kv.Value >= legsToWin)
                return kv.Key;

        return Guid.Empty;
    }


    // =========================================================
    // TURN MANAGEMENT HELPERS
    // =========================================================

    private void ResetScores()
    {
        foreach (var pid in playerIDs)
        {
            playerScores[pid] = GetSettingsAsX01().pointTarget;

            playerCheckedIn[pid] =
                GetSettingsAsX01().checkinType == CheckinType.StraightIn;
        }
    }

    private void StartFirstTurn(Leg leg)
    {
        //currentPlayerIndex = startingPlayerIndex;
        currentTurn = new Turn(GetCurrentPlayerId());
        leg.AddTurn(currentTurn);

        TriggerCheckout();
    }

    private bool IsValidCheckin(Throw t)
    {
        switch (GetSettingsAsX01().checkinType)
        {
            case CheckinType.StraightIn:
                return true;

            case CheckinType.DoubleIn:
                return t.IsDouble();

            case CheckinType.MasterIn:
                return t.IsDouble()
                    || t.IsTriple();
        }

        return true;
    }

    // =========================================================
    // PENALTY SYSTEM (X01 EXTENSIONS)
    // =========================================================

    private new void CheckTurnPenalties(Guid playerId, Turn turn)
    {
        base.CheckTurnPenalties(playerId, turn);
        CheckSchnapszahl(playerId, turn);
    }

    private void CheckSchnapszahl(Guid playerId, Turn turn)
    {
        int score = playerScores[playerId];

        if (score < 100) return;

        string s = score.ToString();

        if (IsSchnapszahl(score))
        {
            if (playerStats.TryGetValue(playerId, out var stats))
            {
                stats.AddPenalty(PenaltyType.Schnapszahl);
            }

            OnPenaltyTriggered?.Invoke(playerId, PenaltyType.Schnapszahl, turn);
        }
    }

    private bool IsSchnapszahl(int value)
    {
        // Schnapszahlen müssen laut deiner Logik mindestens 100 sein (111, 222, etc.)
        if (value < 100) return false;

        string s = value.ToString();
        // Prüft, ob alle Zeichen im String identisch sind
        return s.Distinct().Count() == 1;
    }


    // =========================================================
    // CHECKOUT SYSTEM
    // =========================================================

    private void TriggerCheckout()
    {
        Guid playerId = GetCurrentPlayerId();
        int score = playerScores[playerId];
        int dartsLeft = 3 - currentTurn.GetThrows().Count;

        string checkout = CheckoutDatabase.GetCheckout(
            GetSettingsAsX01().checkoutType,
            score,
            dartsLeft
        );

        OnCheckoutAvailable?.Invoke(playerId, checkout);
    }


    // =========================================================
    // SETTINGS ACCESS
    // =========================================================

    public X01GameSettings GetSettingsAsX01()
    {
        return (X01GameSettings) settings.Clone();
    }


    // =========================================================
    // =================== ADDED STATS API =====================
    // =========================================================

    // ---------------------------------------------------------
    // CHECKOUT STATISTICS
    // ---------------------------------------------------------

    protected override void InitializeStats()
    {
        if (playerStats != null)
            playerStats.Clear();
        playerStats = new Dictionary<Guid, GameStats>();
        foreach (var pid in playerIDs)
        {
            playerStats[pid] = new GameStatsX01(pid, GetSettingsAsX01().pointTarget);
        }
    }

    private void CheckSuddenDeath()
    {
        if (suddenDeathTriggered)
            return;

        X01GameSettings settings = GetSettingsAsX01();

        if (settings == null)
            return;
        
        int suddenDeathValue = 0;

        switch (settings.checkoutType)
        {
            case CheckoutType.Single:
                suddenDeathValue = 1;
                break;
            case CheckoutType.Double:
                suddenDeathValue = 2;
                break;
            case CheckoutType.Triple:
                suddenDeathValue = 3;
                break;
        }

        if (suddenDeathValue == 0)
            return;

        suddenDeathTriggered = playerScores.Values.All(s => s == suddenDeathValue);

        if (suddenDeathTriggered)
        {
            OnSuddenDeath?.Invoke();
        }
    }

    private void CheckSuddenDeathAfterUndo()
    {
        if (!suddenDeathTriggered)
            return;

                X01GameSettings settings = GetSettingsAsX01();

        if (settings == null)
            return;
        
        int suddenDeathValue = 0;

        switch (settings.checkoutType)
        {
            case CheckoutType.Single:
                suddenDeathValue = 1;
                break;
            case CheckoutType.Double:
                suddenDeathValue = 2;
                break;
            case CheckoutType.Triple:
                suddenDeathValue = 3;
                break;
        }

        if (suddenDeathValue == 0)
            return;

        suddenDeathTriggered = playerScores.Values.All(s => s == suddenDeathValue);
    }

    private static bool IsCheckable(int remainingPoints, CheckoutType checkoutType)
    {
        // Absoluter Schutz gegen fehlerhafte Werte (z.B. nach einem Überwerfen/Bust im Minusbereich)
        if (remainingPoints <= 0) return false;

        switch (checkoutType)
        {
            case CheckoutType.Single:
                // Single Out: Jedes Segment von 1-20, alle Doppel/Triple und Single/Double Bull
                if (remainingPoints <= 20) return true;
                if (remainingPoints == 25 || remainingPoints == 50) return true;
                
                bool isValidDoubleSO = remainingPoints <= 40 && remainingPoints % 2 == 0;
                bool isValidTripleSO = remainingPoints <= 60 && remainingPoints % 3 == 0;
                return isValidDoubleSO || isValidTripleSO;

            case CheckoutType.Double:
                // Double Out: Mindestens 2 Punkte (D1), maximal 40 (D20) oder Bullseye (50)
                if (remainingPoints < 2) return false;
                if (remainingPoints == 50) return true;
                return remainingPoints <= 40 && remainingPoints % 2 == 0;

            case CheckoutType.Triple:
                // Triple Out: Mindestens 3 Punkte (T1), maximal 60 (T20)
                if (remainingPoints < 3) return false;
                return remainingPoints <= 60 && remainingPoints % 3 == 0;

            case CheckoutType.Master:
                // Master Out: Mindestens 2 Punkte (D1), maximal 60 (T20) oder Bullseye (50)
                if (remainingPoints < 2) return false;
                if (remainingPoints == 50) return true;
                
                bool isGueltigesDoppelMO = remainingPoints <= 40 && remainingPoints % 2 == 0;
                bool isGueltigesTripleMO = remainingPoints <= 60 && remainingPoints % 3 == 0;
                return isGueltigesDoppelMO || isGueltigesTripleMO;

            default:
                Debug.LogError("Unbekannter Checkout-Typ: " + checkoutType);
                return false;
        }
    }

    private void RegisterCheckoutAttempt(Guid playerId)
    {
        if (playerStats.TryGetValue(playerId, out var stats))
        {
            ((GameStatsX01)stats).RegisterCheckoutAttempt();
        }
    }

    private void UnregisterCheckoutAttempt(Guid playerId)
    {
        if (playerStats.TryGetValue(playerId, out var stats))
        {
            ((GameStatsX01)stats).UnregisterCheckoutAttempt();
        }
    }

}