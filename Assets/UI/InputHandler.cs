using System.Collections.Generic;
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

    // [Header("Penalty Buttons")]
    // public CanvasGroup wallCanvasGroup;
    // public CanvasGroup ceilingCanvasGroup;

    // [Header("Custom Sektoren (Tab 1 „Bereiche“)")]
    // [Tooltip("Eltern-Container für Sektor-Buttons. Leer = Eltern des Wall-Buttons.")]
    // public Transform sectorButtonParent;
    // [Tooltip("Button-Vorlage (Button + TMP_Text + optional CanvasGroup). Leer = Wall-Button wird geklont.")]
    // public GameObject sectorButtonPrefab;

    // (Sektor-Dropdown entfernt; Sektor-Auswahl läuft über das Sector-Panel.)

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

    private void HandleCustomizationChanged(CustomizationChangeKind kind)
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Input-UI kennt nur Penalty-/Sektor-Buttons; reine AudioEvent-
        // Änderungen betreffen sie nicht.
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

        var def = CustomizationStore.GetHitSectors()
            .Find(x => x != null && x.id == sectorId);

        int value = def != null ? def.value : 0;
        DartMultiplier mult = def != null ? def.multiplier : DartMultiplier.Single;

        gameEngine.AddThrow(new Throw(mult, value, HitType.Custom, false, -1, sectorId));
        TriggerHaptic();
        CloseSectorPanel();
    }

    /// <summary>
    /// Klick auf einen Sektor-Button im Panel: wirft den passenden Sektor
    /// (Wall/Ceiling/Custom) und schließt das Panel.
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
        string storedId = AppSettingsManager.Instance?.GetLastSelectedSectorId();

        if (!string.IsNullOrEmpty(storedId))
        {
            var stored = CustomizationStore.GetHitSectors()
                .Find(x => x != null && x.enabled && x.id == storedId);

            if (stored != null)
            {
                displayName = string.IsNullOrWhiteSpace(stored.name) ? stored.id : stored.name;
                return stored.id;
            }
        }

        // Fallback: Wall (auch wenn noch nie ein Sektor gewählt wurde).
        var wall = CustomizationStore.GetHitSectors()
            .Find(x => x != null && x.id == CustomHitDef.WallId);

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

        // Panel selbst muss dauerhaft aktiv bleiben.
        if (sectorPanel != null)
            sectorPanel.SetActive(true);

        isSectorPanelOpen = true;

        // Panel bleibt aktiv (Alpha 1 auf Image); nur Hintergrund-Image + Container/Backdrop einblenden.
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
        // Panel selbst bleibt aktiv.
        if (sectorPanel != null)
            sectorPanel.SetActive(true);

        if (!isSectorPanelOpen)
        {
            // Panel bleibt aktiv; nur Hintergrund ausblenden, Raycasts durchlassen.
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
        // Nie das sectorPanel selbst deaktivieren (würde den Toggle-Button mitnehmen).
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

    protected virtual void Update()
    {
        if (!isSectorPanelOpen)
            return;

        if (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseSectorPanel();
        }
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
        VibrationManager.Instance.Vibrate();
    }


    protected virtual void UpdatePenaltyUI()
    {
        // Wall/Ceiling-Buttons dimmen anhand der Custom-Penalties
        // (custom_wall/custom_ceiling, enabled-Stand aus dem Store).
        bool wallEnabled = HasEnabledInstantPenalty(HitType.Wall);
        bool ceilingEnabled = HasEnabledInstantPenalty(HitType.Ceiling);
    }

    private static bool HasEnabledInstantPenalty(HitType hitType)
    {
        try
        {
            foreach (var def in CustomizationStore.GetPenalties())
            {
                if (def == null || !def.enabled || def.condition == null)
                    continue;
                if (def.condition.kind == PenaltyConditionKind.HitTypeMiss
                    && def.condition.hitType == hitType)
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
    /// Aktualisiert den Quick-Select-Button: zuletzt gewählter, noch
    /// verfügbarer Sektor oder Fallback auf Wall.
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
    /// Baut die Buttons für freie Custom-Sektoren neu (Wand/Decke haben
    /// statische Buttons und werden hier übersprungen). Deaktivierte
    /// Sektoren werden gedimmt wie deaktivierte Wall/Ceiling-Buttons.
    /// Vorlage: sectorButtonPrefab oder geklonter Wall-Button (gleiches
    /// Styling ohne Prefab-Aufwand).
    /// Hinweis: Der Sammelbutton „Sectors“ (UI-Umbau) ersetzt die
    /// statischen Buttons; OnCustomSector(sectorId) bleibt die API dafür.
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

        foreach (var def in CustomizationStore.GetHitSectors())
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            // Wand/Decke haben keine statischen Buttons mehr — alle Sektoren als Panel-Buttons.
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