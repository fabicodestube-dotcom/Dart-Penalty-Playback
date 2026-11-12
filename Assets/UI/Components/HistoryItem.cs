using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum HistoryItemMode
{
    History,
    Summary
}

public class HistoryItem : MonoBehaviour
{
    public HistoryItemHeadline headline;
    [Header("Prefabs and Content Parent")]
    public GameObject playerRowTemplate;
    public Transform contentParent;

    private List<GameObject> items;
    private GameSummary gameSummary;
    private System.Action<Guid> onClick;
    private HistoryItemMode mode;
    private AppHandler historyAppHandler;
    private System.Func<Guid, string> historyPlayerNameResolver;

    private readonly Stack<GameObject> playerRowPool = new Stack<GameObject>();
    private readonly Stack<GameObject> dividerPool = new Stack<GameObject>();

    private void Awake()
    {
        DisableTextRaycasts();
    }

    private void DisableTextRaycasts()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
            text.raycastTarget = false;
    }

    public void Setup(AppHandler appHandler, GameSummary summary, HistoryItemMode mode, System.Action<Guid> onClick = null)
    {
        Setup(appHandler, summary, mode, onClick, null);
    }

    public void Setup(AppHandler appHandler, GameSummary summary, HistoryItemMode mode, System.Action<Guid> onClick, System.Func<Guid, string> playerNameResolver)
    {
        this.gameSummary = summary;
        this.mode = mode;
        this.onClick = onClick;
        this.historyAppHandler = appHandler;
        this.historyPlayerNameResolver = playerNameResolver;

        headline.Initialize(summary);
        RenderBody(appHandler, summary, playerNameResolver);
        ApplyInteraction();
    }

    /// <summary>
    /// Prepares the item for reuse from the pool.
    /// Allows pooled player rows/dividers (only deactivated),
    /// so that the next setup call does not require instantiations.
    /// </summary>
    public void PrepareForPool()
    {
        Clear();
        onClick = null;
        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.RemoveAllListeners();
    }

    public Guid GetGameID()
    {
        return gameSummary.Id;
    }

    public void RefreshLocalization()
    {
        if (gameSummary == null)
            return;

        RenderBody(historyAppHandler, gameSummary, historyPlayerNameResolver);
        headline?.RefreshLocalization();
    }

    private string ResolvePlayerName(AppHandler appHandler, Guid playerId, System.Func<Guid, string> resolver)
    {
        if (resolver != null)
        {
            var resolved = resolver(playerId);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        return appHandler.GetPlayerNameByID(playerId);
    }

    private void RenderBody(AppHandler appHandler, GameSummary summary, System.Func<Guid, string> playerNameResolver = null)
    {
        Clear();

        var playerEntries = summary.PlayerEntries;
        if (playerEntries == null)
            return;

        List<PlayerSummaryEntry> sortedPlayers;
        string metricPrefix;

        if (summary.GameMode == GameMode.ATC)
        {
            sortedPlayers = playerEntries
                .OrderByDescending(p => p.TargetsHit)
                .ToList();
            metricPrefix = LocalizationText.Get("Targets", "Targets");
        }
        else
        {
            sortedPlayers = playerEntries
                .OrderByDescending(p => p.SetsWon)
                .ThenByDescending(p => p.LegsWon)
                .ToList();
            metricPrefix = LocalizationText.Get("Points", "Points");
        }

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var entry = sortedPlayers[i];
            string metricLabel = summary.GameMode == GameMode.ATC
                ? metricPrefix + ": " + $"{entry.TargetsHit} / {entry.TotalTargets}"
                : metricPrefix + ": " + entry.Score.ToString();

            RenderPlayerRow(
                appHandler,
                summary,
                playerNameResolver,
                i,
                sortedPlayers.Count,
                entry,
                metricLabel);
        }
    }

    private void RenderPlayerRow(
        AppHandler appHandler,
        GameSummary summary,
        System.Func<Guid, string> playerNameResolver,
        int index,
        int totalCount,
        PlayerSummaryEntry entry,
        string metricLabel)
    {
        var go = GetPooledPlayerRow();
        if (go == null)
            return;

        var item = go.GetComponent<HistoryItemPlayerPrefab>();

        item.ShowPlayer(
            rank: index + 1,
            playerName: ResolvePlayerName(appHandler, entry.PlayerId, playerNameResolver),
            metricLabel: metricLabel,
            stats: BuildPenaltyStats(entry, summary),
            setsWonOverride: entry.SetsWon,
            legsWonOverride: entry.LegsWon
        );

        items.Add(item.gameObject);
        CreateDividerIfNeeded(index, totalCount);
    }

    private static GameStats BuildPenaltyStats(PlayerSummaryEntry entry, GameSummary summary)
    {
        var stats = new GameStats(entry.PlayerId)
        {
            wallCount = entry.WallCount,
            ceilingCount = entry.CeilingCount,
            allMissCount = entry.AllMissCount,
            tripleOnesCount = entry.ThreeOnesCount,
            tripleDigitCount = entry.TripleDigitCount,
            lostGame = entry.LostGameCount
        };

        // Custom share of the total cost from the game snapshot (cost to the game)
        // Time of play) — otherwise GetTotalPenaltyCosts() showed only Legacy.
        if (entry.CustomPenaltyCounts != null && summary?.CustomPenaltySnapshot != null)
        {
            foreach (var kvp in entry.CustomPenaltyCounts)
            {
                if (kvp.Value <= 0)
                    continue;

                var def = summary.CustomPenaltySnapshot.Find(d => d != null && d.id == kvp.Key);
                if (def != null)
                    stats.AddCustomPenaltyCost(kvp.Value * def.cost);
            }
        }

        return stats;
    }

    private void Clear()
    {
        if (items != null)
        {
            foreach (var go in items)
            {
                if (go == null)
                    continue;
                go.SetActive(false);
                if (go.GetComponent<HistoryItemPlayerPrefab>() != null)
                    playerRowPool.Push(go);
                else
                    dividerPool.Push(go);
            }

            items.Clear();
        }
        else
        {
            items = new List<GameObject>();
        }
    }

    private GameObject GetPooledPlayerRow()
    {
        while (playerRowPool.Count > 0)
        {
            var go = playerRowPool.Pop();
            if (go == null)
                continue;
            go.transform.SetParent(contentParent, false);
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            return go;
        }

        if (playerRowTemplate == null)
        {
            Debug.LogError("HistoryItem: playerRowTemplate is not assigned.");
            return null;
        }

        var instance = Instantiate(playerRowTemplate, contentParent);
        instance.SetActive(true);
        return instance;
    }

    private GameObject GetPooledDivider()
    {
        while (dividerPool.Count > 0)
        {
            var go = dividerPool.Pop();
            if (go == null)
                continue;
            go.transform.SetParent(contentParent, false);
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            return go;
        }

        return CreateDivider();
    }

    private GameObject CreateDivider()
    {
        var root = new GameObject(
            "Divider",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement),
            typeof(ThemeColorBinding));

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(contentParent, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(1250f, 2f);

        var rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.16470589f, 0.16470589f, 0.16470589f, 1f);
        rootImage.raycastTarget = false;

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = -1f;
        layout.preferredHeight = 2f;
        layout.flexibleHeight = -1f;

        root.GetComponent<ThemeColorBinding>().SetRole(ThemeColorRole.Background4);

        var line = new GameObject(
            "Line",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ThemeColorBinding));

        var lineRect = line.GetComponent<RectTransform>();
        lineRect.SetParent(rootRect, false);
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(-80f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);

        var lineImage = line.GetComponent<Image>();
        lineImage.color = Color.white;
        lineImage.raycastTarget = false;

        line.GetComponent<ThemeColorBinding>().SetRole(ThemeColorRole.Text1);

        return root;
    }

    private void ApplyInteraction()
    {
        var button = GetComponent<Button>();

        if (mode == HistoryItemMode.History)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(gameSummary.Id));
        }
        else
        {
            button.interactable = false;
        }
    }

    private void CreateDividerIfNeeded(int index, int totalCount)
    {
        if (index < totalCount - 1)
        {
            var divider = GetPooledDivider();
            items.Add(divider);
        }
    }
}
