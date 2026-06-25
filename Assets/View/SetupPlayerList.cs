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

    public Transform contentParent;

    public PlayerListType listType;

    private List<SetupPlayerItem> items;

    private void Start()
    {
        items = new List<SetupPlayerItem>();

        // 🔥 Wichtig:
        // UI Aufbau wird NICHT direkt gemacht,
        // sondern um 1 Frame verzögert.
        //
        // Grund: Verhindert Race Conditions mit
        // - Layout System
        // - Theme Events
        // - Drag / UI Updates
        StartCoroutine(ShowPlayersNextFrame());
    }

    /// <summary>
    /// Baut die Player-Liste neu auf (frame-sicher).
    /// </summary>
    private IEnumerator ShowPlayersNextFrame()
    {
        // 🔥 1 Frame warten, damit Unity intern
        // alle Start/OnEnable/Layout Updates fertig macht
        yield return null;

        ShowPlayers();
    }

    /// <summary>
    /// Baut die UI-Liste komplett neu auf.
    /// </summary>
    public void ShowPlayers()
    {
        // 🔥 1. Alte Items zerstören (sauber & eindeutig)
        // Kein SetActive(false) nötig → vermeidet Side Effects im UI System
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }

            items.Clear();
        }

        // 🔥 2. Neue Daten holen
        var players = GetPlayers();

        if (players == null)
            return;

        // 🔥 3. Neue UI Elemente erzeugen
        foreach (var player in players)
        {
            GameObject goObj = Instantiate(prefab, contentParent);
            var item = goObj.GetComponent<SetupPlayerItem>();

            item.Initialize(player);

            items.Add(item);
        }
    }

    /// <summary>
    /// Holt die passende Player-Liste je nach Modus.
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
    /// Übernimmt Reihenfolge aus UI Drag & Drop.
    /// </summary>
    public void UpdateOrderFromUI()
    {
        // Nur Active sinnvoll
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