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
    [JsonIgnore] public Action<Guid, PenaltyType, Turn> OnPenaltyTriggered;


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
    public GameSettings GetSettings()
    {
        return settings.Clone();
    }

    public Guid GetCurrentPlayerId() => playerIDs[currentPlayerIndex];

    public bool IsFinished() => finishedAt.HasValue;
    public DateTime? GetFinishedAt() => finishedAt;

    public DateTime GetCreatedAt() => createdAt;
    public DateTime GetLastActivityAt() => lastActivityAt;

    /// <summary>
    /// Sorting timestamp: finishedAt > lastActivityAt
    /// </summary>
    public DateTime GetSortTimestamp() => finishedAt ?? lastActivityAt;


    // =========================================================
    // LIFECYCLE & TIMESTAMPS
    // =========================================================

    public virtual void InitializeAfterLoad()
    {
        if (createdAt == default)
            createdAt = finishedAt ?? DateTime.Now;

        if (lastActivityAt == default)
            lastActivityAt = finishedAt ?? createdAt;
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


    // =========================================================
    // PLAYER ROTATION
    // =========================================================

    protected void IdentifyNextLegStarter()
    {
        // 1. Berechne, welcher Spieler dieses Set anfangen durfte
        int setStarter = (sets.Count) % playerIDs.Count;

        // 2. Berechne ausgehend vom Set-Starter, wer das aktuelle Leg anfängt
        startingPlayerIndex = (setStarter + sets[^1].GetLegs().Count) % playerIDs.Count;
    }

    protected void RotateStartingPlayer()
    {
        IdentifyNextLegStarter();
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
    // PENALTY SYSTEM
    // =========================================================

    protected void AddPenalty(Guid playerId, PenaltyType type, Turn turn)
    {
        if (settings == null)
        {
            Debug.LogWarning("Penalties: Settings is null!");
            return;
        }

        if (!settings.Penalties.IsEnabled(type))
        {
            Debug.Log($"Penalty {type} is not enabled in settings.");
            return;
        }

        OnPenaltyTriggered?.Invoke(playerId, type, turn);
        playerStats[playerId].AddPenalty(type);
    }

    protected void RemovePenalty(Guid playerId, PenaltyType type)
    {
        if (settings == null)
        {
            Debug.LogWarning("Penalties: Settings is null!");
            return;
        }

        if (!settings.Penalties.IsEnabled(type))
        {
            Debug.Log($"Penalty {type} is not enabled in settings.");
            return;
        }
        
        playerStats[playerId].RemovePenalty(type);
    }

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

        return sum;
    }


    // =========================================================
    // PENALTY RULES (TURN BASED)
    // =========================================================

    protected void CheckTurnPenalties(Guid playerId, Turn turn)
    {
        CheckThreeOnes(playerId, turn);
        CheckAllMiss(playerId, turn);
    }

    /// <summary>
    /// All 3 darts scored exactly 1 (Board hit)
    /// </summary>
    private void CheckThreeOnes(Guid playerId, Turn turn)
    {
        var throws = turn.GetThrows();
        if (throws.Count != 3) return;

        if (throws.All(t => t.Value == 1 && t.HitType == HitType.Board))
            AddPenalty(playerId, PenaltyType.ThreeOnes, turn);
    }

    /// <summary>
    /// All darts missed (Wall or Ceiling)
    /// </summary>
    protected void CheckAllMiss(Guid playerId, Turn turn)
    {
        var throws = turn.GetThrows();
        if (throws.Count != 3) return;

        if (throws.All(t => t.HitType == HitType.Wall || t.HitType == HitType.Ceiling))
            AddPenalty(playerId, PenaltyType.AllMiss, turn);
    }


    // =========================================================
    // MATCH FLOW (STATE TRANSITIONS)
    // =========================================================

    protected void HandleLegWon(Guid playerId)
    {
        GetCurrentLeg().SetWinner(playerId);

        OnLegWon?.Invoke(playerId);
    }

    protected void HandleSetWon(Guid playerId)
    {
        sets[^1].SetWinner(playerId);
        OnSetWon?.Invoke(playerId);
    }

    protected void HandleMatchEnd(Guid winnerId)
    {
        finishedAt = DateTime.Now;
        matchWinnerId = winnerId;

        TouchActivity();

        playerStats[winnerId].RegisterGameWon();

        foreach (var pid in playerIDs)
        {
            if (pid == winnerId) continue;

            AddPenalty(pid, PenaltyType.LostGame, null);
        }

        OnMatchWon?.Invoke(winnerId);
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
        }
    }

    private void StatsRegisterGameParticipation()
    {
        if (matchWinnerId != Guid.Empty)
            return;

        foreach (Guid playerGUID in playerIDs)
        {
            playerStats[playerGUID].gameCount = 1;

            if (playerGUID == matchWinnerId)
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

            if (matchWinnerId.HasValue)
            {
                matchWinnerId = null;
            }

            foreach (var p in playerIDs)
            {
                playerStats[p].RemovePenalty(PenaltyType.LostGame);
            }

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
                // Start-Rotation zurückdrehen
                startingPlayerIndex = (startingPlayerIndex - 1 + playerIDs.Count) % playerIDs.Count;
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
        var throws = t.GetThrows();
        var lastThrow = throws?.LastOrDefault();
        Guid id = t.PlayerId;

        if (lastThrow.HitType == HitType.Wall)
        {
            RemovePenalty(id, PenaltyType.Wall);
        }
        else if (lastThrow.HitType == HitType.Ceiling)
        {
            RemovePenalty(id, PenaltyType.Ceiling);
        }

        // 2. Kollektive Strafen (nur prüfen, wenn der 3. Dart gerade rückgängig gemacht wird)
        if (throws.Count == 3)
        {
            // Check: Alle 3 Darts sind Misses (Wall ODER Ceiling)
            bool allMisses = throws.All(d => d.HitType == HitType.Wall || d.HitType == HitType.Ceiling);
            if (allMisses)
            {
                RemovePenalty(id, PenaltyType.AllMiss);
            }

            // Check: Alle 3 Darts stecken in der 1
            bool allOnes = throws.All(d => d.Value == 1);
            if (allOnes)
            {
                RemovePenalty(id, PenaltyType.ThreeOnes); 
            }
        }
    }
}



