using System;

public class UndoResult
{
    public bool LegWinnerUndone { get; set; }
    public Guid PreviousWinnerId { get; set; }
    public bool SetWinnerUndone { get; set; }
    
    // NEU: Damit die X01-Stats das gelöschte Leg noch auswerten können
    public Leg UndoneLeg { get; set; } 
}
