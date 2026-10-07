using System;
using System.Collections.Generic;

[Serializable]
public class GameSummary
{
    public Guid Id { get; set; }
    public GameMode GameMode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public int StartingPlayerIndex { get; set; }
    public Guid? WinnerPlayerId { get; set; }
    public List<Guid> PlayerIds { get; set; } = new List<Guid>();
    public string SettingsJson { get; set; }
    public bool IsFinished => FinishedAt.HasValue;

    public List<PlayerSummaryEntry> PlayerEntries { get; set; } = new List<PlayerSummaryEntry>();

    public DateTime GetSortTimestamp()
    {
        return FinishedAt ?? LastActivityAt;
    }
}

[Serializable]
public class PlayerSummaryEntry
{
    public Guid PlayerId { get; set; }
    public int Score { get; set; }
    public int SetsWon { get; set; }
    public int LegsWon { get; set; }
    public int TargetsHit { get; set; }
    public int TotalTargets { get; set; }
    public int WallCount { get; set; }
    public int CeilingCount { get; set; }
    public int AllMissCount { get; set; }
    public int ThreeOnesCount { get; set; }
    public int TripleDigitCount { get; set; }
    public int LostGameCount { get; set; }
}
