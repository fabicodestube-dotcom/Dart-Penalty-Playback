using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-Popup für Penalties (Tab 2). Hauptpanel zeigt eine Liste aller
/// Triggern (ScoreReached, TurnPattern, GameEvent, TargetArea). Über „Neu“
/// und „Bearbeiten“ öffnet sich das Trigger-Panel (einzelner Trigger-Editor).
/// Ein Penalty ohne Trigger wird beim Speichern automatisch deaktiviert.
/// Built-ins haben eine feste Triggerliste (nur Name/Kosten/Enabled editierbar).
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

    [Header("Trigger-Liste (MainPanel)")]
    public Transform triggerListContent;
    public GameObject triggerRowPrefab;
    public Button newTriggerButton;

    [Header("Trigger-Editor (Trigger-Panel)")]
    public GameObject triggerPanel;
    public TMP_Dropdown conditionTypeDropdown;
    public GameObject patternBody;
    public GameObject scoreBody;
    public GameObject gameEventBody;
    public TurnPatternEditor patternEditor;
    public TMP_InputField scoreInput;
    public TMP_Dropdown eventKindDropdown;
    public TMP_Dropdown instantHitTypeDropdown;
    public GameObject instantHitTypeGroup;
    public Button saveTriggerButton;
    public Button backToListButton;

    [Header("Basis")]
    public TMP_InputField nameInput;
    public TMP_InputField costInput;
    public CustomToggle enabledToggle;
    public Button deleteButton;
    public TMP_InputField shortNameInput;

    [Header("Fehleranzeige")]
    public GameObject errorPanel;
    public TMP_Text errorText;
    public Image nameOutline;
    public Image costOutline;
    public Image scoreOutline;

    private static readonly Color ErrorOutlineColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    private Color nameOutlineColor = Color.white;
    private Color costOutlineColor = Color.white;
    private Color scoreOutlineColor = Color.white;

    private bool isLoading;
    private bool editingNew;
    private bool triggerPanelDirty;

    private string editingId;
    private bool editingBuiltIn;
    private string pendingDeleteId;
    private string editingName;

    private bool editingRemainingScore;
    private int? editingTriggerIndex;
    private PenaltyCondition editingCondition;

    private readonly List<string> instantSectorIds = new List<string>();
    private List<PenaltyCondition> workingConditions;

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
                LocalizationText.Get("Game_events", "Game events"),
                LocalizationText.Get("Target_area", "Target area")
            });
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

        if (patternEditor != null)
            patternEditor.SelectionChanged += RefreshValidation;

        if (nameInput != null)
            nameInput.onValueChanged.AddListener(_ => RefreshValidation());
        if (costInput != null)
            costInput.onValueChanged.AddListener(_ => RefreshValidation());
        if (scoreInput != null)
            scoreInput.onValueChanged.AddListener(_ => RefreshValidation());

        if (newTriggerButton != null)
            newTriggerButton.onClick.AddListener(() => OnNewTrigger());

        if (saveTriggerButton != null)
            saveTriggerButton.onClick.AddListener(OnSaveTrigger);

        if (backToListButton != null)
            backToListButton.onClick.AddListener(ShowTriggerList);

        CaptureOutlineDefaults();
        HideError();
    }

    private void CaptureOutlineDefaults()
    {
        if (nameOutline != null)
            nameOutlineColor = nameOutline.color;
        if (costOutline != null)
            costOutlineColor = costOutline.color;
        if (scoreOutline != null)
            scoreOutlineColor = scoreOutline.color;
    }

    private void ClearOutlines()
    {
        RestoreOutline(nameOutline, nameOutlineColor);
        RestoreOutline(costOutline, costOutlineColor);
        RestoreOutline(scoreOutline, scoreOutlineColor);
    }

    private static void RestoreOutline(Image outline, Color color)
    {
        if (outline != null)
            outline.color = color;
    }

    private void ShowError(string message, Image invalidOutline)
    {
        if (string.IsNullOrEmpty(message))
        {
            HideError();
            return;
        }

        ClearOutlines();
        if (invalidOutline != null)
            invalidOutline.color = ErrorOutlineColor;
        if (errorText != null)
            errorText.text = message;
        if (errorPanel != null)
            errorPanel.SetActive(true);
    }

    private void HideError()
    {
        ClearOutlines();
        if (errorText != null)
            errorText.text = "";
        if (errorPanel != null)
            errorPanel.SetActive(false);
    }

    private void ShowViaWindowHandler()
    {
        if (windowHandler != null)
            windowHandler.ShowPopup(this);
        else
            gameObject.SetActive(true);
    }

    private void RefreshDeleteButton()
    {
        bool canDelete = !editingNew && !editingBuiltIn && !string.IsNullOrEmpty(editingId);

        if (deleteButton == null)
            return;

        deleteButton.interactable = canDelete;
        deleteButton.gameObject.SetActive(canDelete);
    }

    public void OnShow()
    {
        SetConfirmView(false);
        pendingDeleteId = null;

        if (editingNew)
        {
            ShowTriggerEditor();
        }
        else
        {
            ShowTriggerList();
        }
    }

    public void OnHide()
    {
    }

    private void OnDestroy()
    {
        if (patternEditor != null)
            patternEditor.SelectionChanged -= RefreshValidation;
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
        editingNew = true;
        editingRemainingScore = kind == PenaltyConditionKind.RemainingExactScore;
        workingConditions = new List<PenaltyCondition> { FreshCondition(kind) };
        editingTriggerIndex = null;
        triggerPanelDirty = false;

        isLoading = true;
        LoadTriggerEditor(workingConditions[0]);
        isLoading = false;
        RefreshValidation();
        ShowViaWindowHandler();
    }

    public void ShowEdit(CustomizationPenaltiesPage page, string id)
    {
        penaltiesPage = page != null ? page : penaltiesPage;

        var def = CustomizationStore.GetPenalties().Find(x => x != null && x.id == id);
        if (def == null || def.conditions == null || def.conditions.Count == 0)
            return;

        editingId = def.id;
        editingBuiltIn = def.builtIn;
        editingNew = false;
        editingRemainingScore = def.conditions.FirstOrDefault()?.kind == PenaltyConditionKind.RemainingExactScore;
        workingConditions = def.conditions.Select(c =>
        {
            var clone = new PenaltyCondition
            {
                kind = c.kind,
                hitType = c.hitType,
                customHitId = c.customHitId,
                score = c.score,
                minScore = c.minScore,
                matchOrder = c.matchOrder,
                pattern = c.pattern != null ? new List<TurnPatternEntry>(c.pattern) : new List<TurnPatternEntry>()
            };
            return clone;
        }).ToList();
        editingTriggerIndex = null;
        triggerPanelDirty = false;

        isLoading = true;
        ShowTriggerList();
        isLoading = false;
        RefreshValidation();
        ShowViaWindowHandler();
    }

    private void ShowTriggerList()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);
        if (triggerPanel != null)
            triggerPanel.SetActive(false);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        RefreshTriggerListUI();
        RefreshValidation();
        RefreshDeleteButton();
    }

    private void ShowTriggerEditor()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (triggerPanel != null)
            triggerPanel.SetActive(true);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        var condition = editingTriggerIndex.HasValue
            ? workingConditions[editingTriggerIndex.Value]
            : FreshCondition(editingRemainingScore ? PenaltyConditionKind.RemainingExactScore : PenaltyConditionKind.TurnPattern);

        isLoading = true;
        LoadTriggerEditor(condition);
        isLoading = false;
        RefreshValidation();
    }

    private void RefreshTriggerListUI()
    {
        if (triggerListContent == null || triggerRowPrefab == null)
            return;

        foreach (Transform child in triggerListContent)
            Destroy(child.gameObject);

        if (workingConditions == null || workingConditions.Count == 0)
        {
            var go = new GameObject("EmptyHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(triggerListContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = LocalizationText.Get("No_triggers", "Keine Trigger");
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 16;
            return;
        }

        for (int i = 0; i < workingConditions.Count; i++)
        {
            var condition = workingConditions[i];
            var go = Instantiate(triggerRowPrefab, triggerListContent);
            var rowRect = go.GetComponent<RectTransform>();
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 0.5f);
                rowRect.sizeDelta = new Vector2(0f, 60f);
            }

            var summaryText = go.GetComponentInChildren<TextMeshProUGUI>();
            if (summaryText == null)
            {
                var textGo = new GameObject("Summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(0f, 0f);
                textRt.anchorMax = new Vector2(1f, 1f);
                textRt.offsetMin = new Vector2(20f, 0f);
                textRt.offsetMax = new Vector2(-120f, 0f);
                summaryText = textGo.GetComponent<TextMeshProUGUI>();
                summaryText.alignment = TextAlignmentOptions.Left;
                summaryText.fontSize = 14;
            }
            summaryText.text = PenaltyDisplay.SummarizeCondition(condition);

            int index = i;

            var editBtn = go.GetComponentInChildren<Button>(true);
            if (editBtn == null)
            {
                var btnGo = new GameObject("EditButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Button), typeof(Image));
                btnGo.transform.SetParent(go.transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0.5f);
                btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.pivot = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-70f, 0f);
                btnRt.sizeDelta = new Vector2(60f, 40f);
                editBtn = btnGo.GetComponent<Button>();
                var btnImage = btnGo.GetComponent<Image>();
                btnImage.color = new Color(0.2f, 0.6f, 1f, 1f);
                var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRt = btnTextGo.GetComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.sizeDelta = Vector2.zero;
                var btnText = btnTextGo.GetComponent<TextMeshProUGUI>();
                btnText.text = "Bearbeiten";
                btnText.fontSize = 12;
                btnText.alignment = TextAlignmentOptions.Center;
            }
            editBtn.onClick.RemoveAllListeners();
            editBtn.onClick.AddListener(() => OnEditTrigger(index));

            var delBtn = go.GetComponentsInChildren<Button>(true).LastOrDefault();
            if (delBtn == null || delBtn == editBtn)
            {
                var btnGo = new GameObject("DeleteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Button), typeof(Image));
                btnGo.transform.SetParent(go.transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0.5f);
                btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.pivot = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-10f, 0f);
                btnRt.sizeDelta = new Vector2(60f, 40f);
                delBtn = btnGo.GetComponent<Button>();
                var btnImage = btnGo.GetComponent<Image>();
                btnImage.color = new Color(0.85f, 0.2f, 0.2f, 1f);
                var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRt = btnTextGo.GetComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.sizeDelta = Vector2.zero;
                var btnText = btnTextGo.GetComponent<TextMeshProUGUI>();
                btnText.text = "Löschen";
                btnText.fontSize = 12;
                btnText.alignment = TextAlignmentOptions.Center;
            }
            delBtn.onClick.RemoveAllListeners();
            delBtn.onClick.AddListener(() => OnDeleteTrigger(index));
        }
    }

    private void LoadTriggerEditor(PenaltyCondition condition)
    {
        editingCondition = condition;

        if (conditionTypeDropdown != null)
        {
            int typeIndex = TypeToIndex(condition?.kind ?? PenaltyConditionKind.TurnPattern);
            conditionTypeDropdown.SetValueWithoutNotify(typeIndex);
        }

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

        if (condition?.kind == PenaltyConditionKind.TurnPattern)
        {
            int slots = Mathf.Clamp(condition?.pattern?.Count ?? 3, 1, 3);
            patternEditor?.SetSlotCount(slots);
            patternEditor?.LoadEntries(condition?.pattern, condition?.matchOrder ?? false);
        }

        UpdateConditionGroups();
    }

    private void OnNewTrigger()
    {
        editingTriggerIndex = null;
        triggerPanelDirty = false;
        ShowTriggerEditor();
    }

    private void OnEditTrigger(int index)
    {
        editingTriggerIndex = index;
        triggerPanelDirty = false;
        ShowTriggerEditor();
    }

    private void OnDeleteTrigger(int index)
    {
        workingConditions.RemoveAt(index);
        if (editingTriggerIndex == index)
            editingTriggerIndex = null;
        else if (editingTriggerIndex.HasValue && editingTriggerIndex > index)
            editingTriggerIndex--;
        ShowTriggerList();
    }

    private void OnSaveTrigger()
    {
        if (!ValidateTriggerEditor(out string error))
        {
            ShowError(error, null);
            return;
        }

        var condition = ReadCondition();
        if (condition == null)
        {
            ShowError(LocalizationText.Get("Save_failed", "Could not save. Please check your input."), null);
            return;
        }

        if (editingTriggerIndex.HasValue)
            workingConditions[editingTriggerIndex.Value] = condition;
        else
            workingConditions.Add(condition);

        ShowTriggerList();
    }

    private bool ValidateTriggerEditor(out string error)
    {
        error = null;

        int typeIndex = conditionTypeDropdown != null ? conditionTypeDropdown.value : 0;
        if (typeIndex == 1
            && (!int.TryParse(scoreInput != null ? scoreInput.text : "", out int score) || score < 0))
        {
            error = LocalizationText.Get("Invalid_number", "Please enter a valid number.");
            return false;
        }

        if (typeIndex == 3 && instantSectorIds.Count == 0)
        {
            error = LocalizationText.Get("Select_target_area", "Please select a target area.");
            return false;
        }

        if (typeIndex == 3 && !CustomizationStore.IsKnownHitSector(SelectedInstantSectorId()))
        {
            error = LocalizationText.Get("Select_target_area", "Please select a target area.");
            return false;
        }

        if (typeIndex == 0 && patternEditor != null && patternEditor.HasUnresolvedSector())
        {
            error = LocalizationText.Get("Select_target_area", "Please select a target area.");
            return false;
        }

        return true;
    }

    private void RefreshValidation()
    {
        if (isLoading)
            return;

        bool triggerPanelActive = triggerPanel != null && triggerPanel.activeInHierarchy;
        if (triggerPanelActive)
        {
            if (ValidateTriggerEditor(out string error))
                HideError();
            else
                ShowError(error, null);
        }
        else
        {
            HideError();
        }
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

        if (instantHitTypeGroup != null)
            instantHitTypeGroup.SetActive(index == 3);

        RefreshValidation();
    }

    public void OnClickSave()
    {
        if (workingConditions == null || workingConditions.Count == 0)
        {
            if (enabledToggle != null)
                enabledToggle.Initialize(false);
        }

        if (!TrySave())
            return;

        CloseAndRefresh();
    }

    private bool TrySave()
    {
        if (workingConditions == null || workingConditions.Count == 0)
        {
            // Ohne Trigger: automatisch deaktivieren, aber trotzdem speichern.
        }

        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            ShowError(LocalizationText.Get("Name_required", "Name is required."), null);
            return false;
        }

        if (CustomizationStore.IsPenaltyNameTaken(name, editingId))
        {
            ShowError(LocalizationText.Get("Name_taken", "Name is already taken."), null);
            return false;
        }

        if (!int.TryParse(costInput != null ? costInput.text : "", out int cost) || cost < 0)
        {
            ShowError(LocalizationText.Get("Invalid_number", "Please enter a valid number."), null);
            return false;
        }

        bool enabled = enabledToggle == null || enabledToggle.IsActive();

        if (workingConditions == null || workingConditions.Count == 0)
            enabled = false;

        string shortName = shortNameInput != null ? shortNameInput.text.Trim() : "";
        if (shortName.Length > 16)
            shortName = shortName.Substring(0, 16);

        // Built-ins: Triggerliste ist fix, nur Name/Kosten/Enabled editierbar.
        if (editingBuiltIn)
        {
            var existing = CustomizationStore.GetPenalties().Find(x => x != null && x.id == editingId);
            if (existing != null && existing.builtIn && existing.conditions != null && existing.conditions.Count > 0)
                workingConditions = existing.conditions.Select(c =>
                {
                    var clone = new PenaltyCondition
                    {
                        kind = c.kind,
                        hitType = c.hitType,
                        customHitId = c.customHitId,
                        score = c.score,
                        minScore = c.minScore,
                        matchOrder = c.matchOrder,
                        pattern = c.pattern != null ? new List<TurnPatternEntry>(c.pattern) : new List<TurnPatternEntry>()
                    };
                    return clone;
                }).ToList();
        }

        if (!CustomizationStore.SavePenalty(new CustomPenaltyDef
        {
            id = editingId,
            name = name,
            shortName = shortName,
            enabled = enabled,
            cost = cost,
            builtIn = editingBuiltIn,
            conditions = workingConditions
        }))
        {
            ShowError(LocalizationText.Get("Save_failed", "Could not save. Please check your input."), null);
            return false;
        }

        HideError();
        editingName = name;

        penaltiesPage?.Refresh();
        return true;
    }

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

            case 3:
                if (instantSectorIds.Count == 0)
                    return null;

                string sectorId = SelectedInstantSectorId();
                HitType hitType = CustomHitDef.HitTypeForSector(sectorId);

                return new PenaltyCondition
                {
                    kind = PenaltyConditionKind.HitTypeMiss,
                    hitType = hitType,
                    customHitId = hitType == HitType.Custom ? sectorId : null
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
        if (editingNew || editingBuiltIn)
            return;
        if (string.IsNullOrEmpty(editingId))
            return;

        pendingDeleteId = editingId;

        string displayName = nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text)
            ? nameInput.text.Trim()
            : editingName;

        if (confirmText != null)
            confirmText.text = LocalizationText.Format("Delete_penalty_named", "Delete penalty {0}?", displayName);

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
        if (triggerPanel != null)
            triggerPanel.SetActive(false);
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
        workingConditions = null;
        editingTriggerIndex = null;

        if (windowHandler != null)
            windowHandler.HidePopup();
    }

    private static int TypeToIndex(PenaltyConditionKind kind)
    {
        if (kind == PenaltyConditionKind.ExactScore
            || kind == PenaltyConditionKind.RemainingExactScore)
            return 1;
        if (CustomPenaltyDef.IsGameEventKind(kind))
            return 2;
        if (kind == PenaltyConditionKind.HitTypeMiss)
            return 3;
        return 0;
    }

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

        return PenaltyDisplay.SectorName(sectorId);
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

    private static int DefaultCost(PenaltyConditionKind kind)
    {
        return CustomPenaltyDef.IsGameEventKind(kind) || kind == PenaltyConditionKind.ExactScore
            ? 50 : 100;
    }

    private static PenaltyCondition FreshCondition(PenaltyConditionKind kind)
    {
        if (kind == PenaltyConditionKind.ExactScore)
            return new PenaltyCondition { kind = kind, score = 100 };

        if (kind == PenaltyConditionKind.HitTypeMiss)
            return new PenaltyCondition { kind = kind, hitType = HitType.Wall };

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
}
