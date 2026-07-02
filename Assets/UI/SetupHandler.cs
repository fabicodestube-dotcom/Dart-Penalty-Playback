using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class SetupHandler : MonoBehaviour, IUIScreen
{   
    [Header("Control")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;

    [Header("Engines")]
    public X01GameEngine x01GameEngine;
    public CricketGameEngine cricketGameEngine;
    public ATCGameEngine atcGameEngine;

    [Header("UI")]
    public SetupPlayerList activePlayerList;

    [Header("UI")]
    public SingleSelectionGroup topGroup;
    public GameObject x01Panel;
    public GameObject cricketPanel;
    public GameObject atcPanel;

    [Header("UI X01")]
    public TMP_Text x01PointsLabel;
    public TMP_Text x01CheckinLabel;
    public TMP_Text x01CheckoutLabel;
    
    [Header("UI Cricket")]
    public CanvasGroup cricketModeCanvas;
    public TMP_Text cricketPointsLabel;
    public TMP_Text cricketModeLabel;

    [Header("UI ATC")]
    public TMP_Text atcTargetLabel;
    public TMP_Text atcOrderLabel;

    [Header("UI Sets and Legs")]
    public TMP_Text setCountLabel;
    public TMP_Text setsAndLegsLabel;
    public TMP_Text legCountLabel;
    public SetsAndLegsScrollView setsScrollView;
    public SetsAndLegsScrollView legsScrollView;


    public CustomToggle toggleRandomOrder;
    public SetupPlayerList activeList;
    public SetupPlayerList reserveList;
    public UIScreen popupNoPlayersAlert;
    public UIScreen popupAddBot;
    public SingleSelectionGroup botDifficultyGroup;


    [Header("Private")]
    private X01GameSettings x01Settings;
    private CricketGameSettings cricketSettings;
    private ATCGameSettings atcSettings;
    private SetsAndLegs setsAndLegsMode;
    private int setCount = 1;
    private int legCount = 1;


    private void Start()
    {
        ApplyStoredSettings();
    }

    public void OnShow()
    {
        activeList.ShowPlayers();
        reserveList.ShowPlayers();
    }

    public void OnHide()
    {
        // optional cleanup
    }

    private void ApplyStoredSettings()
    {
        ApplyX01Settings();
        ApplyCricketSettings();
        ApplyATCSettings();
        ApplySetsAndLegs();

        if (appHandler.GetLastGameMode() == GameMode.X01)
        {
            topGroup.Init(0);
            x01Panel.SetActive(true);
            cricketPanel.SetActive(false);
            atcPanel.SetActive(false);
        }
        else if (appHandler.GetLastGameMode() == GameMode.Cricket)
        {
            topGroup.Init(1);
            x01Panel.SetActive(false);
            cricketPanel.SetActive(true);
            atcPanel.SetActive(false);
        }

        else if (appHandler.GetLastGameMode() == GameMode.ATC)
        {
            topGroup.Init(2);
            x01Panel.SetActive(false);
            cricketPanel.SetActive(false);
            atcPanel.SetActive(true);
        }
    }

    private void ApplyX01Settings()
    {
        x01Settings = appHandler.GetX01Settings();
        x01PointsLabel.text = x01Settings.pointTarget.ToString();
        x01CheckinLabel.text = x01Settings.checkinType.ToDescription();
        x01CheckoutLabel.text = x01Settings.checkoutType.ToDescription();
    }

    private void ApplyCricketSettings()
    {
        cricketSettings = appHandler.GetCricketSettings();
        if (!cricketSettings.pointsEnabled)
        {
            cricketModeCanvas.alpha = 0.5f;
            cricketModeCanvas.interactable = false;
            cricketModeCanvas.blocksRaycasts = false;
            cricketPointsLabel.text = "Off";
        }
        else
        {
            cricketModeCanvas.alpha = 1f;
            cricketModeCanvas.interactable = true;
            cricketModeCanvas.blocksRaycasts = true;
            cricketPointsLabel.text = "On";
        }

        if (!cricketSettings.cutThroatEnabled)
        {
            cricketModeLabel.text = "Normal";
        }
        else
        {
            cricketModeLabel.text = "Cutthroat";
        }
}

    private void ApplyATCSettings()
    {
        atcSettings = appHandler.GetATCSettings();
        atcTargetLabel.text = atcSettings.targetType.ToString();
        atcOrderLabel.text = atcSettings.order.ToString();
    }

    private void ApplySetsAndLegs()
    {
        setsAndLegsMode = appHandler.GetSetsAndLegsMode();
        setsAndLegsLabel.text = setsAndLegsMode.ToDescription();
        setCount = Mathf.Max(1, appHandler.GetSetCount()); // Default 1, falls ungültig
        legCount = Mathf.Max(1, appHandler.GetLegCount()); // Default 1, falls ungültig
        setCountLabel.text = setCount.ToString();
        legCountLabel.text = legCount.ToString();
        setsScrollView.ShowEvenButtons(setsAndLegsMode != SetsAndLegs.BestOf);
        legsScrollView.ShowEvenButtons(setsAndLegsMode != SetsAndLegs.BestOf);
    }

    public void X01()
    {
        appHandler.SetLastGameMode(GameMode.X01);
        x01Panel.SetActive(true);
        cricketPanel.SetActive(false);
        atcPanel.SetActive(false);
    }

    public void Cricket()
    {
        appHandler.SetLastGameMode(GameMode.Cricket);
        x01Panel.SetActive(false);
        cricketPanel.SetActive(true);
        atcPanel.SetActive(false);
    }

    public void ATC()
    {
        appHandler.SetLastGameMode(GameMode.ATC);
        x01Panel.SetActive(false);
        cricketPanel.SetActive(false);
        atcPanel.SetActive(true);
    }


    // =========================
    // X01 BUTTONS
    // =========================
    public void X01SetPoints(int points)
    {
        x01Settings.pointTarget = points;
        x01PointsLabel.text = points.ToString();
    }

    public void X01SetCheckinTypeStraightIn() => X01SetCheckinType(CheckinType.StraightIn);
    public void X01SetCheckinTypeDouble() => X01SetCheckinType(CheckinType.DoubleIn);
    public void X01SetCheckinTypeMaster() => X01SetCheckinType(CheckinType.MasterIn);

    private void X01SetCheckinType(CheckinType type)
    {
        x01Settings.checkinType = type;
        x01CheckinLabel.text = type.ToDescription();
    }

    public void X01SetCheckoutTypeSingle() => X01SetCheckoutType(CheckoutType.Single);
    public void X01SetCheckoutTypeDouble() => X01SetCheckoutType(CheckoutType.Double);
    public void X01SetCheckoutTypeTriple() => X01SetCheckoutType(CheckoutType.Triple);

    private void X01SetCheckoutType(CheckoutType type)
    {
        x01Settings.checkoutType = type;
        x01CheckoutLabel.text = type.ToString();
    }

 

    // =========================
    // CRICKET BUTTONS
    // =========================

    public void CricketPointsOn()
    {
        cricketSettings.pointsEnabled = true;
        cricketModeCanvas.alpha = 1f;
        cricketModeCanvas.interactable = true;
        cricketModeCanvas.blocksRaycasts = true;
        cricketPointsLabel.text = "On";
    }

    public void CricketPointsOff()
    {
        cricketSettings.pointsEnabled = false;
        cricketModeCanvas.alpha = 0.5f;
        cricketModeCanvas.interactable = false;
        cricketModeCanvas.blocksRaycasts = false;
        cricketPointsLabel.text = "Off";
    }

    public void CricketModeNormal()
    {
        cricketSettings.cutThroatEnabled = false;
        cricketModeLabel.text = "Normal";
    }

    public void CricketModeCutThroat()
    {
        cricketSettings.cutThroatEnabled = true;
        cricketModeLabel.text = "Cutthroat";
    }


    // =========================
    // ATC BUTTONS
    // =========================

    public void ATCTargetSingles()
    {
        atcSettings.targetType = ATCTargetType.Singles;
        atcTargetLabel.text = "Singles";
    }
    public void ATCTargetDoubles(){
        atcSettings.targetType = ATCTargetType.Doubles;
        atcTargetLabel.text = "Doubles";
    }
    public void ATCTargetTriples()
    {
        atcSettings.targetType = ATCTargetType.Triples;
        atcTargetLabel.text = "Triples";
    }

    public void ATCOrderAscending()
    {
        atcSettings.order = ATCOrder.Ascending;
        atcOrderLabel.text = "Ascending";
    }
    // keep spelling as requested for button binding
    public void ATCOrderDecending()
    {
        atcSettings.order = ATCOrder.Descending;
        atcOrderLabel.text = "Descending";
    }
    public void ATCOrderRandom()
    {
        atcSettings.order = ATCOrder.Random;
        atcOrderLabel.text = "Random";
    }


    public void SetSetsAndLegsModeFirstTo()
    {
        setsAndLegsMode = SetsAndLegs.FirstTo;
        setsAndLegsLabel.text = setsAndLegsMode.ToDescription();
        // Bei FirstTo auch gerade Werte erlauben
        setsScrollView.ShowEvenButtons(true);
        legsScrollView.ShowEvenButtons(true);
    }

    public void SetSetsAndLegsModeBestOf()
    {
        setsAndLegsMode = SetsAndLegs.BestOf;
        setsAndLegsLabel.text = setsAndLegsMode.ToDescription();
        // Bei BestOf nur ungerade Werte erlauben
        if (setCount % 2 == 0)
            SetSetCount(setCount - 1);
        if (legCount % 2 == 0)
            SetLegCount(legCount - 1);

        setsScrollView.ShowEvenButtons(false);
        legsScrollView.ShowEvenButtons(false);
    }

    public void SetSetCount(int count)
    {
        if (count < 1 || count > 20)
        {
            return;
        }

        setCount = count;
        setCountLabel.text = count.ToString();
    }

    public void SetLegCount(int count)
    {
        if (count < 1 || count > 20)
        {
            return;
        }

        legCount = count;
        legCountLabel.text = count.ToString();
    }

    public void AddBot()
    {
        windowHandler.ShowPopup(popupAddBot);
    }


    public void OnClickAddBotConfirm()
    {
        DartBotDifficulty difficulty = DartBotDifficulty.Easy;
        if (botDifficultyGroup.GetSelectedIndex() == 1)
            difficulty = DartBotDifficulty.Medium;
        else if (botDifficultyGroup.GetSelectedIndex() == 2)            
            difficulty = DartBotDifficulty.Hard;
        else if (botDifficultyGroup.GetSelectedIndex() == 3)            
            difficulty = DartBotDifficulty.Pro;

        appHandler.AddDartBot(difficulty);
        activeList.ShowPlayers();
        reserveList.ShowPlayers();
        windowHandler.HidePopup();
    }



    public void StartGame()
    {
        if (activePlayerList != null)
        {
            activePlayerList.UpdateOrderFromUI();
        }
        
        if (appHandler.GetActivePlayers().Count < 1)
        {
            Debug.Log("Kein Spieler hinzugefügt: zeige Popup");
            windowHandler.ShowPopup(popupNoPlayersAlert);
        }

        else
        {
            EnsureEngineReferences();
            CheckForShuffeling();

            GameMode gameMode = appHandler.GetLastGameMode();

            if (gameMode == GameMode.X01)
            {
                X01GameSettings settingsCopy = (X01GameSettings)x01Settings.Clone();
                settingsCopy.setsAndLegsMode = setsAndLegsMode;
                settingsCopy.setCount = setCount;
                settingsCopy.legCount = legCount;
                X01Game game = appHandler.PrepareX01Game(settingsCopy);
                //settingsCopy.Print();
                windowHandler.GoTo(ScreenId.X01Game);
                x01GameEngine.StartGame(game);
            }

            else if (gameMode == GameMode.Cricket)
            {
                CricketGameSettings settingsCopy = (CricketGameSettings)cricketSettings.Clone();
                settingsCopy.setsAndLegsMode = setsAndLegsMode;
                settingsCopy.setCount = setCount;
                settingsCopy.legCount = legCount;
                CricketGame game = appHandler.PrepareCricketGame(settingsCopy);
                //settingsCopy.Print();
                windowHandler.GoTo(ScreenId.CricketGame);
                cricketGameEngine.StartGame(game);
            }

            else if (gameMode == GameMode.ATC)
            {
                ATCGameSettings settingsCopy = (ATCGameSettings)atcSettings.Clone();
                settingsCopy.setsAndLegsMode = setsAndLegsMode;
                settingsCopy.setCount = setCount;
                settingsCopy.legCount = legCount;
                ATCGame game = appHandler.PrepareATCGame(settingsCopy);
                //settingsCopy.Print();
                windowHandler.GoTo(ScreenId.ATCGame);
                atcGameEngine.StartGame(game);
            }
        }
    }

    private void CheckForShuffeling()
    {
        if (toggleRandomOrder.IsActive())
        {
            var activePlayers = appHandler.GetActivePlayers();
            var rnd = new System.Random();
            var shuffled = activePlayers.OrderBy(p => rnd.Next()).ToList();
            appHandler.SetPlayers(shuffled.Select(p => p.GetID()).ToList(), appHandler.GetReservePlayers().Select(p => p.GetID()).ToList());
        }
    }

    private void EnsureEngineReferences()
    {
        if (x01GameEngine == null)
            x01GameEngine = FindEngineInScene<X01GameEngine>();
        if (cricketGameEngine == null)
            cricketGameEngine = FindEngineInScene<CricketGameEngine>();
        if (atcGameEngine == null)
            atcGameEngine = FindEngineInScene<ATCGameEngine>();
    }

    private T FindEngineInScene<T>() where T : MonoBehaviour
    {
        var engine = FindFirstObjectByType<T>();
        if (engine != null)
            return engine;

        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(e => e != null && e.gameObject.scene.IsValid());
    }

    public void HidePopup()
    {
        windowHandler.HidePopup();
    }


    // =========================
    // PLAYER TOGGLE
    // =========================

    /// <summary>
    /// Wechselt einen Spieler zwischen Active und Reserve.
    /// </summary>
    public void TogglePlayerBetweenLists(Guid playerID)
    {
        // Hole aktuelle Listen
        List<Guid> activeIDs = new List<Guid>(appHandler.GetActivePlayers().Select(p => p.GetID()));
        List<Guid> reserveIDs = new List<Guid>(appHandler.GetReservePlayers().Select(p => p.GetID()));

        // Prüfe, in welcher Liste der Spieler ist
        if (activeIDs.Contains(playerID))
        {
            // Spieler ist in Active → in Reserve verschieben
            activeIDs.Remove(playerID);
            reserveIDs.Add(playerID);
        }
        else if (reserveIDs.Contains(playerID))
        {
            // Spieler ist in Reserve → in Active verschieben
            reserveIDs.Remove(playerID);
            activeIDs.Add(playerID);
        }
        else
        {
            Debug.LogWarning($"Spieler {playerID} nicht gefunden!");
            return;
        }

        // Speichere die neue Anordnung
        appHandler.SetPlayers(activeIDs, reserveIDs);

        // Aktualisiere beide Listen
        activeList.ShowPlayers();
        reserveList.ShowPlayers();
    }
}
