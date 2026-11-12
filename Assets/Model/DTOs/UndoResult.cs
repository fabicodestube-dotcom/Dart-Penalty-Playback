using System;

public class UndoResult
{
    public bool LegWinnerUndone { get; set; }
    public Guid PreviousWinnerId { get; set; }
    public bool SetWinnerUndone { get; set; }
    
    // NEW: So that the X01-Stats can still evaluate the deleted leg
    public Leg UndoneLeg { get; set; } 
}
