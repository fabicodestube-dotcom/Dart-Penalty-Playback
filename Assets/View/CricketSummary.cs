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

    private Game game;


    public void ShowSummary(Game game)
    {
        item.Setup(appHandler, game, HistoryItemMode.Summary);
        this.game = game;
    }

    public void OnClickShowStatistic()
    {
        appHandler.SetSelectedGame(game);
        windowHandler.GoTo(ScreenId.GameDetail);
    }

    public void OnClickUndoLastDart()
    {
        cricketGameEngine.Undo();
        windowHandler.GoTo(ScreenId.CricketGame);
    }

    public void OnClickSaveGame()
    {
        appHandler.SaveDatabase();
        windowHandler.GoTo(ScreenId.Zoggen);
    }

    public void OnClickDeleteGame()
    {
        appHandler.DeleteGame(game.GetID());
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