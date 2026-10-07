using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
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

    private void Start()
    {
        StartCoroutine(BuildAllPages());
        appHandler.OnDeleteGame += HandleDeleteGame;
        appHandler.OnAddGame += HandleAddGame;
        appHandler.OnDeleteGamesOfMode += HandleDeleteGamesOfMode;
        appHandler.OnAllGamesDeleted += HandleAllGamesDeleted;
    }

    private IEnumerator BuildAllPages()
    {
        ClearAllItems();
        var summaries = appHandler.GetGameSummaries();

        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            if (summaries != null && summaries.Count > 0)
            {
                foreach (var s in summaries)
                {
                    CreateItem(s, allGamesParent, allItems, allItemLookup);

                    if (s.GameMode == GameMode.X01)
                        CreateItem(s, x01GamesParent, x01Items, x01ItemLookup);
                    else if (s.GameMode == GameMode.ATC)
                        CreateItem(s, atcGamesParent, atcItems, atcItemLookup);
                    else if (s.GameMode == GameMode.Cricket)
                        CreateItem(s, cricketGamesParent, cricketItems, cricketItemLookup);
                }
            }

            if (allItems.Count == 0)
                allHeadline = CreateHeadline(allGamesParent, "No_games_available", "No games available");

            if (x01Items.Count == 0)
                x01Headline = CreateHeadline(x01GamesParent, "No_games_available_x01", "No X01 games available");

            if (atcItems.Count == 0)
                atcHeadline = CreateHeadline(atcGamesParent, "No_games_available_atc", "No Around the Clock games available");

            if (cricketItems.Count == 0)
                cricketHeadline = CreateHeadline(cricketGamesParent, "No_games_available_cricket", "No Cricket games available");

        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }

        yield return null;
    }

    private GameObject CreateHeadline(Transform parent, string localizationKey, string fallbackText)
    {
        if (prefabHeadline == null || parent == null)
            return null;

        var go = Instantiate(prefabHeadline, parent);
        var tmp = go.GetComponent<TMP_Text>();

        if (tmp != null)
        {
            // Sofort sinnvoller Text, damit nie das Prefab-Default ("Titel") steht.
            tmp.text = fallbackText;

            var localizeEvent = go.GetComponent<LocalizeStringEvent>()
                ?? go.AddComponent<LocalizeStringEvent>();

            // Listener VOR dem Setzen der Referenz anhängen, sonst kann das
            // (asynchrone) Update verloren gehen.
            localizeEvent.OnUpdateString.RemoveAllListeners();
            localizeEvent.OnUpdateString.AddListener(localizedValue =>
            {
                if (!string.IsNullOrEmpty(localizedValue))
                    tmp.text = localizedValue;
            });
            localizeEvent.StringReference = new LocalizedString("LocalizationTable", localizationKey);
            localizeEvent.RefreshString();
        }

        return go;
    }

    private void CreateItem(
        GameSummary summary,
        Transform parent,
        List<HistoryItem> list,
        Dictionary<Guid, HistoryItem> lookup)
    {
        existingGameIds.Add(summary.Id);

        HistoryItem item = Instantiate(prefab, parent);
        item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);

        list.Add(item);
        lookup[summary.Id] = item;
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

    private void OnGameClicked(Guid gameId)
    {
        appHandler.SetSelectedGame(null);
        appHandler.SetLastClickedGameId(gameId);
        windowHandler.GoTo(ScreenId.GameDetail);
    }

    public void OnClickDeleteButton()
    {
        deleteIndex = swipeMenu.GetCurrentPage();
        windowHandler.ShowPopup(popups[deleteIndex]);
    }

    public void AbortDelete()
    {
        windowHandler.HidePopup();
    }

    public void ConfirmDelete()
    {
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

        windowHandler.HidePopup();
    }

    private void HandleDeleteGame(GameMode mode, Guid gameId)
    {
        existingGameIds.Remove(gameId);

        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
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

            if (allItems.Count == 0 && allHeadline == null)
            {
                allHeadline = CreateHeadline(allGamesParent, "No_games_available", "No games available");
            }

            if (x01Items.Count == 0 && x01Headline == null)
            {
                x01Headline = CreateHeadline(x01GamesParent, "No_games_available_x01", "No X01 games available");
            }

            if (cricketItems.Count == 0 && cricketHeadline == null)
            {
                cricketHeadline = CreateHeadline(cricketGamesParent, "No_games_available_cricket", "No Cricket games available");
            }

            if (atcItems.Count == 0 && atcHeadline == null)
            {
                atcHeadline = CreateHeadline(atcGamesParent, "No_games_available_atc", "No Around the Clock games available");
            }
        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }
    }

    private void HandleDeleteGamesOfMode(GameMode mode)
    {
        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            if (mode == GameMode.X01)
            {
                RemoveModeItems(x01Items, x01ItemLookup);

                if (x01Headline == null)
                    x01Headline = CreateHeadline(x01GamesParent, "No_games_available_x01", "No X01 games available");
            }
            else if (mode == GameMode.Cricket)
            {
                RemoveModeItems(cricketItems, cricketItemLookup);

                if (cricketHeadline == null)
                    cricketHeadline = CreateHeadline(cricketGamesParent, "No_games_available_cricket", "No Cricket games available");
            }
            else if (mode == GameMode.ATC)
            {
                RemoveModeItems(atcItems, atcItemLookup);

                if (atcHeadline == null)
                    atcHeadline = CreateHeadline(atcGamesParent, "No_games_available_atc", "No Around the Clock games available");
            }

            if (allItems.Count == 0 && allHeadline == null)
            {
                allHeadline = CreateHeadline(allGamesParent, "No_games_available", "No games available");
            }
        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }
    }

    private void HandleAllGamesDeleted()
    {
        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            ClearAllItems();

            allHeadline = CreateHeadline(allGamesParent, "No_games_available", "No games available");
            x01Headline = CreateHeadline(x01GamesParent, "No_games_available_x01", "No X01 games available");
            atcHeadline = CreateHeadline(atcGamesParent, "No_games_available_atc", "No Around the Clock games available");
            cricketHeadline = CreateHeadline(cricketGamesParent, "No_games_available_cricket", "No Cricket games available");
        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }
    }

    private void HandleAddGame(Game game)
    {
        if (game == null) return;

        var summary = appHandler.GetGameSummary(game.GetID());
        if (summary == null) return;

        if (existingGameIds.Contains(summary.Id))
        {
            DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

            try
            {
                UpdateAndMoveToTop(allItemLookup, allItems, summary);

                switch (summary.GameMode)
                {
                    case GameMode.X01:
                        UpdateAndMoveToTop(x01ItemLookup, x01Items, summary);
                        break;

                    case GameMode.Cricket:
                        UpdateAndMoveToTop(cricketItemLookup, cricketItems, summary);
                        break;

                    case GameMode.ATC:
                        UpdateAndMoveToTop(atcItemLookup, atcItems, summary);
                        break;
                }
            }
            finally
            {
                EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
            }

            return;
        }

        existingGameIds.Add(summary.Id);

        DisableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);

        try
        {
            RemoveHeadline(ref allHeadline);

            var allItem = Instantiate(prefab, allGamesParent);
            allItem.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);
            allItem.transform.SetSiblingIndex(0);
            allItems.Insert(0, allItem);
            allItemLookup[summary.Id] = allItem;

            switch (summary.GameMode)
            {
                case GameMode.X01:
                {
                    RemoveHeadline(ref x01Headline);

                    var item = Instantiate(prefab, x01GamesParent);
                    item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);
                    item.transform.SetSiblingIndex(0);

                    x01Items.Insert(0, item);
                    x01ItemLookup[summary.Id] = item;

                    break;
                }

                case GameMode.Cricket:
                {
                    RemoveHeadline(ref cricketHeadline);

                    var item = Instantiate(prefab, cricketGamesParent);
                    item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);
                    item.transform.SetSiblingIndex(0);

                    cricketItems.Insert(0, item);
                    cricketItemLookup[summary.Id] = item;

                    break;
                }

                case GameMode.ATC:
                {
                    RemoveHeadline(ref atcHeadline);

                    var item = Instantiate(prefab, atcGamesParent);
                    item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);
                    item.transform.SetSiblingIndex(0);

                    atcItems.Insert(0, item);
                    atcItemLookup[summary.Id] = item;

                    break;
                }
            }
        }
        finally
        {
            EnableLayoutGroups(allGamesParent, x01GamesParent, atcGamesParent, cricketGamesParent);
        }
    }

    private void UpdateAndMoveToTop(
        Dictionary<Guid, HistoryItem> lookup,
        List<HistoryItem> list,
        GameSummary summary)
    {
        if (!lookup.TryGetValue(summary.Id, out var item))
            return;

        item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked);

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

    /// <summary>
    /// Entfernt alle Items eines Modus-Tabs mitsamt ihren Duplikaten im All-Tab.
    /// </summary>
    private void RemoveModeItems(List<HistoryItem> modeItems, Dictionary<Guid, HistoryItem> modeLookup)
    {
        foreach (var item in modeItems.ToList())
        {
            var id = item.GetGameID();
            Destroy(item.gameObject);

            if (allItemLookup.TryGetValue(id, out var allItem))
            {
                Destroy(allItem.gameObject);
                allItems.Remove(allItem);
                allItemLookup.Remove(id);
            }

            existingGameIds.Remove(id);
        }

        modeItems.Clear();
        modeLookup.Clear();
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

    private void OnDestroy()
    {
        if (appHandler != null)
        {
            appHandler.OnDeleteGame -= HandleDeleteGame;
            appHandler.OnAddGame -= HandleAddGame;
            appHandler.OnDeleteGamesOfMode -= HandleDeleteGamesOfMode;
            appHandler.OnAllGamesDeleted -= HandleAllGamesDeleted;
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
