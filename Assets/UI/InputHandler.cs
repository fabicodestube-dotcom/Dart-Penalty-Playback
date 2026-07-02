using UnityEngine;


public abstract class InputHandler : MonoBehaviour
{
    [Header("Engine")]
    public GameEngine gameEngine;

    [Header("Bot")]
    public GameObject botPausePanel;
    public GameObject botPausePanelPause;
    public GameObject botPausePanelPlay;

    [Header("Penalty Buttons")]
    public CanvasGroup wallCanvasGroup;
    public CanvasGroup ceilingCanvasGroup;

    protected bool inputEnabled;
    protected bool undoEnabled;


    protected virtual void OnEnable()
    {
        UpdatePenaltyUI();
    }


    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void SetUndoEnabled(bool enabled)
    {
        undoEnabled = enabled;
    }


    public virtual void OnWall()
    {
        if (!inputEnabled)
            return;

        gameEngine.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        TriggerHaptic();
    }


    public virtual void OnCeiling()
    {
        if (!inputEnabled)
            return;

        gameEngine.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Ceiling));
        TriggerHaptic();
    }


    public virtual void Undo()
    {
        if (!inputEnabled && !undoEnabled)
            return;
        gameEngine.Undo();
        TriggerHaptic();
    }

    public void PauseBot()
    {
        gameEngine.PauseBotTurn();
    }

    public void ResumeBot()
    {
        gameEngine.ResumeBotTurn();
    }

    public virtual void BotUndo()
    {
        gameEngine.Undo();
        TriggerHaptic();
    }

    public void ShowBotPlayingState()
    {
        botPausePanel.SetActive(true);
        botPausePanelPlay.SetActive(true);
        botPausePanelPause.SetActive(false);
    }

    public void ShowBotPausedState()
    {
        botPausePanel.SetActive(true);
        botPausePanelPause.SetActive(true);
        botPausePanelPlay.SetActive(false);
    }

    public void HideBotState()
    {
        botPausePanel.SetActive(false);
    }


    protected void TriggerHaptic()
    {
        var vibration = AppSettingsManager.Instance.Settings.Vibration;

        if (!vibration.Enabled)
            return;

        switch (vibration.Strength)
        {
            case VibrationStrength.Weak:
                VibrationManager.Instance.Vibrate(1);
                break;

            case VibrationStrength.Medium:
                VibrationManager.Instance.Vibrate(2);
                break;

            case VibrationStrength.Strong:
                VibrationManager.Instance.Vibrate(3);
                break;
        }
    }


    protected virtual void UpdatePenaltyUI()
    {
        bool wallEnabled =
            AppSettingsManager.Instance.Settings.Penalties
                .IsEnabled(PenaltyType.Wall);

        bool ceilingEnabled =
            AppSettingsManager.Instance.Settings.Penalties
                .IsEnabled(PenaltyType.Ceiling);

        SetCanvasGroup(wallCanvasGroup, wallEnabled);
        SetCanvasGroup(ceilingCanvasGroup, ceilingEnabled);
    }


    protected void SetCanvasGroup(CanvasGroup cg, bool visible)
    {
        if (cg == null)
            return;

        cg.alpha = visible ? 1f : 0.4f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }
}