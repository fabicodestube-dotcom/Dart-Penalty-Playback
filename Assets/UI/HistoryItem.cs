using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public enum HistoryItemMode
{
    History,
    Summary
}

public class HistoryItem : MonoBehaviour
{
    public HistoryItemHeadline headline;
    [Header("Prefabs and Content Parent")]
    public GameObject playerPrefab;
    public GameObject dividerPrefab;
    public Transform contentParent;

    [Header("Header UI Elements")]
    public TMP_Text textDate;
    public TMP_Text textSettings;
    public GameObject textGameStatusFinished;
    public GameObject textGameStatusRunning;


    private List<GameObject> items;
    private GameSummary gameSummary;
    private System.Action<Guid> onClick;
    private HistoryItemMode mode;
    private AppHandler historyAppHandler;
    private System.Func<Guid, string> historyPlayerNameResolver;

    private readonly Stack<GameObject> playerRowPool = new Stack<GameObject>();
    private readonly Stack<GameObject> dividerPool = new Stack<GameObject>();

    private static readonly Dictionary<string, string> settingsLabelCache = new Dictionary<string, string>();

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

        RenderHeader(summary);
        RenderBody(appHandler, summary, playerNameResolver);

        ApplyInteraction();

        headline.Initialize(summary);
    }

    /// <summary>
    /// Bereitet das Item für die Wiederverwendung aus dem Pool vor.
    /// Lässt gepoolte Player-Rows/Divider bestehen (nur deaktiviert),
    /// damit der nächste Setup-Aufruf ohne Instantiate auskommt.
    /// </summary>
    public void PrepareForPool()
    {
        Clear();
        onClick = null;
        var button = GetComponent<UnityEngine.UI.Button>();
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

        RenderHeader(gameSummary);
        RenderBody(historyAppHandler, gameSummary, historyPlayerNameResolver);
        headline?.RefreshLocalization();
    }

    private void RenderHeader(GameSummary summary)
    {
        var finishedAt = summary.FinishedAt;

        textDate.text = finishedAt.HasValue
            ? finishedAt.Value.ToString("dd.MM.yyyy HH:mm")
            : LocalizationText.Format("Game_running_since", "Game still running (as of {0})", summary.LastActivityAt.ToString("dd.MM.yyyy HH:mm"));

        if (finishedAt.HasValue)
        {
            textGameStatusRunning.SetActive(false);
            textGameStatusFinished.SetActive(true);
        }
        else
        {
            textGameStatusFinished.SetActive(false);
            textGameStatusRunning.SetActive(true);
        }

        if (summary.GameMode == GameMode.X01)
        {
            textSettings.text = "X01 (" + GetSettingsString(summary) + ")";
        }
        else if (summary.GameMode == GameMode.Cricket)
        {
            textSettings.text = "Cricket (" + GetSettingsString(summary) + ")";
        }
        else if (summary.GameMode == GameMode.ATC)
        {
            textSettings.text = "ATC (" + GetSettingsString(summary) + ")";
        }
    }

    private string GetSettingsString(GameSummary summary)
    {
        if (string.IsNullOrEmpty(summary.SettingsJson))
            return "";

        if (settingsLabelCache.TryGetValue(summary.SettingsJson, out var cached))
            return cached;

        try
        {
            var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<GameSettings>(summary.SettingsJson);
            var label = settings?.GetString() ?? "";
            if (settingsLabelCache.Count > 200)
                settingsLabelCache.Clear();
            settingsLabelCache[summary.SettingsJson] = label;
            return label;
        }
        catch
        {
            return "";
        }
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
        if (summary.GameMode == GameMode.X01)
        {
            var sortedPlayers = playerEntries
                .OrderByDescending(p => p.SetsWon)
                .ThenByDescending(p => p.LegsWon)
                .ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var entry = sortedPlayers[i];

                var go = GetPooledPlayerRow();
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: ResolvePlayerName(appHandler, entry.PlayerId, playerNameResolver),
                    metricLabel: LocalizationText.Get("Points", "Points") + ": " + entry.Score.ToString(),
                    stats: BuildPenaltyStats(entry, summary),
                    setsWonOverride: entry.SetsWon,
                    legsWonOverride: summary.IsFinished ? 0 : entry.LegsWon
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
        else if (summary.GameMode == GameMode.Cricket)
        {
            var sortedPlayers = playerEntries
                .OrderByDescending(p => p.SetsWon)
                .ThenByDescending(p => p.LegsWon)
                .ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var entry = sortedPlayers[i];

                var go = GetPooledPlayerRow();
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: ResolvePlayerName(appHandler, entry.PlayerId, playerNameResolver),
                    metricLabel: LocalizationText.Get("Points", "Points") + ": " + entry.Score.ToString(),
                    stats: BuildPenaltyStats(entry, summary),
                    setsWonOverride: entry.SetsWon,
                    legsWonOverride: summary.IsFinished ? 0 : entry.LegsWon
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
        else if (summary.GameMode == GameMode.ATC)
        {
            var sortedPlayers = playerEntries
                .OrderByDescending(p => p.TargetsHit)
                .ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var entry = sortedPlayers[i];

                var go = GetPooledPlayerRow();
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: ResolvePlayerName(appHandler, entry.PlayerId, playerNameResolver),
                    metricLabel: LocalizationText.Get("Targets", "Targets") + ": " + $"{entry.TargetsHit} / {entry.TotalTargets}",
                    stats: BuildPenaltyStats(entry, summary),
                    setsWonOverride: entry.SetsWon,
                    legsWonOverride: summary.IsFinished ? 0 : entry.LegsWon
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
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

        // Custom-Anteil der Gesamtkosten aus dem Spiel-Snapshot (Kosten zum
        // Spielzeitpunkt) — sonst zeigte GetTotalPenaltyCosts() nur Legacy.
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

        return Instantiate(playerPrefab, contentParent);
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

        return Instantiate(dividerPrefab, contentParent);
    }

    private void ApplyInteraction()
    {
        var button = GetComponent<UnityEngine.UI.Button>();

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
        if (dividerPrefab != null && index < totalCount - 1)
        {
            var divider = GetPooledDivider();
            items.Add(divider);
        }
    }
}
