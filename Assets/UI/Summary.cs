using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Summary : MonoBehaviour
{
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public X01GameEngine x01GameEngine;
    public CricketGameEngine cricketGameEngine;
    public ATCGameEngine atcGameEngine;
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
        if (loadedGame == null) return;

        appHandler.SetSelectedGame(loadedGame);

        if (gameSummary.GameMode == GameMode.X01)
        {
            windowHandler.Replace(ScreenId.X01Game);
            x01GameEngine.LoadGame((X01Game)loadedGame);
            x01GameEngine.Undo();
        }
        else if (gameSummary.GameMode == GameMode.Cricket)
        {
            windowHandler.Replace(ScreenId.CricketGame);
            cricketGameEngine.LoadGame((CricketGame)loadedGame);
            cricketGameEngine.Undo();
        }
        else if (gameSummary.GameMode == GameMode.ATC)
        {
            windowHandler.Replace(ScreenId.ATCGame);
            atcGameEngine.LoadGame((ATCGame)loadedGame);
            atcGameEngine.Undo();
        }
    }

    public void OnClickSaveGame()
    {
        if (gameSummary == null) return;

        var loadedGame = appHandler.LoadGameById(gameSummary.Id);
        if (loadedGame == null || !appHandler.SaveGame(loadedGame))
        {
            Debug.LogError("Summary: Spiel konnte nicht gespeichert werden.");
            return;
        }

        windowHandler.HidePopup(0f);
        windowHandler.ResetTo(ScreenId.Zoggen);
    }

    public void OnClickDeleteGame()
    {
        if (gameSummary == null) return;

        if (!appHandler.DeleteGame(gameSummary.Id))
        {
            Debug.LogError("Summary: Spiel konnte nicht gelöscht werden.");
            return;
        }

        windowHandler.HidePopup(0f);
        windowHandler.ResetTo(ScreenId.Zoggen);
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
