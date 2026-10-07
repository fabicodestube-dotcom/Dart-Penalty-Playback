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
        appHandler.ApplyPlayerSetupStartOrderFromFlags();

        ApplyX01Settings();
        ApplyCricketSettings();
        ApplyATCSettings();
        ApplySetsAndLegs();

        var currentMode = ResolveSelectedGameMode();
        if (currentMode == GameMode.X01)
        {
            topGroup.Init(0);
            x01Panel.SetActive(true);
            cricketPanel.SetActive(false);
            atcPanel.SetActive(false);
        }
        else if (currentMode == GameMode.Cricket)
        {
            topGroup.Init(1);
            x01Panel.SetActive(false);
            cricketPanel.SetActive(true);
            atcPanel.SetActive(false);
        }

        else if (currentMode == GameMode.ATC)
        {
            topGroup.Init(2);
            x01Panel.SetActive(false);
            cricketPanel.SetActive(false);
            atcPanel.SetActive(true);
        }
        else
        {
            topGroup.Init(0);
            x01Panel.SetActive(true);
            cricketPanel.SetActive(false);
            atcPanel.SetActive(false);
            appHandler.SetLastGameMode(GameMode.X01);
        }
    }

    private GameMode ResolveSelectedGameMode()
    {
        var storedMode = appHandler.GetLastGameMode();
        if (storedMode != GameMode.All)
            return storedMode;

        if (x01Panel != null && x01Panel.activeSelf)
            return GameMode.X01;

        if (cricketPanel != null && cricketPanel.activeSelf)
            return GameMode.Cricket;

        if (atcPanel != null && atcPanel.activeSelf)
            return GameMode.ATC;

        return GameMode.X01;
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
        appHandler.SetX01Settings(x01Settings);
    }

    public void X01SetCheckinTypeStraightIn() => X01SetCheckinType(CheckinType.StraightIn);
    public void X01SetCheckinTypeDouble() => X01SetCheckinType(CheckinType.DoubleIn);
    public void X01SetCheckinTypeMaster() => X01SetCheckinType(CheckinType.MasterIn);

    private void X01SetCheckinType(CheckinType type)
    {
        x01Settings.checkinType = type;
        x01CheckinLabel.text = type.ToDescription();
        appHandler.SetX01Settings(x01Settings);
    }

    public void X01SetCheckoutTypeSingle() => X01SetCheckoutType(CheckoutType.Single);
    public void X01SetCheckoutTypeDouble() => X01SetCheckoutType(CheckoutType.Double);
    public void X01SetCheckoutTypeTriple() => X01SetCheckoutType(CheckoutType.Triple);

    private void X01SetCheckoutType(CheckoutType type)
    {
        x01Settings.checkoutType = type;
        x01CheckoutLabel.text = type.ToString();
        appHandler.SetX01Settings(x01Settings);
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
        appHandler.SetCricketSettings(cricketSettings);
    }

    public void CricketPointsOff()
    {
        cricketSettings.pointsEnabled = false;
        cricketModeCanvas.alpha = 0.5f;
        cricketModeCanvas.interactable = false;
        cricketModeCanvas.blocksRaycasts = false;
        cricketPointsLabel.text = "Off";
        appHandler.SetCricketSettings(cricketSettings);
    }

    public void CricketModeNormal()
    {
        cricketSettings.cutThroatEnabled = false;
        cricketModeLabel.text = "Normal";
        appHandler.SetCricketSettings(cricketSettings);
    }

    public void CricketModeCutThroat()
    {
        cricketSettings.cutThroatEnabled = true;
        cricketModeLabel.text = "Cutthroat";
        appHandler.SetCricketSettings(cricketSettings);
    }


    // =========================
    // ATC BUTTONS
    // =========================

    public void ATCTargetSingles()
    {
        atcSettings.targetType = ATCTargetType.Singles;
        atcTargetLabel.text = "Singles";
        appHandler.SetATCSettings(atcSettings);
    }
    public void ATCTargetDoubles(){
        atcSettings.targetType = ATCTargetType.Doubles;
        atcTargetLabel.text = "Doubles";
        appHandler.SetATCSettings(atcSettings);
    }
    public void ATCTargetTriples()
    {
        atcSettings.targetType = ATCTargetType.Triples;
        atcTargetLabel.text = "Triples";
        appHandler.SetATCSettings(atcSettings);
    }

    public void ATCOrderAscending()
    {
        atcSettings.order = ATCOrder.Ascending;
        atcOrderLabel.text = "Ascending";
        appHandler.SetATCSettings(atcSettings);
    }
    // keep spelling as requested for button binding
    public void ATCOrderDecending()
    {
        atcSettings.order = ATCOrder.Descending;
        atcOrderLabel.text = "Descending";
        appHandler.SetATCSettings(atcSettings);
    }
    public void ATCOrderRight()
    {
        atcSettings.order = ATCOrder.Right;
        atcOrderLabel.text = "Right";
        appHandler.SetATCSettings(atcSettings);
    }

    public void ATCOrderLeft()
    {
        atcSettings.order = ATCOrder.Left;
        atcOrderLabel.text = "Left";
        appHandler.SetATCSettings(atcSettings);
    }

    public void ATCOrderRandom()
    {
        atcSettings.order = ATCOrder.Random;
        atcOrderLabel.text = "Random";
        appHandler.SetATCSettings(atcSettings);
    }


    public void SetSetsAndLegsModeFirstTo()
    {
        setsAndLegsMode = SetsAndLegs.FirstTo;
        setsAndLegsLabel.text = setsAndLegsMode.ToDescription();
        // Bei FirstTo auch gerade Werte erlauben
        setsScrollView.ShowEvenButtons(true);
        legsScrollView.ShowEvenButtons(true);
        appHandler.SetSetupState(appHandler.GetLastGameMode(), setsAndLegsMode, setCount, legCount);
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
        appHandler.SetSetupState(appHandler.GetLastGameMode(), setsAndLegsMode, setCount, legCount);
    }

    public void SetSetCount(int count)
    {
        if (count < 1 || count > 20)
        {
            return;
        }

        setCount = count;
        setCountLabel.text = count.ToString();
        appHandler.SetSetupState(appHandler.GetLastGameMode(), setsAndLegsMode, setCount, legCount);
    }

    public void SetLegCount(int count)
    {
        if (count < 1 || count > 20)
        {
            return;
        }

        legCount = count;
        legCountLabel.text = count.ToString();
        appHandler.SetSetupState(appHandler.GetLastGameMode(), setsAndLegsMode, setCount, legCount);
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

            GameMode gameMode = ResolveSelectedGameMode();
            appHandler.SetLastGameMode(gameMode);

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
            else
            {
                appHandler.SetLastGameMode(GameMode.X01);
                X01GameSettings settingsCopy = (X01GameSettings)x01Settings.Clone();
                settingsCopy.setsAndLegsMode = setsAndLegsMode;
                settingsCopy.setCount = setCount;
                settingsCopy.legCount = legCount;
                X01Game game = appHandler.PrepareX01Game(settingsCopy);
                windowHandler.GoTo(ScreenId.X01Game);
                x01GameEngine.StartGame(game);
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
