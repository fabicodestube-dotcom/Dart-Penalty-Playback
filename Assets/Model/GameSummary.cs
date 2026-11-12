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

    /// <summary>
    /// copy of custom penalty defs (ID, name, cost) active during storage,
    /// Per game, so history and
    /// Statistics Names/Triggers/Cost can show, even if the Def long ago
    /// deleted or renamed. source is exclusively
    /// Snapshot, never the live store.
    /// </summary>
    public List<CustomPenaltyDef> CustomPenaltySnapshot { get; set; } = new List<CustomPenaltyDef>();

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

    /// <summary>
    /// Per Custom-Def booked number of this player in this game
    /// (Def-ID → Count) Names/costs/triggers come from
    /// <see cref="GameSummary.CustomPenaltySnapshot"/>.
    /// </summary>
    public Dictionary<string, int> CustomPenaltyCounts { get; set; } = new Dictionary<string, int>();
}
