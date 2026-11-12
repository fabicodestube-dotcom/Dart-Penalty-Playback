using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerListType
{
    Active,
    Reserve
}

public class SetupPlayerList : MonoBehaviour
{
    public AppHandler appHandler;
    public GameObject prefab;
    public GameObject playerPrefab;
    public GameObject botPrefab;

    public Transform contentParent;

    public PlayerListType listType;

    private List<GameObject> items;

    private void Start()
    {
        items = new List<GameObject>();

        // Important:
        // UI construction is NOT made directly,
        // It is delayed by 1 frame.
        //
        // Reason: Prevents Race Conditions with
        // - Layout System
        // - Theme Events
        // - Drag / UI Updates
        StartCoroutine(ShowPlayersNextFrame());
    }

    /// <summary>
    /// Rebuilds the player list (frame-safe).
    /// </summary>
    private IEnumerator ShowPlayersNextFrame()
    {
        // Wait one frame for Unity to finish
        // all start/OnEnable/layout updates internally
        yield return null;

        ShowPlayers();
    }

    /// <summary>
    /// Completely rebuild the UI list.
    /// </summary>
    public void ShowPlayers()
    {
        // Destroy Old Items (Clean & Clear)
        // No SetActive (false) necessary → avoids side effects in the UI system
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                    Destroy(item);
            }

            items.Clear();
        }

        // 2. Fetch new data
        var players = GetPlayers();

        if (players == null)
            return;

        // 3. Create new UI elements
        foreach (var player in players)
        {
            GameObject template = prefab;
            if (player is DartBot && botPrefab != null)
                template = botPrefab;
            else if (!(player is DartBot) && playerPrefab != null)
                template = playerPrefab;

            if (template == null)
                continue;

            GameObject goObj = Instantiate(template, contentParent);

            var playerItem = goObj.GetComponent<PlayerItem>();
            if (playerItem != null)
                playerItem.Initialize(player);
            else
            {
                var botItem = goObj.GetComponent<BotItem>();
                if (botItem != null)
                    botItem.Initialize(player);
            }

            items.Add(goObj);
        }
    }

    /// <summary>
    /// Get the appropriate player list depending on the mode.
    /// </summary>
    private IEnumerable<BasePlayer> GetPlayers()
    {
        switch (listType)
        {
            case PlayerListType.Active:
                return appHandler.GetActivePlayers();

            case PlayerListType.Reserve:
                return appHandler.GetReservePlayers();

            default:
                return null;
        }
    }

    /// <summary>
    /// Takes order from UI Drag & Drop.
    /// </summary>
    public void UpdateOrderFromUI()
    {
        // Only Active is Useful
        if (listType != PlayerListType.Active)
            return;

        List<Guid> newOrder = new List<Guid>();

        foreach (Transform child in contentParent)
        {
            var drag = child.GetComponent<PlayerDragHandler>();

            if (drag == null)
                continue;

            newOrder.Add(drag.playerID);
        }

        appHandler.SetActivePlayerOrder(newOrder);
    }
}