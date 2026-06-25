using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class GameStats
{
    // =========================================================
    // IDENTIFIKATION
    // =========================================================
    [JsonProperty] public Guid playerID;

    // =========================================================
    // GAME STATE (laufender Spielstand innerhalb eines Matches)
    // =========================================================
    [JsonProperty] public int gameCount = 0;
    [JsonProperty] public int gamesWon = 0;
    [JsonProperty] public int currentLegCount = 0; // Anzahl der gewonnenen Legs im aktuellen Set (wird nach Set-Win auf 0 gesetzt)
    [JsonProperty] public int totalLegsCount = 0; // Gesamtzahl der gespielten Legs (über alle gespielten Matches)
    [JsonProperty] public int totalLegsWon = 0; // Gesamtzahl der gewonnenen Legs (über alle gespielten Matches)
    [JsonProperty] public float totalLegWinRate = 0f; // wird aus won/count berechnet (gerundet)

    [JsonProperty] public int totalSetsCount = 0;
    [JsonProperty] public int totalSetsWon = 0;
    [JsonProperty] public float totalSetWinRate = 0f; // wird aus won/count berechnet (gerundet)

    // =========================================================
    // PENALTIES (Fehler / Regelverstöße / Spezialereignisse)
    // =========================================================
    [JsonProperty] public int wallCount = 0;
    [JsonProperty] public int ceilingCount = 0;
    [JsonProperty] public int allMissCount = 0;
    [JsonProperty] public int tripleOnesCount = 0;
    [JsonProperty] public int tripleDigitCount; // Nur X01 + Cricket
    [JsonProperty] public int lostGame = 0;

    // =========================================================
    // THROWS (Dart-Wurf Statistik / Trefferquoten)
    // =========================================================
    [JsonProperty] public int totalThrowsCount = 0;
    [JsonProperty] public int totalDoubleCount = 0;
    [JsonProperty] public int totalTripleCount = 0;
    [JsonProperty] public float doublePercentage = 0f;
    [JsonProperty] public float triplePercentage = 0f;


    // =========================================================
    // INITIALISIERUNG
    // =========================================================
    [JsonProperty] public List<Guid> appliedGameIds = new List<Guid>();
    [JsonProperty] public Dictionary<Guid, float> gamePenaltyCosts = new Dictionary<Guid, float>();
    [JsonProperty] public float totalPenaltyCost = 0f;

    public GameStats(Guid playerID)
    {
        this.playerID = playerID;
    }

    public virtual void AddGameStat(GameStats stats, Guid gameId, float penaltyCost = 0f)
    {
        if (stats == null || gameId == null || appliedGameIds.Contains(gameId))
            return;

        int previousGames = gameCount;

        appliedGameIds.Add(gameId);
        gameCount++;

        gamesWon += stats.gamesWon;

        gamePenaltyCosts[gameId] = penaltyCost;
        totalPenaltyCost = gamePenaltyCosts.Values.Sum();

        totalLegsCount += stats.totalLegsCount;
        totalLegsWon += stats.totalLegsWon;
        totalSetsCount += stats.totalSetsCount;
        totalSetsWon += stats.totalSetsWon;

        wallCount += stats.wallCount;
        ceilingCount += stats.ceilingCount;
        allMissCount += stats.allMissCount;
        tripleOnesCount += stats.tripleOnesCount;
        tripleDigitCount += stats.tripleDigitCount;
        lostGame += stats.lostGame;

        AddThrowCountToBaseplayer(previousGames, stats.totalThrowsCount);
        totalDoubleCount += stats.totalDoubleCount;
        totalTripleCount += stats.totalTripleCount;

        UpdateThrowPercentages();
        UpdateLegWinRate();
        UpdateSetWinRate();
    }

    public virtual void RemoveGameStat(GameStats stats, Guid gameId)
    {
        if (stats == null)
        {
            Debug.LogWarning("[Abbruch] Stats-Objekt ist null.");
            return;
        }

        if (gameId == null)
        {
            Debug.LogWarning($"[Abbruch] Ungültige GameId: {gameId} (muss einen Wert haben).");
            return;
        }

        if (!appliedGameIds.Contains(gameId))
        {
            Debug.LogWarning($"[Abbruch] GameId {gameId} ist nicht in 'appliedGameIds' enthalten.");
            return;
        }

        int currentGames = gameCount;

        appliedGameIds.Remove(gameId);
        gameCount = Math.Max(0, gameCount - 1);
        gamesWon -= stats.gamesWon;

        gamePenaltyCosts.Remove(gameId);
        totalPenaltyCost = gamePenaltyCosts.Values.Sum();

        totalLegsCount = Math.Max(0, totalLegsCount - stats.totalLegsCount);
        totalLegsWon = Math.Max(0, totalLegsWon - stats.totalLegsWon);
        totalSetsCount = Math.Max(0, totalSetsCount - stats.totalSetsCount);
        totalSetsWon = Math.Max(0, totalSetsWon - stats.totalSetsWon);

        wallCount = Math.Max(0, wallCount - stats.wallCount);
        ceilingCount = Math.Max(0, ceilingCount - stats.ceilingCount);
        allMissCount = Math.Max(0, allMissCount - stats.allMissCount);
        tripleOnesCount = Math.Max(0, tripleOnesCount - stats.tripleOnesCount);
        tripleDigitCount = Math.Max(0, tripleDigitCount - stats.tripleDigitCount);
        lostGame = Math.Max(0, lostGame - stats.lostGame);

        RemoveThrowCountFromBaseplayer(currentGames, stats.totalThrowsCount);
        totalDoubleCount = Math.Max(0, totalDoubleCount - stats.totalDoubleCount);
        totalTripleCount = Math.Max(0, totalTripleCount - stats.totalTripleCount);

        UpdateThrowPercentages();
        UpdateLegWinRate();
        UpdateSetWinRate();
    }

    private void AddThrowCountToBaseplayer(int previousGames, int addedThrows)
    {
        if (previousGames == 0)
        {
            totalThrowsCount = addedThrows;
        }
        else
        {
            totalThrowsCount = (int)Math.Round(
                ((float)totalThrowsCount * previousGames + addedThrows)
                / (previousGames + 1));
        }
    }

    public void RemoveThrowCountFromBaseplayer(int currentGames, int removedThrows)
    {
        if (currentGames <= 1)
        {
            totalThrowsCount = 0;
        }
        else
        {
            totalThrowsCount = (int)Math.Round(
                ((float)totalThrowsCount * currentGames - removedThrows)
                / (currentGames - 1));
        }

    }


    // =========================================================
    // THROW LOGIC (FORWARD UPDATE)
    // =========================================================
    public virtual void AddThrow(Throw t)
    {
        if (t == null)
        {
            return;
        }

        totalThrowsCount++;

        if (t.HitType == HitType.Board)
        {
            if (t.Multiplier == DartMultiplier.Double)
            {
                totalDoubleCount++;
            }

            if (t.Multiplier == DartMultiplier.Triple)
            {
                totalTripleCount++;
            }
        }

        if (totalThrowsCount > 0)
        {
            doublePercentage = (float)Math.Round((float)totalDoubleCount / totalThrowsCount, 2);
            triplePercentage = (float)Math.Round((float)totalTripleCount / totalThrowsCount, 2);
        }
    }


    // =========================================================
    // PENALTIES (FORWARD UPDATE)
    // =========================================================
    public virtual void AddPenalty(PenaltyType type)
    {
        switch (type)
        {
            case PenaltyType.Wall:
                wallCount++;
                break;

            case PenaltyType.Ceiling:
                ceilingCount++;
                break;

            case PenaltyType.AllMiss:
                allMissCount++;
                break;

            case PenaltyType.ThreeOnes:
                tripleOnesCount++;
                break;

            case PenaltyType.Schnapszahl:
                tripleDigitCount++;
                break;

            case PenaltyType.LostGame:
                lostGame++;
                break;    
        }
    }

    public void ResetForStatCalculation()
    {
        currentLegCount = 0;
        totalLegsCount = 0;
        totalLegsWon = 0;
        totalLegWinRate = 0f;

        totalSetsCount = 0;
        totalSetsWon = 0;
        totalSetWinRate = 0;

        totalThrowsCount = 0;
        totalDoubleCount = 0;
        totalTripleCount = 0;

        doublePercentage = 0f;
        triplePercentage = 0f;
    }


    // =========================================================
    // LEG TRACKING (FORWARD UPDATE)
    // =========================================================
    public void RegisterLegWon()
    {
        totalLegsWon++;
        currentLegCount ++;
        UpdateLegWinRate();
    }

    public void RegisterLegPlayed()
    {
        totalLegsCount++;
        UpdateLegWinRate();
    }

    public void RegisterSetWon()
    {
        totalSetsWon ++;
        UpdateSetWinRate();
    }

    public void RegisterSetPlayed()
    {
        totalSetsCount++;
        currentLegCount = 0;
        UpdateSetWinRate();
    }

    public void RegisterGameWon()
    {
        gamesWon += 1;   
    }


    // // =========================================================
    // // THROW LOGIC (UNDO / BACKWARD UPDATE)
    // // =========================================================
    // public virtual void RemoveThrow(Throw t)
    // {
    //     if (t == null)
    //     {
    //         Debug.LogWarning($"[PlayerStats] RemoveThrow NULL (Player {playerID})");
    //         return;
    //     }

    //     totalThrowsCount = Math.Max(0, totalThrowsCount - 1);

    //     if (t.HitType == HitType.Board)
    //     {
    //         if (t.Multiplier == DartMultiplier.Double)
    //             totalDoubleCount = Math.Max(0, totalDoubleCount - 1);

    //         if (t.Multiplier == DartMultiplier.Triple)
    //             totalTripleCount = Math.Max(0, totalTripleCount - 1);
    //     }

    //     UpdateThrowPercentages();
    // }


    // =========================================================
    // PENALTIES (UNDO / BACKWARD UPDATE)
    // =========================================================
    public virtual void RemovePenalty(PenaltyType type)
    {
        switch (type)
        {
            case PenaltyType.Wall:
                wallCount = Math.Max(0, wallCount - 1);
                break;

            case PenaltyType.Ceiling:
                ceilingCount = Math.Max(0, ceilingCount - 1);
                break;
            
            case PenaltyType.AllMiss:
                allMissCount = Math.Max(0, allMissCount - 1);
                break;

            case PenaltyType.ThreeOnes:
                tripleOnesCount = Math.Max(0, tripleOnesCount - 1);
                break;

            case PenaltyType.Schnapszahl:
                tripleDigitCount = Math.Max(0, tripleDigitCount - 1);
                break;

            case PenaltyType.LostGame:
                lostGame = Math.Max(0, lostGame - 1);
                break;
        }
    }

    public float GetTotalPenaltyCosts()
    {
        var settings = AppSettingsManager.Instance.Settings.Penalties;

        float total = 0f;

        total += GetPenaltyCost(settings, PenaltyType.Wall, wallCount);
        total += GetPenaltyCost(settings, PenaltyType.Ceiling, ceilingCount);
        total += GetPenaltyCost(settings, PenaltyType.AllMiss, allMissCount);
        total += GetPenaltyCost(settings, PenaltyType.ThreeOnes, tripleOnesCount);
        total += GetPenaltyCost(settings, PenaltyType.LostGame, lostGame);

        // Achtung: existiert nicht in AddPenalty, aber in Stats vorhanden
        total += GetPenaltyCost(settings, PenaltyType.Schnapszahl, tripleDigitCount);

        return total;
    }


    // =========================================================
    // HELPER / DERIVED STATS
    // =========================================================
    private void UpdateLegWinRate()
    {
        if (totalLegsCount == 0)
        {
            totalLegWinRate = 0;
            return;
        }

        totalLegWinRate = (float)Math.Round(
            (float)totalLegsWon / totalLegsCount, 2);
    }

    private void UpdateSetWinRate()
    {
        if (totalSetsCount == 0)
        {
            totalSetWinRate = 0;
            return;
        }

        totalSetWinRate = (float)Math.Round(
            (float)totalSetsWon / totalSetsCount, 2);
    }

    private void UpdateThrowPercentages()
    {
        if (totalThrowsCount == 0)
        {
            doublePercentage = 0;
            triplePercentage = 0;
            return;
        }

        doublePercentage = (float)Math.Round((float)totalDoubleCount / totalThrowsCount, 2);
        triplePercentage = (float)Math.Round((float)totalTripleCount / totalThrowsCount, 2);
    }

    private float GetPenaltyCost(PenaltySettings settings, PenaltyType type, int count)
    {
        if (count <= 0)
            return 0f;

        if (!settings.IsEnabled(type))
            return 0f;

        float cost = settings.GetCost(type);
        float result = cost * count;

        return result;
    }
}