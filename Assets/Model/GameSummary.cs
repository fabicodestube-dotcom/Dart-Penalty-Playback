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
    /// Kopie der beim Speichern aktiven Custom-Penalty-Defs (ID, Name, Kosten,
    /// volle Trigger-Bedingung). Pro Spiel festgeschrieben, damit History und
    /// Statistik Namen/Trigger/Kosten zeigen können, auch wenn die Def längst
    /// gelöscht oder umbenannt wurde. Quelle ist ausschließlich dieser
    /// Snapshot, niemals der Live-Store.
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
    /// Pro Custom-Def gebuchte Anzahl dieses Spielers in diesem Spiel
    /// (Def-ID → Count). Namen/Kosten/Trigger kommen aus
    /// <see cref="GameSummary.CustomPenaltySnapshot"/>.
    /// </summary>
    public Dictionary<string, int> CustomPenaltyCounts { get; set; } = new Dictionary<string, int>();
}
