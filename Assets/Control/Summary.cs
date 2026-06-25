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
        if (game.GetGameMode() == GameMode.X01)
        {
            windowHandler.GoTo(ScreenId.X01Game);
            x01GameEngine.Undo();
        }
        else if (game.GetGameMode() == GameMode.Cricket)
        {
            windowHandler.GoTo(ScreenId.CricketGame);
            cricketGameEngine.Undo();
        }

        else if (game.GetGameMode() == GameMode.ATC)
        {
            windowHandler.GoTo(ScreenId.ATCGame);
            atcGameEngine.Undo();
        }
    }

    public void OnClickSaveGame()
    {
        appHandler.SaveGame(game);
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
