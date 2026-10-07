
[System.Serializable]
public enum PenaltyType
{
   Wall,
   Ceiling,
   Schnapszahl,
   ThreeOnes,
   LostGame,
   AllMiss
}

[System.Serializable]
public class Penalty
{
   public int PlayerId;
   public PenaltyType Type;
   public Turn Turn; // 🔴 Referenz für Undo
}

[System.Serializable]
public enum PenaltyTiming
{
   Instant,
   EndOfTurn
}