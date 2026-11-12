using Newtonsoft.Json;
using System;


[System.Serializable]
public class Player : BasePlayer
{
    // =========================================================
    // Constructor
    // =========================================================

    public Player(Guid id, string playerName) : base(id, playerName)
    {
        // No more code is needed here, as the logic is in BasePlayer
    }


    // =========================================================
    // Class specific methods (if needed) 
    // =========================================================

    public override void Rename(string newName)
    {
        this.playerName = newName;
    }
}