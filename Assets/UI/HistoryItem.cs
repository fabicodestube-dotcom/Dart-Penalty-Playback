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


    public void Setup(AppHandler appHandler, GameSummary summary, HistoryItemMode mode, System.Action<Guid> onClick = null)
    {
        this.gameSummary = summary;
        this.mode = mode;
        this.onClick = onClick;

        RenderHeader(summary);
        RenderBody(appHandler, summary);

        ApplyInteraction();

        headline.Initialize(summary);
    }

    public Guid GetGameID()
    {
        return gameSummary.Id;
    }

    private void RenderHeader(GameSummary summary)
    {
        var finishedAt = summary.FinishedAt;

        textDate.text = finishedAt.HasValue
            ? finishedAt.Value.ToString("dd.MM.yyyy HH:mm")
            : $"Spiel läuft noch (Stand: {summary.LastActivityAt:dd.MM.yyyy HH:mm})";

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

        try
        {
            var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<GameSettings>(summary.SettingsJson);
            return settings?.GetString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private void RenderBody(AppHandler appHandler, GameSummary summary)
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

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(entry.PlayerId),
                    metricLabel: "Punkte: " + entry.Score.ToString(),
                    stats: BuildPenaltyStats(entry),
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

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(entry.PlayerId),
                    metricLabel: "Punkte: " + entry.Score.ToString(),
                    stats: BuildPenaltyStats(entry),
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

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(entry.PlayerId),
                    metricLabel: "Stand: " + $"{entry.TargetsHit} / {entry.TotalTargets}",
                    stats: BuildPenaltyStats(entry),
                    setsWonOverride: entry.SetsWon,
                    legsWonOverride: summary.IsFinished ? 0 : entry.LegsWon
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
    }

    private static GameStats BuildPenaltyStats(PlayerSummaryEntry entry)
    {
        return new GameStats(entry.PlayerId)
        {
            wallCount = entry.WallCount,
            ceilingCount = entry.CeilingCount,
            allMissCount = entry.AllMissCount,
            tripleOnesCount = entry.ThreeOnesCount,
            tripleDigitCount = entry.TripleDigitCount,
            lostGame = entry.LostGameCount
        };
    }

    private void Clear()
    {
        if (items != null)
        {
            foreach (var go in items)
                Destroy(go);

            items.Clear();
        }
        else
        {
            items = new List<GameObject>();
        }
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
            var divider = Instantiate(dividerPrefab, contentParent);
            items.Add(divider);
        }
    }
}
