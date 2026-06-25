using Newtonsoft.Json;
using System;


[System.Serializable]
public class Player : BasePlayer
{
    // =========================================================
    // Konstructor
    // =========================================================

    public Player(Guid id, string playerName) : base(id, playerName)
    {
        // Hier ist kein weiterer Code nötig, da die Logik in BasePlayer steckt
    }


    // =========================================================
    // Class specific methods (if needed) 
    // =========================================================

    public override void Rename(string newName)
    {
        this.playerName = newName;
    }
}