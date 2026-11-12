using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public abstract class InputHandler : MonoBehaviour
{
    [Header("Engine")]
    public GameEngine gameEngine;

    [Header("Bot")]
    public GameObject botPausePanel;
    public GameObject botPausePanelPause;
    public GameObject botPausePanelPlay;
    [SerializeField] private Sprite botDartSprite;
    [SerializeField] private Sprite botDartboardSprite;

    [Header("Bot Icons")]
    public Transform botIconRow;
    public Image botIcon;

    private BotTurnPanel botTurnPanel;

    // [Header("Penalty Buttons")]
    // public CanvasGroup wallCanvasGroup;
    // public CanvasGroup ceilingCanvasGroup;

    // [Header("Custom Sectors (Tab 1 “Areas”)")]
    // [Tooltip("Parent container for sector buttons. Empty = parent of the Wall button.")]
    // public Transform sectorButtonParent;
    // [Tooltip("Button template (Button + TMP_Text + optional CanvasGroup). Empty = clone the Wall button.")]
    // public GameObject sectorButtonPrefab;

    // (Sector dropdown removed; sector selection runs through the sector panel.)

    [Header("Quick Select")]
    public Button quickSelectButton;
    public TMP_Text quickSelectLabel;

    [Header("Sektor-Panel (Overlay)")]
    public GameObject sectorPanel;
    public Transform sectorButtonContainer;
    public GameObject sectorButtonPrefab;

    protected bool inputEnabled;
    protected bool undoEnabled;

    private readonly List<GameObject> sectorButtons = new List<GameObject>();

    private bool isSectorPanelOpen;
    private float sectorPanelBackgroundAlpha = 0.78431374f;


    protected virtual void OnEnable()
    {
        CustomizationStore.Changed += HandleCustomizationChanged;
        UpdatePenaltyUI();
        RefreshSectorButtons();
        RefreshQuickSelect();

        if (sectorPanel != null)
        {
            var bg = sectorPanel.GetComponent<UnityEngine.UI.Image>();
            if (bg != null && bg.color.a > 0.01f)
                sectorPanelBackgroundAlpha = bg.color.a;
            sectorPanel.SetActive(true);
        }

        CloseSectorPanel();
    }

    protected virtual void OnDisable()
    {
        CustomizationStore.Changed -= HandleCustomizationChanged;
        CloseSectorPanel();
    }

    public void RefreshSectorUiFromGame()
    {
        RefreshSectorButtons();
        RefreshQuickSelect();
    }

    private List<CustomHitDef> GetGameplayHitSectors()
    {
        var snapshot = gameEngine != null ? gameEngine.GetHitSectorSnapshot() : null;
        return snapshot ?? CustomizationStore.GetHitSectors();
    }

    private void HandleCustomizationChanged(CustomizationChangeKind kind)
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Input UI only knows penalty/sector buttons; pure AudioEvent
        // changes do not affect them.
        if (kind == CustomizationChangeKind.AudioEvents)
            return;

        UpdatePenaltyUI();
        RefreshSectorButtons();
        RefreshQuickSelect();
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


    public virtual void OnCustomSector(string sectorId)
    {
        if (!inputEnabled || string.IsNullOrEmpty(sectorId))
            return;

        var def = GetGameplayHitSectors()
            .Find(x => x != null && x.id == sectorId);

        int value = def != null ? def.value : 0;
        DartMultiplier mult = def != null ? def.multiplier : DartMultiplier.Single;

        gameEngine.AddThrow(new Throw(mult, value, HitType.Custom, false, -1, sectorId));
        TriggerHaptic();
        CloseSectorPanel();
    }

    /// <summary>
    /// Click on a sector button in the panel: throws the appropriate sector
    /// (Wall/Ceiling/Custom) and closes the panel.
    /// </summary>
    public virtual void OnSectorButtonClicked(string sectorId)
    {
        if (!inputEnabled || string.IsNullOrEmpty(sectorId))
            return;

        AddSectorThrow(sectorId);
        RememberSelectedSector(sectorId);
        RefreshQuickSelect();
        CloseSectorPanel();
    }


    public void OnQuickSelectSector()
    {
        if (!inputEnabled)
            return;

        AddSectorThrow(ResolveQuickSelectSectorId(out _));
    }


    private void AddSectorThrow(string sectorId)
    {
        switch (CustomHitDef.HitTypeForSector(sectorId))
        {
            case HitType.Wall:
                OnWall();
                break;
            case HitType.Ceiling:
                OnCeiling();
                break;
            default:
                OnCustomSector(sectorId);
                break;
        }
    }

    private static void RememberSelectedSector(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return;

        AppSettingsManager.Instance?.SetLastSelectedSectorId(sectorId);
    }

    private string ResolveQuickSelectSectorId(out string displayName)
    {
        var sectors = GetGameplayHitSectors();
        string storedId = AppSettingsManager.Instance?.GetLastSelectedSectorId();

        if (!string.IsNullOrEmpty(storedId))
        {
            var stored = sectors.Find(x => x != null && x.enabled && x.id == storedId);

            if (stored != null)
            {
                displayName = string.IsNullOrWhiteSpace(stored.name) ? stored.id : stored.name;
                return stored.id;
            }
        }

        // Fallback: Wall (even if no sector has ever been chosen).
        var wall = sectors.Find(x => x != null && x.id == CustomHitDef.WallId);

        displayName = wall != null && !string.IsNullOrWhiteSpace(wall.name)
            ? wall.name
            : "Wall";

        return CustomHitDef.WallId;
    }


    public void ToggleSectorPanel()
    {
        if (isSectorPanelOpen)
            CloseSectorPanel();
        else
            OpenSectorPanel();
    }

    public void OpenSectorPanel()
    {
        if (isSectorPanelOpen)
            return;

        if (sectorPanel == null)
            return;

        // Panel itself must remain permanently active.
        if (sectorPanel != null)
            sectorPanel.SetActive(true);

        isSectorPanelOpen = true;

        // Panel remains active (Alpha 1 on Image); only show background image + container/backdrop.
        UnityEngine.UI.Image bg = GetSectorPanelBackgroundImage();
        if (bg != null)
        {
            bg.raycastTarget = true;
        }

        SetSectorButtonsVisible(true);


        StopAllCoroutines();
        StartCoroutine(FadeSectorPanel(true));
    }

    public void CloseSectorPanel()
    {
        // Panel itself remains active.
        if (sectorPanel != null)
            sectorPanel.SetActive(true);

        if (!isSectorPanelOpen)
        {
            // Panel remains active; only hide background, let raycasts pass through.
            UnityEngine.UI.Image bg = GetSectorPanelBackgroundImage();
            if (bg != null)
            {
                bg.raycastTarget = false;
            }
            SetSectorPanelBackgroundAlpha(0f);
            SetSectorButtonsVisible(false);

            return;
        }

        isSectorPanelOpen = false;

        UnityEngine.UI.Image bg2 = GetSectorPanelBackgroundImage();
        if (bg2 != null)
        {
            bg2.raycastTarget = false;
        }

        if (!gameObject.activeInHierarchy)
        {
            SetSectorPanelBackgroundAlpha(0f);
            SetSectorButtonsVisible(false);

            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeSectorPanel(false));
    }

    private System.Collections.IEnumerator FadeSectorPanel(bool open)
    {
        UnityEngine.UI.Image bg = GetSectorPanelBackgroundImage();
        float duration = 0.15f;
        float t = 0f;
        float start = bg != null ? bg.color.a : (open ? 0f : 1f);
        float end = open ? sectorPanelBackgroundAlpha : 0f; // original alpha

        if (open)
        {
            if (sectorButtonContainer != null)
                SetSectorButtonsVisible(true);
        }

        while (t < duration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            SetSectorPanelBackgroundAlpha(Mathf.Lerp(start, end, p));
            t += Time.deltaTime;
            yield return null;
        }

        SetSectorPanelBackgroundAlpha(end);

        if (!open)
        {
            SetSectorButtonsVisible(false);

        }
    }

    private UnityEngine.UI.Image GetSectorPanelBackgroundImage()
    {
        if (sectorPanel == null)
            return null;
        return sectorPanel.GetComponent<UnityEngine.UI.Image>();
    }

    private void SetSectorButtonsVisible(bool visible)
    {
        // Never deactivate the sectorPanel itself (would take the toggle button with you).
        if (sectorButtonContainer == null || (sectorPanel != null && sectorButtonContainer.gameObject == sectorPanel))
        {
            foreach (var btn in sectorButtons)
            {
                if (btn != null)
                    btn.SetActive(visible);
            }
            return;
        }

        sectorButtonContainer.gameObject.SetActive(visible);
    }
    private void SetSectorPanelBackgroundAlpha(float a)
    {
        UnityEngine.UI.Image bg = GetSectorPanelBackgroundImage();
        if (bg == null)
            return;
        Color c = bg.color;
        c.a = a;
        bg.color = c;
    }

    public bool TryHandleBack()
    {
        if (!isSectorPanelOpen)
            return false;

        CloseSectorPanel();
        return true;
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

    public void ShowBotPlayingState(DartBot bot)
    {
        botPausePanel.SetActive(true);
        EnsureBotTurnPanel();

        if (botTurnPanel != null)
            botTurnPanel.ShowBot(bot);

        botPausePanelPlay.SetActive(true);
        botPausePanelPause.SetActive(false);
    }

    public void ShowBotTarget(Throw throwData)
    {
        EnsureBotTurnPanel();

        if (botTurnPanel != null)
            botTurnPanel.ShowTarget(throwData);
    }

    public IEnumerator PlayBotThrow(Throw throwData, Action onImpact = null)
    {
        EnsureBotTurnPanel();

        if (botTurnPanel == null)
        {
            yield return new WaitForSeconds(0.8f + 0.45f + 0.12f);
            onImpact?.Invoke();
            yield return new WaitForSeconds(1f);
            yield break;
        }

        yield return botTurnPanel.PlayThrow(throwData, onImpact);
    }

    public bool IsBotHitHoldActive => botTurnPanel != null && botTurnPanel.IsHitHoldActive;

    public void CancelBotHitHold()
    {
        botTurnPanel?.CancelHitHold();
    }

    public void ShowBotPausedState()
    {
        botPausePanel.SetActive(true);
        EnsureBotTurnPanel();

        if (botTurnPanel != null)
            botTurnPanel.ResetVisuals();

        botPausePanelPause.SetActive(true);
        botPausePanelPlay.SetActive(false);
    }

    public void HideBotState()
    {
        if (botTurnPanel != null)
            botTurnPanel.ResetVisuals();

        botPausePanel.SetActive(false);
    }

    private void EnsureBotTurnPanel()
    {
        if (botTurnPanel != null || botPausePanel == null)
            return;

        botTurnPanel = botPausePanel.GetComponent<BotTurnPanel>();
        if (botTurnPanel == null)
            botTurnPanel = botPausePanel.AddComponent<BotTurnPanel>();

        botTurnPanel.Initialize(botPausePanel, botPausePanelPlay, botPausePanelPause, botDartSprite, botDartboardSprite, botIconRow, botIcon);
    }


    protected void TriggerHaptic()
    {
        VibrationManager.Instance.Vibrate();
    }


    protected virtual void UpdatePenaltyUI()
    {
        // Wall/Ceiling buttons dim based on custom penalties
        // (custom_wall / custom_ceiling, enabled-Stand from the store).
        bool wallEnabled = HasEnabledInstantPenalty(HitType.Wall);
        bool ceilingEnabled = HasEnabledInstantPenalty(HitType.Ceiling);
    }

    private static bool HasEnabledInstantPenalty(HitType hitType)
    {
        try
        {
            foreach (var def in CustomizationStore.GetPenalties())
            {
                if (def == null || !def.enabled || def.conditions == null || def.conditions.Count == 0)
                    continue;
                if (def.conditions.Any(c => c.kind == PenaltyConditionKind.HitTypeMiss
                    && c.hitType == hitType))
                    return true;
            }
        }
        catch (System.Exception)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Update the Quick-Select button: last chosen, still
    /// Available sector or fallback on Wall.
    /// </summary>
    protected virtual void RefreshQuickSelect()
    {
        if (quickSelectButton == null && quickSelectLabel == null)
            return;

        if (quickSelectLabel == null && quickSelectButton != null)
            quickSelectLabel = quickSelectButton.GetComponentInChildren<TMP_Text>(true);

        ResolveQuickSelectSectorId(out string displayName);

        if (quickSelectLabel != null)
            quickSelectLabel.text = displayName;
    }

    /// <summary>
    /// Rebuild the buttons for free custom sectors (have wall/ceiling)
    /// Static buttons and are skipped here.
    /// Sectors are dimmed like deactivated Wall/Ceiling buttons.
    /// Template: sectorButtonPrefab or cloned wall button (same)
    /// styling without prefab overhead.
    /// Note: The "Sectors" (UI conversion) button replaces the
    /// static buttons; OnCustomSector (sectorId) remains the API for this.
    /// </summary>
    protected virtual void RefreshSectorButtons()
    {
        foreach (var old in sectorButtons)
        {
            if (old != null)
                Destroy(old);
        }
        sectorButtons.Clear();

        Transform container = sectorButtonContainer != null ? sectorButtonContainer : (sectorPanel != null ? sectorPanel.transform : null);
        if (container == null)
            return;

        foreach (var def in GetGameplayHitSectors())
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            // Wall/ceiling no longer have static buttons — all sectors as panel buttons.
            if (!def.enabled)
                continue;

            GameObject template = sectorButtonPrefab;
            if (template == null)
                continue;

            GameObject btnGo = Instantiate(template, container);
            sectorButtons.Add(btnGo);

            string sectorId = def.id;
            string displayName = string.IsNullOrWhiteSpace(def.name) ? def.id : def.name;

            Button btn = btnGo.GetComponent<Button>();
            if (btn == null)
                btn = btnGo.GetComponentInChildren<Button>();

            TMP_Text label = btnGo.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = displayName;

            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSectorButtonClicked(sectorId));
            }
        }
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