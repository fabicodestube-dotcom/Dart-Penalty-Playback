using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HistoryHandler : MonoBehaviour, IUIScreen
{
    [Header("Handler")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public SwipeMenu swipeMenu;
    public List<UIScreen> popups;

    [Header("Pages")]
    public Transform allGamesParent;
    public Transform x01GamesParent;
    public Transform cricketGamesParent;
    public Transform atcGamesParent;

    public HistoryItem prefab;
    public GameObject prefabHeadline;

    private List<HistoryItem> allItems = new();
    private List<HistoryItem> x01Items = new();
    private List<HistoryItem> atcItems = new();
    private List<HistoryItem> cricketItems = new();

    private Dictionary<Guid, HistoryItem> allItemLookup = new();
    private Dictionary<Guid, HistoryItem> x01ItemLookup = new();
    private Dictionary<Guid, HistoryItem> cricketItemLookup = new();
    private Dictionary<Guid, HistoryItem> atcItemLookup = new();

    [Header("Headlines für Löschen bei AddGame")]
    private GameObject allHeadline;
    private GameObject x01Headline;
    private GameObject cricketHeadline;
    private GameObject atcHeadline;

    private HashSet<Guid> existingGameIds = new HashSet<Guid>(); 

    private int deleteIndex;


    // =========================
    // UI LIFECYCLE
    // =========================
    private void Start()
    {
        StartCoroutine(BuildAllPages());
        appHandler.OnDeleteGame += HandleDeleteGame;
        appHandler.OnAddGame += HandleAddGame;
    }

    // =========================
    // PAGE BUILDING
    // =========================

    private IEnumerator BuildAllPages()
    {
        ClearAllItems();
        var games = appHandler.GetGames();

        // Deaktiviere LayoutGroups für schnelleres Laden
        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            // 1. Alle Spiele einsortieren (falls vorhanden)
            if (games != null && games.Count > 0)
            {
                foreach (var g in games)
                {
                    CreateItem(g, allGamesParent, allItems, allItemLookup);

                    if (g.GetGameMode() == GameMode.X01)
                        CreateItem(g, x01GamesParent, x01Items, x01ItemLookup);
                    else if (g.GetGameMode() == GameMode.ATC)
                        CreateItem(g, atcGamesParent, atcItems, atcItemLookup);
                    else if (g.GetGameMode() == GameMode.Cricket)
                        CreateItem(g, cricketGamesParent, cricketItems, cricketItemLookup);
                }
            }

            // 2. Jetzt für jede Seite prüfen: Ist sie leer geblieben?
            if (allItems.Count == 0)
                allHeadline = CreateHeadline(allGamesParent, "Keine Spiele vorhanden");

            if (x01Items.Count == 0)
                x01Headline = CreateHeadline(x01GamesParent, "Keine X01 Spiele vorhanden");

            if (atcItems.Count == 0)
                atcHeadline = CreateHeadline(atcGamesParent, "Keine ATC Spiele vorhanden");

            if (cricketItems.Count == 0)
                cricketHeadline = CreateHeadline(cricketGamesParent, "Keine Cricket Spiele vorhanden");
        }
        finally
        {
            // Reaktiviere LayoutGroups und rebuild einmalig
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }

        yield return null;
    }


    private GameObject CreateHeadline(Transform parent, string text)
    {
        if (prefabHeadline == null || parent == null)
            return null;

        var go = Instantiate(prefabHeadline, parent);
        var tmp = go.GetComponent<TMP_Text>();

        if (tmp != null)
            tmp.text = text;

        return go;
    }

    private void CreateItem(
        Game g,
        Transform parent,
        List<HistoryItem> list,
        Dictionary<Guid, HistoryItem> lookup)
    {
        existingGameIds.Add(g.GetID());

        HistoryItem item = Instantiate(prefab, parent);
        item.Setup(appHandler, g, HistoryItemMode.History, OnGameClicked);

        list.Add(item);
        lookup[g.GetID()] = item;
    }

    private void ClearAllItems()
    {
        allHeadline = null;
        x01Headline = null;
        atcHeadline = null;
        cricketHeadline = null;
        existingGameIds.Clear();
        ClearParent(allGamesParent);
        ClearParent(x01GamesParent);
        ClearParent(atcGamesParent);
        ClearParent(cricketGamesParent);

        // Listen trotzdem leeren (wichtig!)
        allItems.Clear();
        x01Items.Clear();
        atcItems.Clear();
        cricketItems.Clear();
        allItemLookup.Clear();
        x01ItemLookup.Clear();
        cricketItemLookup.Clear();
        atcItemLookup.Clear();
    }

    private void ClearParent(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }


    // =========================
    // USER INTERACTION
    // =========================

    private void OnGameClicked(Game game)
    {
        // Speichert ausgewähltes Spiel global im AppHandler
        appHandler.SetSelectedGame(game);

        // Wechselt in die Detailansicht
        windowHandler.GoTo(ScreenId.GameDetail);
    }


    // =========================
    // DELETE FLOW
    // =========================

    public void OnClickDeleteButton()
    {
        // Aktuelle Page wird als Ziel für Löschung gespeichert
        deleteIndex = swipeMenu.GetCurrentPage();

        // Zeigt entsprechendes Popup an
        windowHandler.ShowPopup(popups[deleteIndex]);
    }

    public void AbortDelete()
    {
        // Schließt Popup ohne Aktion
        windowHandler.HidePopup();
    }

    public void ConfirmDelete()
    {
        // Löschen abhängig von aktuell ausgewählter Seite
        if (deleteIndex == 0)
        {
            appHandler.DeleteAllGames();
        }
        else if (deleteIndex == 1)
        {
            appHandler.DeleteGamesOfMode(GameMode.X01);
        }
        else if (deleteIndex == 2)
        {
            appHandler.DeleteGamesOfMode(GameMode.Cricket);
        }
        else if (deleteIndex == 3)
        {
            appHandler.DeleteGamesOfMode(GameMode.ATC);
        }
        else
        {
            Debug.Log("[HistoryHandler] Unbekannter Index beim Löschen!");
        }

        // UI wird nach dem Löschen neu aufgebaut
        windowHandler.HidePopup();
        StartCoroutine(BuildAllPages());
    }

    private void HandleDeleteGame(GameMode mode, Guid gameId)
    {
        existingGameIds.Remove(gameId);

        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            // ------------------------
            // ALL
            // ------------------------
            if (allItemLookup.TryGetValue(gameId, out var allItem))
            {
                Destroy(allItem.gameObject);

                allItems.Remove(allItem);
                allItemLookup.Remove(gameId);
            }
            else
            {
                Debug.LogWarning(
                    $"[HistoryHandler] ALL Item NICHT gefunden für Guid={gameId}");
            }

            // ------------------------
            // Mode bestimmen
            // ------------------------
            List<HistoryItem> targetList = null;
            Dictionary<Guid, HistoryItem> targetLookup = null;

            switch (mode)
            {
                case GameMode.X01:
                    targetList = x01Items;
                    targetLookup = x01ItemLookup;
                    break;

                case GameMode.Cricket:
                    targetList = cricketItems;
                    targetLookup = cricketItemLookup;
                    break;

                case GameMode.ATC:
                    targetList = atcItems;
                    targetLookup = atcItemLookup;
                    break;
            }

            // ------------------------
            // Mode-Liste
            // ------------------------
            if (targetLookup != null)
            {
                if (targetLookup.TryGetValue(gameId, out var item))
                {
                    Destroy(item.gameObject);

                    targetList.Remove(item);
                    targetLookup.Remove(gameId);
                }
                else
                {
                    Debug.LogWarning(
                        $"[HistoryHandler] MODE Item NICHT gefunden für Guid={gameId}");
                }
            }

            // ------------------------
            // Platzhalter prüfen
            // ------------------------
            if (allItems.Count == 0 && allHeadline == null)
            {
                allHeadline = CreateHeadline(allGamesParent, "Keine Spiele vorhanden");
            }

            if (x01Items.Count == 0 && x01Headline == null)
            {
                x01Headline = CreateHeadline(x01GamesParent, "Keine X01 Spiele vorhanden");
            }

            if (cricketItems.Count == 0 && cricketHeadline == null)
            {
                cricketHeadline = CreateHeadline(cricketGamesParent, "Keine Cricket Spiele vorhanden");
            }

            if (atcItems.Count == 0 && atcHeadline == null)
            {
                atcHeadline = CreateHeadline(atcGamesParent, "Keine ATC Spiele vorhanden");
            }
        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }
    }

    private void HandleAddGame(Game game)
    {
        if (game == null) return;

        if (existingGameIds.Contains(game.GetID()))
        {
            DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

            try
            {
                UpdateAndMoveToTop(allItemLookup, allItems, game);

                switch (game.GetGameMode())
                {
                    case GameMode.X01:
                        UpdateAndMoveToTop(x01ItemLookup, x01Items, game);
                        break;

                    case GameMode.Cricket:
                        UpdateAndMoveToTop(cricketItemLookup, cricketItems, game);
                        break;

                    case GameMode.ATC:
                        UpdateAndMoveToTop(atcItemLookup, atcItems, game);
                        break;
                }
            }
            finally
            {
                EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
            }

            return;
        }

        else
        {
            existingGameIds.Add(game.GetID());

            // Deaktiviere LayoutGroups für schnelleres Hinzufügen
            DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

            try
            {
                // Placeholder der ALL-Seite entfernen
                RemoveHeadline(ref allHeadline);

                // --- ALL LISTE ---
                var allItem = Instantiate(prefab, allGamesParent);
                allItem.Setup(appHandler, game, HistoryItemMode.History, OnGameClicked);
                allItem.transform.SetSiblingIndex(0);
                allItems.Insert(0, allItem);
                allItemLookup[game.GetID()] = allItem;

                switch (game.GetGameMode())
                {
                    case GameMode.X01:
                    {
                        RemoveHeadline(ref x01Headline);

                        var item = Instantiate(prefab, x01GamesParent);
                        item.Setup(appHandler, game, HistoryItemMode.History, OnGameClicked);
                        item.transform.SetSiblingIndex(0);

                        x01Items.Insert(0, item);
                        x01ItemLookup[game.GetID()] = item;

                        break;
                    }

                    case GameMode.Cricket:
                    {
                        RemoveHeadline(ref cricketHeadline);

                        var item = Instantiate(prefab, cricketGamesParent);
                        item.Setup(appHandler, game, HistoryItemMode.History, OnGameClicked);
                        item.transform.SetSiblingIndex(0);

                        cricketItems.Insert(0, item);
                        cricketItemLookup[game.GetID()] = item;

                        break;
                    }

                    case GameMode.ATC:
                    {
                        RemoveHeadline(ref atcHeadline);

                        var item = Instantiate(prefab, atcGamesParent);
                        item.Setup(appHandler, game, HistoryItemMode.History, OnGameClicked);
                        item.transform.SetSiblingIndex(0);

                        atcItems.Insert(0, item);
                        atcItemLookup[game.GetID()] = item;

                        break;
                    }
                }
            }
            finally
            {
                EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
            }
        }
    }

    private void UpdateAndMoveToTop(
        Dictionary<Guid, HistoryItem> lookup,
        List<HistoryItem> list,
        Game game)
    {
        if (!lookup.TryGetValue(game.GetID(), out var item))
            return;

        item.Setup(appHandler, game, HistoryItemMode.History, OnGameClicked);

        item.transform.SetAsFirstSibling();

        list.Remove(item);
        list.Insert(0, item);
    }

    private void RemoveHeadline(ref GameObject headline)
    {
        if (headline != null)
        {
            Destroy(headline);
            headline = null;
        }
    }

    private void DisableLayoutGroups(params Transform[] parents)
    {
        foreach (var parent in parents)
        {
            if (parent == null) continue;
            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null)
                layout.enabled = false;
        }
    }

    private void EnableLayoutGroups(params Transform[] parents)
    {
        foreach (var parent in parents)
        {
            if (parent == null) continue;
            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.enabled = true;
            }
        }
    }

    public void OnShow()
    {
        
    }

    public void OnHide()
    {
        swipeMenu.ResetView();
    }
}
