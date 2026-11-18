using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

public class CricketStats
{
    [JsonProperty] public int turnCount;
    [JsonProperty] public int markCount;
    [JsonProperty] public float marksPerRound;

    [JsonProperty] public Dictionary<Guid, float> gameMarksPerRound = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<int, int> turnScores = new Dictionary<int, int>();
    [JsonProperty] public Dictionary<int, int> turnMarks = new Dictionary<int, int>();
    [JsonProperty] public Dictionary<int, int> turnOverkills = new Dictionary<int, int>();
    [JsonProperty] public float maxMPR = 0f;

    [JsonProperty] public Dictionary<string, Dictionary<int, int>> gameTurnScores = new Dictionary<string, Dictionary<int, int>>();
    [JsonProperty] public Dictionary<string, Dictionary<int, int>> gameTurnOverkills = new Dictionary<string, Dictionary<int, int>>();

    [JsonProperty] public int firstClosedField = -1;
    [JsonProperty] public int lastClosedField = -1;
    [JsonProperty] public int maxPointsField = -1;
    [JsonProperty] public int maxPointsFieldValue = 0;

    [JsonProperty] public int nineMarkTurns = 0;
    [JsonProperty] public int whiteHorseTurns = 0;
    [JsonProperty] public int sameTripleTurns = 0;

    public CricketStats()
    {
        EnsureInternalStructures();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        EnsureInternalStructures();
    }

    public void EnsureInternalStructures()
    {
        gameMarksPerRound ??= new Dictionary<Guid, float>();
        turnScores ??= new Dictionary<int, int>();
        turnMarks ??= new Dictionary<int, int>();
        turnOverkills ??= new Dictionary<int, int>();
        gameTurnScores ??= new Dictionary<string, Dictionary<int, int>>();
        gameTurnOverkills ??= new Dictionary<string, Dictionary<int, int>>();
    }

    public void AddGameStat(CricketStats cricket, Guid gameId)
    {
        if (cricket == null || gameId == Guid.Empty || gameMarksPerRound.ContainsKey(gameId))
            return;

        turnCount += cricket.turnCount;
        markCount += cricket.markCount;

        gameMarksPerRound[gameId] = cricket.marksPerRound;

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

        gameTurnScores[gameId.ToString()] = new Dictionary<int, int>(cricket.turnScores);
        gameTurnOverkills[gameId.ToString()] = new Dictionary<int, int>(cricket.turnOverkills);
        RebuildMergedTurnScores();

        RecalculateMPR();
    }

    public void RemoveGameStat(CricketStats cricket, Guid gameId)
    {
        if (cricket == null || !gameMarksPerRound.ContainsKey(gameId))
            return;

        turnCount = Math.Max(0, turnCount - cricket.turnCount);
        markCount = Math.Max(0, markCount - cricket.markCount);

        gameMarksPerRound.Remove(gameId);

        nineMarkTurns = Math.Max(0, nineMarkTurns - cricket.nineMarkTurns);
        whiteHorseTurns = Math.Max(0, whiteHorseTurns - cricket.whiteHorseTurns);
        sameTripleTurns = Math.Max(0, sameTripleTurns - cricket.sameTripleTurns);

        gameTurnScores.Remove(gameId.ToString());
        gameTurnOverkills.Remove(gameId.ToString());
        RebuildMergedTurnScores();

        if (cricket.maxMPR >= maxMPR)
            RecalculateMaxMPR();

        if (cricket.firstClosedField == firstClosedField)
            RecalculateFieldPreferences();

        RecalculateMPR();
    }

    public void ResetForRebuild()
    {
        turnCount = 0;
        markCount = 0;
        marksPerRound = 0f;
        maxMPR = 0f;
        firstClosedField = -1;
        lastClosedField = -1;
        maxPointsField = -1;
        maxPointsFieldValue = 0;
        nineMarkTurns = 0;
        whiteHorseTurns = 0;
        sameTripleTurns = 0;

        gameMarksPerRound.Clear();
        turnScores.Clear();
        turnMarks.Clear();
        turnOverkills.Clear();
        gameTurnScores.Clear();
        gameTurnOverkills.Clear();
    }

    private void RebuildMergedTurnScores()
    {
        turnScores.Clear();
        turnOverkills.Clear();

        int idx = 0;
        foreach (var kv in gameTurnScores)
        {
            foreach (var e in kv.Value.OrderBy(x => x.Key))
                turnScores[idx++] = e.Value;
        }

        int oidx = 0;
        foreach (var kv in gameTurnOverkills)
        {
            foreach (var e in kv.Value.OrderBy(x => x.Key))
                turnOverkills[oidx++] = e.Value;
        }
    }

    public void RegisterThrow(Throw t, int newMarks)
    {
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
            return;
        }

        marksPerRound = (float)Math.Round((float)markCount / turnCount, 2);
    }

    public void UpdateTurnStats(int turnIndex, int score, int marks, int overkills)
    {
        turnScores[turnIndex] = score;
        turnMarks[turnIndex] = marks;
        turnOverkills[turnIndex] = overkills;

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

    private void RecalculateMPR()
    {
        marksPerRound = turnCount == 0
            ? 0f
            : (float)Math.Round((float)markCount / turnCount, 2);
    }

    private void RecalculateMaxMPR()
    {
        maxMPR = gameMarksPerRound.Count == 0
            ? 0f
            : gameMarksPerRound.Values.Max();
    }

    private void RecalculateFieldPreferences()
    {
        firstClosedField = -1;
        lastClosedField = -1;
        maxPointsField = -1;
        maxPointsFieldValue = 0;
    }

    [JsonIgnore] public double AverageScorePerTurn => turnCount == 0 ? 0 : turnScores.Values.Sum() / (double)turnCount;
    [JsonIgnore] public int MaxScoreInTurn => turnScores.Count == 0 ? 0 : turnScores.Values.Max();
    [JsonIgnore] public int TotalOverkillPoints => turnOverkills.Values.Sum();
}
