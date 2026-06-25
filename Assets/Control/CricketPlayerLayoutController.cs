using System.Collections.Generic;
using UnityEngine;

public class CricketPlayerLayoutController : MonoBehaviour
{
    public CricketDefaultPlayerList defaultLayout;
    public CricketTwoLayoutPlayerList twoPlayerLayout;

    private bool isTwoPlayerLayoutActive = false;

    public void Init(List<BasePlayer> activePlayers)
    {
        if (activePlayers == null || activePlayers.Count == 0)
        {
            Debug.LogError("CricketPlayerLayoutController.Init: activePlayers cannot be null or empty.");
            return;
        }

        if (activePlayers.Count == 2)
        {
            defaultLayout.gameObject.SetActive(false);
            twoPlayerLayout.gameObject.SetActive(true);
            twoPlayerLayout.Init(activePlayers);
            isTwoPlayerLayoutActive = true;
            Debug.Log("CricketPlayerLayoutController: Initialized two-player layout.");
        }
        else
        {
            twoPlayerLayout.gameObject.SetActive(false);
            defaultLayout.gameObject.SetActive(true);
            defaultLayout.Init(activePlayers);
            isTwoPlayerLayoutActive = false;
            Debug.Log("CricketPlayerLayoutController: Initialized default layout for " + activePlayers.Count + " players.");
        }
    }
    public void RefreshUI()
    {
        if (isTwoPlayerLayoutActive)
        {
            twoPlayerLayout.RefreshUI();
        }
        else
        {
            defaultLayout.RefreshUI();
        }
    }
}
