using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerList : MonoBehaviour, IPlayerItemOwner
{
    public GameObject prefab;
    public GameObject playerPrefab;
    public GameObject botPrefab;

    public Transform contentParent;

    private List<GameObject> items = new List<GameObject>();

    private PlayerHandler handler;

    public void Initialize(PlayerHandler handler)
    {
        this.handler = handler;
    }

    public void UpdateList(List<BasePlayer> players)
    {
        // 🔥 1. Alte Items zerstören
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                    Destroy(item);
            }
            items.Clear();
        }

        if (players == null)
            return;

        // 🔥 2. Neue UI Elemente erzeugen
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
                playerItem.Initialize(player, this);
            else
            {
                var botItem = goObj.GetComponent<BotItem>();
                if (botItem != null)
                    botItem.Initialize(player, this);
            }

            items.Add(goObj);
        }
    }

    public void UpdateOrderFromUI()
    {
        if (appHandler == null)
            return;

        List<Guid> newOrder = new List<Guid>();

        foreach (Transform child in contentParent)
        {
            var drag = child.GetComponent<PlayerDragHandler>();
            if (drag == null)
                continue;

            newOrder.Add(drag.playerID);
        }

        appHandler.SetPlayerOrder(newOrder);
    }

    public void RenameClicked(Guid id)
    {
        handler?.OpenRenamePopup(id);
    }

    public void DeleteClicked(Guid id)
    {
        handler?.OpenDeletePopup(id);
    }

    private AppHandler appHandler => handler != null ? handler.appHandler : null;
}
