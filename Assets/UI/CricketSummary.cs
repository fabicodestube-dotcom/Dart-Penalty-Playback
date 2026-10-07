using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CricketSummary : MonoBehaviour
{
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public CricketGameEngine cricketGameEngine;
    public UIScreen popup;

    public HistoryItem item;

    private GameSummary gameSummary;
    private Game game;

    public void ShowSummary(GameSummary summary)
    {
        gameSummary = summary;
        item.Setup(appHandler, summary, HistoryItemMode.Summary);
    }

    public void OnClickShowStatistic()
    {
        if (gameSummary == null) return;

        var loadedGame = appHandler.LoadGameById(gameSummary.Id);
        if (loadedGame != null)
        {
            appHandler.SetSelectedGame(loadedGame);
        }
        windowHandler.GoTo(ScreenId.GameDetail);
    }

    public void OnClickUndoLastDart()
    {
        if (gameSummary == null) return;

        var loadedGame = appHandler.LoadGameById(gameSummary.Id);
        if (loadedGame != null)
        {
            appHandler.SetSelectedGame(loadedGame);
            cricketGameEngine.LoadGame((CricketGame)loadedGame);
            cricketGameEngine.Undo();
        }
        windowHandler.GoTo(ScreenId.CricketGame);
    }

    public void OnClickSaveGame()
    {
        if (gameSummary == null) return;

        var loadedGame = appHandler.LoadGameById(gameSummary.Id);
        if (loadedGame != null)
        {
            appHandler.SaveGame(loadedGame);
        }
        windowHandler.GoTo(ScreenId.Zoggen);
    }

    public void OnClickDeleteGame()
    {
        if (gameSummary == null) return;

        appHandler.DeleteGame(gameSummary.Id);
        windowHandler.GoTo(ScreenId.Zoggen);
    }

    public void OnClickBackButton()
    {
        windowHandler.ShowPopup(popup);
    }

    public void OnClickHidePopup()
    {
        windowHandler.HidePopup();
    }
}
