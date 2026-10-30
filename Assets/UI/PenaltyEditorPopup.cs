using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Editor-Popup für Penalties (Tab 2): 2.1 Turn-Pattern, 2.2 exakter Score,
/// 2.3 Leg-/Set-/Game-Event. Ein Typ-Dropdown schaltet zwischen drei Bodies
/// (patternBody/scoreBody/gameEventBody) um, die übrigen werden deaktiviert.
/// Die Umschaltung braucht KEINE Inspector-Verdrahtung: Awake registriert
/// genau einen Listener (OnValueChanged -> UpdateConditionGroups).
/// Ist ein UIScreen mit id Popup und wird über den WindowHandler
/// (ShowPopup/HidePopup) angezeigt, analog zu den übrigen Popups.
/// Legacy HitTypeMiss/RepdigitScore werden gemappt geladen (Upgrade bei Save).
/// Buttons: Save → OnClickSave(), Cancel → OnClickCancel(),
/// Toggle → ToggleEnabled(), Reihenfolge → ToggleMatchOrder().
/// </summary>
public class PenaltyEditorPopup : UIScreen, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;
    public CustomizationPenaltiesPage penaltiesPage;

    [Header("Bestätigung")]
    public GameObject mainPanel;
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    [Header("Basis")]
    public TMP_InputField nameInput;
    public TMP_InputField costInput;
    public CustomToggle enabledToggle;

    [Header("Kurzname (optional, Spalten)")]
    [Tooltip("In der Szene verdrahten (sonst bleibt shortName leer und die Anzeige kürzt den Namen).")]
    public TMP_InputField shortNameInput;

    [Header("Typ-Umschalter")]
    public TMP_Dropdown conditionTypeDropdown;

    [Header("Bodies (Umschalter Sichtbarkeit)")]
    public GameObject patternBody;
    public GameObject scoreBody;
    public GameObject gameEventBody;

    [Header("Body-Inhalte")]
    public TurnPatternEditor patternEditor;
    public TMP_InputField scoreInput;
    public TMP_Dropdown eventKindDropdown;

    [Header("Sofort-Typ (HitTypeMiss, pro Wurf)")]
    [Tooltip("Dropdown mit Wall/Ceiling (Code füllt Optionen). Szene: neues Dropdown + Gruppen-Objekt anlegen und hier verdrahten.")]
    public TMP_Dropdown instantHitTypeDropdown;
    public GameObject instantHitTypeGroup;

    private string editingId;
    private bool editingBuiltIn;
    private string pendingDeleteId;
    private string editingName;

    /// <summary>
    /// True, wenn die geladene Def Rest-Semantik hat (RemainingExactScore):
    /// Score-Body bleibt, Save schreibt dieselbe Art zurück (kein stiller
    /// Wechsel zu ExactScore beim Editieren migrierter Seeds).
    /// </summary>
    private bool editingRemainingScore;

    // Index-Reihenfolge = Dropdown-Reihenfolge (Code füllt die Optionen).
    /// <summary>Sektor-IDs in Dropdown-Reihenfolge (Wall/Ceiling zuerst).</summary>
    private readonly List<string> instantSectorIds = new List<string>();

    private void OnValidate()
    {
        id = ScreenId.Popup;

        if (rect == null)
            rect = GetComponent<RectTransform>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    protected override void Awake()
    {
        base.Awake();

        id = ScreenId.Popup;

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (penaltiesPage == null)
            penaltiesPage = FindAnyObjectByType<CustomizationPenaltiesPage>();

        if (conditionTypeDropdown != null)
        {
            conditionTypeDropdown.ClearOptions();
            conditionTypeDropdown.AddOptions(new List<string>
            {
                LocalizationText.Get("Turn_pattern", "Turn pattern"),
                LocalizationText.Get("Exact_score", "Exact score"),
                LocalizationText.Get("Game_events", "Game events")
            });
            // EINZIGER Umschalt-Listener (kein Inspector-Event nötig).
            conditionTypeDropdown.onValueChanged.AddListener(_ => UpdateConditionGroups());
        }

        if (eventKindDropdown != null)
        {
            eventKindDropdown.ClearOptions();
            eventKindDropdown.AddOptions(new List<string>
            {
                LocalizationText.Get("Leg_lost", "Leg lost"),
                LocalizationText.Get("Set_lost", "Set lost"),
                LocalizationText.Get("Game_lost", "Game lost")
            });
        }

        RefreshInstantSectorOptions();
    }

    public void OnShow()
    {
        SetConfirmView(false);
        pendingDeleteId = null;
        editingName = null;
    }

    public void OnHide()
    {
    }

    public void ShowNew(CustomizationPenaltiesPage page)
    {
        ShowNew(page, PenaltyConditionKind.TurnPattern);
    }

    public void ShowNew(CustomizationPenaltiesPage page, PenaltyConditionKind kind)
    {
        penaltiesPage = page != null ? page : penaltiesPage;
        editingId = System.Guid.NewGuid().ToString("N");
        editingBuiltIn = false;
        editingRemainingScore = kind == PenaltyConditionKind.RemainingExactScore;

        LoadDef("New penalty", DefaultCost(kind), true, FreshCondition(kind));
        ShowViaWindowHandler();
    }

    public void ShowEdit(CustomizationPenaltiesPage page, string id)
    {
        penaltiesPage = page != null ? page : penaltiesPage;

        var def = CustomizationStore.GetPenalties().Find(x => x != null && x.id == id);
        if (def == null || def.condition == null)
            return;

        editingId = def.id;
        editingBuiltIn = def.builtIn;
        editingRemainingScore = def.condition.kind == PenaltyConditionKind.RemainingExactScore;

        // Legacy-Kinds auf neue Bodies mappen (Upgrade erst bei Save).
        // HitTypeMiss bleibt bewusst erhalten (Sofort-Typ, 4. Option) —
        // früher wurde er hier zu TurnPattern umgebogen (Semantikwechsel).
        PenaltyCondition condition = def.condition;
        if (condition.kind == PenaltyConditionKind.RepdigitScore)
            condition = new PenaltyCondition
            {
                kind = PenaltyConditionKind.ExactScore,
                score = condition.minScore
            };

        LoadDef(def.name, def.cost, def.enabled, condition, def.shortName);
        ShowViaWindowHandler();
    }

    private static int DefaultCost(PenaltyConditionKind kind)
    {
        return CustomPenaltyDef.IsGameEventKind(kind) || kind == PenaltyConditionKind.ExactScore
            ? 50 : 100;
    }

    private static PenaltyCondition FreshCondition(PenaltyConditionKind kind)
    {
        if (kind == PenaltyConditionKind.ExactScore)
            return new PenaltyCondition { kind = kind, score = 100 };

        if (CustomPenaltyDef.IsGameEventKind(kind))
            return new PenaltyCondition { kind = kind };

        return new PenaltyCondition
        {
            kind = PenaltyConditionKind.TurnPattern,
            matchOrder = false,
            pattern = new List<TurnPatternEntry>
            {
                new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true }
            }
        };
    }

    private void ShowViaWindowHandler()
    {
        if (windowHandler != null)
            windowHandler.ShowPopup(this);
        else
            gameObject.SetActive(true);
    }

    private void LoadDef(string name, int cost, bool enabled, PenaltyCondition condition, string shortName = "")
    {
        editingName = name;

        if (nameInput != null)
            nameInput.text = name;

        if (shortNameInput != null)
            shortNameInput.text = shortName ?? "";

        if (costInput != null)
            costInput.text = cost.ToString();

        enabledToggle?.Initialize(enabled);

        int typeIndex = TypeToIndex(condition?.kind ?? PenaltyConditionKind.TurnPattern);
        if (conditionTypeDropdown != null)
            conditionTypeDropdown.SetValueWithoutNotify(typeIndex);

        if (scoreInput != null)
            scoreInput.text = (condition?.score ?? 0).ToString();

        if (eventKindDropdown != null)
            eventKindDropdown.SetValueWithoutNotify(GameEventToIndex(condition?.kind ?? PenaltyConditionKind.LegLost));

        string extraSectorId = condition != null
            && condition.kind == PenaltyConditionKind.HitTypeMiss
            && condition.hitType == HitType.Custom
                ? condition.customHitId
                : null;

        RefreshInstantSectorOptions(extraSectorId);

        if (instantHitTypeDropdown != null)
            instantHitTypeDropdown.SetValueWithoutNotify(InstantSectorToIndex(condition));

        if (condition?.kind == PenaltyConditionKind.TurnPattern || typeIndex == 0)
        {
            int slots = Mathf.Clamp(condition?.pattern?.Count ?? 3, 1, 3);
            patternEditor?.SetSlotCount(slots);
            patternEditor?.LoadEntries(condition?.pattern, condition?.matchOrder ?? false);
        }

        UpdateConditionGroups();
    }

    public void ToggleEnabled()
    {
        enabledToggle?.Toggle();
    }

    public void ToggleMatchOrder()
    {
        patternEditor?.ToggleMatchOrder();
    }

    public void OnSlotCountChanged()
    {
        patternEditor?.OnSlotCountChanged();
    }

    private void UpdateConditionGroups()
    {
        int index = conditionTypeDropdown != null ? conditionTypeDropdown.value : 0;

        if (patternBody != null)
            patternBody.SetActive(index == 0);
        if (scoreBody != null)
            scoreBody.SetActive(index == 1);
        if (gameEventBody != null)
            gameEventBody.SetActive(index == 2);

        // Vierter Body (Sofort-Typ) braucht ein eigenes Gruppen-Objekt in der
        // Szene (instantHitTypeGroup + instantHitTypeDropdown verdrahten).
        var instantBody = instantHitTypeGroup != null
            ? instantHitTypeGroup
            : patternBody;
        if (instantBody != null && instantBody != patternBody)
            instantBody.SetActive(index == 3);
    }

    public void OnClickSave()
    {
        if (TrySave())
            CloseAndRefresh();
    }

    /// <summary>
    /// Speichert den aktuellen Stand, ohne das Popup zu schließen. Wird auch
    /// vom Recorder genutzt, damit neue Einträge vor dem ClipBrowser existieren.
    /// </summary>
    private bool TrySave()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
            return false;

        if (!int.TryParse(costInput != null ? costInput.text : "0", out int cost) || cost < 0)
            return false;

        bool enabled = enabledToggle == null || enabledToggle.IsActive();

        string shortName = shortNameInput != null ? shortNameInput.text.Trim() : "";
        if (shortName.Length > 16)
            shortName = shortName.Substring(0, 16);

        // Namen sind pro Penalty eindeutig (eigene ID ausgenommen).
        if (CustomizationStore.IsPenaltyNameTaken(name, editingId))
            return false;

        var condition = ReadCondition();
        if (condition == null)
            return false;

        // Built-ins (Altdaten-Seeds): Bedingung ist fix, nur Name/Kosten/
        // Enabled sind editierbar (Anzeige + Clip-Verlinkung + Engine-Sync).
        if (editingBuiltIn)
        {
            var existing = CustomizationStore.GetPenalties().Find(x => x != null && x.id == editingId);
            if (existing != null && existing.builtIn && existing.condition != null)
                condition = existing.condition;
        }

        CustomizationStore.SavePenalty(new CustomPenaltyDef
        {
            id = editingId,
            name = name,
            shortName = shortName,
            enabled = enabled,
            cost = cost,
            builtIn = editingBuiltIn,
            condition = condition
        });

        editingName = name;

        // Direkt-Refresh der eigenen Liste (sofort sichtbar, auch wenn die
        // Changed-Event-Kette z.B. bei inaktivem Handler nicht geliefert hat).
        // Dank Abgleich ist ein doppelter Refresh praktisch gratis.
        penaltiesPage?.Refresh();
        return true;
    }

    /// <summary>
    /// Recorder-Button: speichert zunächst den aktuellen Stand (neue Einträge
    /// inklusive), schließt das Popup und öffnet den ClipBrowser für diese ID.
    /// </summary>
    public void OnClickOpenClips()
    {
        if (string.IsNullOrEmpty(editingId) || !TrySave())
            return;

        string label = nameInput != null && !string.IsNullOrEmpty(nameInput.text)
            ? nameInput.text.Trim()
            : editingName;

        windowHandler?.HidePopup(0f);
        penaltiesPage?.OnClickOpenClips(editingId, label);
    }

    private PenaltyCondition ReadCondition()
    {
        int index = conditionTypeDropdown != null ? conditionTypeDropdown.value : 0;

        switch (index)
        {
            case 1:
                if (!int.TryParse(scoreInput != null ? scoreInput.text : "", out int score) || score < 0)
                    return null;
                return new PenaltyCondition
                {
                    kind = editingRemainingScore
                        ? PenaltyConditionKind.RemainingExactScore
                        : PenaltyConditionKind.ExactScore,
                    score = score
                };

            case 2:
                return new PenaltyCondition
                {
                    kind = IndexToGameEvent(eventKindDropdown != null ? eventKindDropdown.value : 0)
                };

            default:
                return new PenaltyCondition
                {
                    kind = PenaltyConditionKind.TurnPattern,
                    pattern = patternEditor != null
                        ? patternEditor.SaveEntries()
                        : new List<TurnPatternEntry>(),
                    matchOrder = patternEditor != null && patternEditor.LoadMatchOrder()
                };
        }
    }

    public void OnClickDelete()
    {
        if (editingBuiltIn)
            return;
        if (string.IsNullOrEmpty(editingId))
            return;

        pendingDeleteId = editingId;

        if (confirmText != null)
            confirmText.text = LocalizationText.Format("Delete_penalty_named", "Delete penalty “{0}”?", editingName);

        SetConfirmView(true);
    }

    public void ConfirmDelete()
    {
        if (string.IsNullOrEmpty(pendingDeleteId))
        {
            SetConfirmView(false);
            return;
        }

        CustomizationStore.DeletePenalty(pendingDeleteId);

        penaltiesPage?.Refresh();

        SetConfirmView(false);
        pendingDeleteId = null;
        editingName = null;

        CloseAndRefresh();
    }

    public void CancelDelete()
    {
        SetConfirmView(false);
        pendingDeleteId = null;
        editingName = null;
    }

    private void SetConfirmView(bool confirm)
    {
        if (mainPanel != null)
            mainPanel.SetActive(!confirm);
        if (confirmPanel != null)
            confirmPanel.SetActive(confirm);
    }

    public void OnClickCancel()
    {
        CloseAndRefresh();
    }

    private void CloseAndRefresh()
    {
        editingId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Kein explizites Refresh: Save feuert CustomizationStore.Changed,
        // das die Penalty-Liste bereits aktualisiert (Cancel ändert nichts).
    }

    private static int TypeToIndex(PenaltyConditionKind kind)
    {
        if (kind == PenaltyConditionKind.ExactScore
            || kind == PenaltyConditionKind.RemainingExactScore)
            return 1;
        if (CustomPenaltyDef.IsGameEventKind(kind))
            return 2;
        return 0;
    }

    /// <summary>
    /// Baut die Instant-Sektor-Auswahl neu auf: Wall/Ceiling zuerst, danach
    /// alle Custom-Sektoren aus dem Store. extraSectorId hält eine geladene
    /// Bedingung wählbar, deren Sektor inzwischen umbenannt/gelöscht wurde.
    /// </summary>
    private void RefreshInstantSectorOptions(string extraSectorId = null)
    {
        if (instantHitTypeDropdown == null)
            return;

        instantSectorIds.Clear();
        AddInstantSectorOption(CustomHitDef.WallId);
        AddInstantSectorOption(CustomHitDef.CeilingId);

        foreach (var def in CustomizationStore.GetHitSectors())
        {
            if (def != null && !string.IsNullOrEmpty(def.id))
                AddInstantSectorOption(def.id);
        }

        AddInstantSectorOption(extraSectorId);

        var labels = new List<string>(instantSectorIds.Count);
        foreach (var id in instantSectorIds)
            labels.Add(InstantSectorLabel(id));

        instantHitTypeDropdown.ClearOptions();
        instantHitTypeDropdown.AddOptions(labels);
    }

    private void AddInstantSectorOption(string sectorId)
    {
        if (string.IsNullOrEmpty(sectorId))
            return;

        foreach (var existing in instantSectorIds)
        {
            if (string.Equals(existing, sectorId, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        instantSectorIds.Add(sectorId);
    }

    private static string InstantSectorLabel(string sectorId)
    {
        var def = CustomizationStore.GetHitSectors()
            .Find(x => x != null && string.Equals(x.id, sectorId,
                System.StringComparison.OrdinalIgnoreCase));

        if (def != null && !string.IsNullOrEmpty(def.name))
            return def.name;

        if (CustomHitDef.IsBuiltInSector(sectorId))
            return CustomHitDef.HitTypeForSector(sectorId).ToString();

        return sectorId;
    }

    private int InstantSectorToIndex(PenaltyCondition condition)
    {
        if (condition == null || instantSectorIds.Count == 0)
            return 0;

        string sectorId = null;

        if (condition.kind == PenaltyConditionKind.HitTypeMiss)
        {
            sectorId = condition.hitType switch
            {
                HitType.Wall => CustomHitDef.WallId,
                HitType.Ceiling => CustomHitDef.CeilingId,
                HitType.Custom => condition.customHitId,
                _ => null
            };
        }

        if (string.IsNullOrEmpty(sectorId))
            return 0;

        int index = instantSectorIds.FindIndex(id =>
            string.Equals(id, sectorId, System.StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : 0;
    }

    private string SelectedInstantSectorId()
    {
        if (instantSectorIds.Count == 0)
            return CustomHitDef.WallId;

        int index = instantHitTypeDropdown != null ? instantHitTypeDropdown.value : 0;
        index = Mathf.Clamp(index, 0, instantSectorIds.Count - 1);
        return instantSectorIds[index];
    }

    private static PenaltyConditionKind IndexToGameEvent(int index)
    {
        return index switch
        {
            1 => PenaltyConditionKind.SetLost,
            2 => PenaltyConditionKind.GameLost,
            _ => PenaltyConditionKind.LegLost
        };
    }

    private static int GameEventToIndex(PenaltyConditionKind kind)
    {
        return kind switch
        {
            PenaltyConditionKind.SetLost => 1,
            PenaltyConditionKind.GameLost => 2,
            _ => 0
        };
    }
}
