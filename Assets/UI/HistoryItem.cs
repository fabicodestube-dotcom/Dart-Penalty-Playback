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
    private Game game;
    private System.Action<Game> onClick;
    private HistoryItemMode mode;


    public void Setup(AppHandler appHandler, Game game, HistoryItemMode mode, System.Action<Game> onClick = null)
    {
        this.game = game;
        this.mode = mode;
        this.onClick = onClick;

        RenderHeader(game);
        RenderBody(appHandler, game);

        ApplyInteraction();

        headline.Initialize(game);
    }

    public Guid GetGameID()
    {
        return game.GetID();
    }

    private void RenderHeader(Game g)
    {
        var finishedAt = g.GetFinishedAt();

        textDate.text = finishedAt.HasValue
            ? finishedAt.Value.ToString("dd.MM.yyyy HH:mm")
            : $"Spiel läuft noch (Stand: {g.GetLastActivityAt():dd.MM.yyyy HH:mm})";

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

        if (g is X01Game)
        {
            textSettings.text = "X01 (" + g.GetSettings().GetString() + ")";
        }
        else if (g is CricketGame)
        {
            textSettings.text = "Cricket (" + g.GetSettings().GetString() + ")";
        }
        else if (g is ATCGame)
        {
            textSettings.text = "ATC (" + g.GetSettings().GetString() + ")";
        }
    }

    private void RenderPenalties(Game g)
    {
        var settings = g.GetSettings();

        if (settings == null)
        {
            Debug.LogWarning(" No settings found for game ID " + g.GetID());
            return;
        }
    }

    private void RenderBody(AppHandler appHandler, Game g)
    {
        Clear();

        if (g.GetGameMode() == GameMode.X01)
        {
            var game = (X01Game)g;

            var playerIDs = game.GetPlayerIDs();

            var sortedPlayers = playerIDs
                .OrderByDescending(p => game.GetWonSets(p))
                .ThenByDescending(p => game.GetWonLegs(p))
                .ToList();

            var stats = game.GetPlayerStats();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                Guid pid = sortedPlayers[i];

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                var turns = game.GetAllTurns(pid);

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(pid),
                    metricLabel: "Punkte: " + game.GetScore(pid).ToString(),
                    stats: stats[pid]
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
        else if (g.GetGameMode() == GameMode.Cricket)
        {
            var game = (CricketGame)g;

            var playerIDs = game.GetPlayerIDs();

            var sortedPlayers = playerIDs
                .OrderByDescending(p => game.GetWonSets(p))
                .ThenByDescending(p => game.GetWonLegsTotal(p))
                .ToList();


            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                Guid pid = sortedPlayers[i];

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                var turns = game.GetAllTurns(pid);

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(pid),
                    metricLabel: "Punkte: " + game.GetScore(pid).ToString(),
                    stats: game.GetPlayerStats()[pid]
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
        else if (g.GetGameMode() == GameMode.ATC)
        {
            var game = (ATCGame)g;

            var playerIDs = game.GetPlayerIDs();

            var sortedPlayers = playerIDs
                .OrderByDescending(p => game.GetWonSetsTotal(p))
                .ThenByDescending(p => game.GetWonLegsTotal(p))
                .ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                Guid pid = sortedPlayers[i];

                var go = Instantiate(playerPrefab, contentParent);
                var item = go.GetComponent<HistoryItemPlayerPrefab>();

                var turns = game.GetAllTurns(pid);
                var stats = game.GetPlayerStats()[pid];

                item.ShowPlayer(
                    rank: i + 1,
                    playerName: appHandler.GetPlayerNameByID(pid),
                    metricLabel: "Stand: " + $"{game.GetTargetsHit(pid)} / {game.GetTotalTargets()}",
                    stats: game.GetPlayerStats()[pid]
                );

                items.Add(item.gameObject);
                CreateDividerIfNeeded(i, sortedPlayers.Count);
            }
        }
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
            button.onClick.AddListener(() => onClick?.Invoke(game));
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
