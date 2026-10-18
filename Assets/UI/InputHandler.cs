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

    [Header("Penalty Buttons")]
    public CanvasGroup wallCanvasGroup;
    public CanvasGroup ceilingCanvasGroup;

    [Header("Custom Sektoren (Tab 1 „Bereiche“)")]
    [Tooltip("Eltern-Container für Sektor-Buttons. Leer = Eltern des Wall-Buttons.")]
    public Transform sectorButtonParent;
    [Tooltip("Button-Vorlage (Button + TMP_Text + optional CanvasGroup). Leer = Wall-Button wird geklont.")]
    public GameObject sectorButtonPrefab;

    protected bool inputEnabled;
    protected bool undoEnabled;

    private readonly List<GameObject> sectorButtons = new List<GameObject>();


    protected virtual void OnEnable()
    {
        CustomizationStore.Changed += HandleCustomizationChanged;
        UpdatePenaltyUI();
        RefreshSectorButtons();
    }

    protected virtual void OnDisable()
    {
        CustomizationStore.Changed -= HandleCustomizationChanged;
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

        gameEngine.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Custom, false, -1, sectorId));
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
        VibrationManager.Instance.Vibrate();
    }


    protected virtual void UpdatePenaltyUI()
    {
        // Wall/Ceiling-Buttons dimmen anhand der Custom-Penalties
        // (custom_wall/custom_ceiling, enabled-Stand aus dem Store).
        bool wallEnabled = HasEnabledInstantPenalty(HitType.Wall);
        bool ceilingEnabled = HasEnabledInstantPenalty(HitType.Ceiling);

        SetCanvasGroup(wallCanvasGroup, wallEnabled);
        SetCanvasGroup(ceilingCanvasGroup, ceilingEnabled);
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

        Transform parent = sectorButtonParent;
        if (parent == null && wallCanvasGroup != null)
            parent = wallCanvasGroup.transform.parent;

        GameObject template = sectorButtonPrefab;
        if (template == null && wallCanvasGroup != null)
            template = wallCanvasGroup.gameObject;

        if (parent == null || template == null)
            return;

        foreach (var def in CustomizationStore.GetHitSectors())
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            // Wand/Decke haben statische Buttons (kein Duplikat).
            if (CustomHitDef.IsBuiltInSector(def.id))
                continue;

            string sectorId = def.id;

            var go = Instantiate(template, parent);
            go.name = $"Sector_{def.name}";

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = def.name;

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnCustomSector(sectorId));
            }

            var cg = go.GetComponent<CanvasGroup>();
            SetCanvasGroup(cg, def.enabled);

            sectorButtons.Add(go);
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