using System;
using TMPro;
using UnityEngine;

public class StatisticsFilterListItem : MonoBehaviour
{
    public CustomToggle toggle;
    public TMP_Text playername;

    private Guid playerID;

    public void Initialize(bool isActive, string playername, Guid playerID)
    {
        toggle.Initialize(isActive);
        this.playername.text = playername;
        this.playerID = playerID;
    }

    public bool IsMarked()
    {
        return toggle.IsActive();
    }

    public Guid GetPlayerID()
    {
        return playerID;
    }

    public void Select(bool isSelected)
    {
        if (toggle.IsActive() != isSelected)
        {
            toggle.Toggle();
        }
    }
}
