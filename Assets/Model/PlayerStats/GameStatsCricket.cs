using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class GameStatsCricket : GameStats
{
    [JsonProperty] public int turnCount;
    [JsonProperty] public int markCount;
    [JsonProperty] public float marksPerRound;

    [JsonProperty] public Dictionary<Guid, int> gameThrowCount = new Dictionary<Guid, int>();
    [JsonProperty] public Dictionary<Guid, float> gameDoublePercentage = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, float> gameTriplePercentage = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, float> gameMarksPerRound = new Dictionary<Guid, float>();

    // Neue Felder für erweiterte Statistiken
    [JsonProperty] public Dictionary<int, int> turnScores = new Dictionary<int, int>(); // Punkte pro Turn
    [JsonProperty] public Dictionary<int, int> turnMarks = new Dictionary<int, int>(); // Marks pro Turn
    [JsonProperty] public Dictionary<int, int> turnOverkills = new Dictionary<int, int>(); // Overkill-Punkte pro Turn
    [JsonProperty] public float maxMPR = 0f; // Höchste MPR einer Runde
    
    // Preferences
    [JsonProperty] public int firstClosedField = -1;
    [JsonProperty] public int lastClosedField = -1;
    [JsonProperty] public int maxPointsField = -1;
    [JsonProperty] public int maxPointsFieldValue = 0;
    
    // Highscores
    [JsonProperty] public int nineMarkTurns = 0;
    [JsonProperty] public int whiteHorseTurns = 0;
    [JsonProperty] public int sameTripleTurns = 0;

    public GameStatsCricket(Guid playerID) : base(playerID)
    {
        turnCount = 0;
        markCount = 0;
        EnsureInternalStructures();
    }

    public override void AddGameStat(GameStats stats, Guid gameId, float penaltyCost = 0f)
    {
        if (stats is not GameStatsCricket cricket)
            return;

        if (gameMarksPerRound.ContainsKey(gameId))
            return;

        base.AddGameStat(stats, gameId, penaltyCost);

        turnCount += cricket.turnCount;
        markCount += cricket.markCount;

        gameThrowCount[gameId] = cricket.totalThrowsCount;
        gameDoublePercentage[gameId] = cricket.doublePercentage;
        gameTriplePercentage[gameId] = cricket.triplePercentage;
        gameMarksPerRound[gameId] = cricket.marksPerRound;

        // Neue Felder hinzufügen
        if (cricket.maxMPR > maxMPR)
            maxMPR = cricket.maxMPR;

        if (cricket.firstClosedField != -1 && firstClosedField == -1)
            firstClosedField = cricket.firstClosedField;

        if (cricket.lastClosedField != -1)
            lastClosedField = cricket.lastClosedField;

        if (cricket.maxPointsFieldValue > maxPointsFieldValue)
        {
            maxPointsField = cricket.maxPointsField;
            maxPointsFieldValue = cricket.maxPointsFieldValue;
        }

        nineMarkTurns += cricket.nineMarkTurns;
        whiteHorseTurns += cricket.whiteHorseTurns;
        sameTripleTurns += cricket.sameTripleTurns;

        RecalculateAggregatedValues();
    }

    public override void RemoveGameStat(GameStats stats, Guid gameId)
    {
        if (stats is not GameStatsCricket cricket)
            return;

        if (!gameMarksPerRound.ContainsKey(gameId))
            return;

        base.RemoveGameStat(stats, gameId);

        turnCount = Math.Max(0, turnCount - cricket.turnCount);
        markCount = Math.Max(0, markCount - cricket.markCount);

        gameThrowCount.Remove(gameId);
        gameDoublePercentage.Remove(gameId);
        gameTriplePercentage.Remove(gameId);
        gameMarksPerRound.Remove(gameId);

        // Neue Felder entfernen
        nineMarkTurns = Math.Max(0, nineMarkTurns - cricket.nineMarkTurns);
        whiteHorseTurns = Math.Max(0, whiteHorseTurns - cricket.whiteHorseTurns);
        sameTripleTurns = Math.Max(0, sameTripleTurns - cricket.sameTripleTurns);

        // Maximalwerte neu berechnen
        if (cricket.maxMPR >= maxMPR)
            RecalculateMaxMPR();

        if (cricket.firstClosedField == firstClosedField)
            RecalculateFieldPreferences();

        RecalculateAggregatedValues();
    }

    public int GameEntries => gameThrowCount.Count;

    public double AverageThrowsPerGame => gameThrowCount.Count == 0
        ? 0f
        : gameThrowCount.Values.Average();

    public float AverageDoublePercentage => gameDoublePercentage.Count == 0
        ? 0f
        : gameDoublePercentage.Values.Average();

    public float AverageTriplePercentage => gameTriplePercentage.Count == 0
        ? 0f
        : gameTriplePercentage.Values.Average();

    private void RecalculateAggregatedValues()
    {
        marksPerRound = gameMarksPerRound.Count == 0
            ? 0f
            : gameMarksPerRound.Values.Average();
    }

    public List<float> GetAllGameMPRs()
    {
        return gameMarksPerRound.Values.ToList();
    }

    private void EnsureInternalStructures()
    {
        gameThrowCount ??= new Dictionary<Guid, int>();
        gameDoublePercentage ??= new Dictionary<Guid, float>();
        gameTriplePercentage ??= new Dictionary<Guid, float>();
        gameMarksPerRound ??= new Dictionary<Guid, float>();
        turnScores ??= new Dictionary<int, int>();
        turnMarks ??= new Dictionary<int, int>();
        turnOverkills ??= new Dictionary<int, int>();
    }

    public void RegisterThrow(Throw t, int newMarks)
    {
        base.AddThrow(t);
        markCount += newMarks;
    }


    public void RegisterTurn()
    {
        turnCount++;

        CalculateMPR();
        UpdateMaxMPR();
    }

    private void CalculateMPR()
    {
        if (turnCount == 0)
        {
            marksPerRound = 0;
            Debug.Log("[PlayerStatsCricket] MPR reset (no turns)");
            return;
        }

        marksPerRound = (float)markCount / turnCount;
        marksPerRound = (float)Math.Round(marksPerRound, 2);

        Debug.Log(
            $"[PlayerStatsCricket] MPR updated" +
            $" = {marksPerRound} ({markCount}/{turnCount})"
        );
    }

    // ========================================================
    // NEUE STATISTIK-METHODEN
    // ========================================================

    public void UpdateTurnStats(int turnIndex, int score, int marks, int overkills)
    {
        turnScores[turnIndex] = score;
        turnMarks[turnIndex] = marks;
        turnOverkills[turnIndex] = overkills;

        // Erkenne 9 Marks
        if (marks == 9)
            nineMarkTurns++;
    }

    public void UpdateHighscores(bool hasWhiteHorse, bool hasSameTriple)
    {
        if (hasWhiteHorse)
            whiteHorseTurns++;
        if (hasSameTriple)
            sameTripleTurns++;
    }

    public void UpdateMaxMPR()
    {
        // Aktualisiere maxMPR nach RegisterTurn()
        if (marksPerRound > maxMPR)
            maxMPR = marksPerRound;
    }

    public void UpdateFieldPreferences(int fieldNumber, int turnsTaken, int pointsGiven, bool isNewClosed)
    {
        if (isNewClosed && firstClosedField == -1)
            firstClosedField = fieldNumber;

        if (isNewClosed)
            lastClosedField = fieldNumber;

        if (pointsGiven > maxPointsFieldValue)
        {
            maxPointsField = fieldNumber;
            maxPointsFieldValue = pointsGiven;
        }
    }

    private void RecalculateMaxMPR()
    {
        if (gameMarksPerRound.Count == 0)
        {
            maxMPR = 0f;
            return;
        }

        maxMPR = gameMarksPerRound.Values.Max();
    }

    private void RecalculateFieldPreferences()
    {
        firstClosedField = -1;
        lastClosedField = -1;
        maxPointsField = -1;
        maxPointsFieldValue = 0;
    }

    public double AverageScorePerTurn => turnCount == 0 ? 0 : turnScores.Values.Sum() / (double)turnCount;
    public int MaxScoreInTurn => turnScores.Count == 0 ? 0 : turnScores.Values.Max();
    public int TotalOverkillPoints => turnOverkills.Values.Sum();
}