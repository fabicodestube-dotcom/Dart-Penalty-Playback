
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
   public Turn Turn; // Reference for Undo
}

[System.Serializable]
public enum PenaltyTiming
{
   Instant,
   EndOfTurn
}