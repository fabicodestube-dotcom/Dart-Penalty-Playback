using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Editor-Popup für Audio-Events (Tab 3). Hauptpanel zeigt eine Liste aller
/// Triggern. Über „Neu“ und „Bearbeiten“ öffnet sich das Trigger-Panel
/// (einzelner Trigger-Editor). Ein Event ohne Trigger wird beim Speichern
/// automatisch deaktiviert. Built-ins haben eine feste Triggerliste.
/// </summary>
public class AudioEventEditorPopup : UIScreen, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;
    public LocalizeStringEvent topBarHeadlineLocalize;
    public CustomizationAudioEventsPage eventsPage;

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
    public LocalizeStringEvent triggerHeadlineLocalize;
    public TMP_Dropdown triggerKindDropdown;
    public TMP_InputField scoreInput;
    public TMP_InputField thresholdInput;
    public TurnPatternEditor patternEditor;
    public GameObject scoreGroup;
    public GameObject thresholdGroup;
    public GameObject patternGroup;
    public Button saveTriggerButton;
    public Button backToListButton;

    [Header("Basis")]
    public TMP_InputField nameInput;
    public TMP_Dropdown categoryDropdown;
    [Tooltip("Legacy-Anzeige (wird nur noch mitbeschriftet, falls verdrahtet).")]
    public TMP_Text categoryLabel;
    public CustomToggle enabledToggle;
    public Button deleteButton;

    [Header("Fehleranzeige")]
    public GameObject errorPanel;
    public TMP_Text errorText;
    public Image nameOutline;
    public Image scoreOutline;
    public Image thresholdOutline;

    private static readonly Color ErrorOutlineColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    private Color nameOutlineColor = Color.white;
    private Color scoreOutlineColor = Color.white;
    private Color thresholdOutlineColor = Color.white;

    private bool isLoading;
    private bool editingNew;

    private string editingId;
    private AudioEventCategory editingCategory;
    private bool editingBuiltIn;
    private bool editingManual;
    private string pendingDeleteId;
    private string editingName;

    private int? editingTriggerIndex;
    private List<AudioEventTrigger> workingTriggers;
    private bool fallbackControlsCreated;

    // Dropdown-Reihenfolge = Enum-Reihenfolge (Global=0, X01=1, Cricket=2, ATC=3).
    private static void SetHeadline(LocalizeStringEvent localize, string key)
    {
        if (localize == null)
            return;

        localize.StringReference = new LocalizedString("LocalizationTable", key);
        localize.RefreshString();
    }

    private void OnValidate()
    {
        id = ScreenId.AudioEventEditor;

        if (rect == null)
            rect = GetComponent<RectTransform>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    protected override void Awake()
    {
        base.Awake();

        id = ScreenId.AudioEventEditor;

        if (eventsPage == null)
            eventsPage = FindAnyObjectByType<CustomizationAudioEventsPage>();

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (categoryDropdown != null)
        {
            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(new List<string>
            {
                CategoryDisplayName(AudioEventCategory.Global),
                CategoryDisplayName(AudioEventCategory.X01),
                CategoryDisplayName(AudioEventCategory.Cricket),
                CategoryDisplayName(AudioEventCategory.ATC)
            });
            categoryDropdown.onValueChanged.AddListener(_ => OnCategoryChanged());
        }

        if (triggerKindDropdown != null)
        {
            triggerKindDropdown.onValueChanged.RemoveAllListeners();
            triggerKindDropdown.onValueChanged.AddListener(_ => OnTriggerKindChanged());
        }

        if (newTriggerButton != null)
        {
            newTriggerButton.onClick.RemoveAllListeners();
            newTriggerButton.onClick.AddListener(OnNewTrigger);
        }

        if (saveTriggerButton != null)
        {
            saveTriggerButton.onClick.RemoveAllListeners();
            saveTriggerButton.onClick.AddListener(OnSaveTrigger);
        }

        if (backToListButton != null)
        {
            backToListButton.onClick.RemoveAllListeners();
            backToListButton.onClick.AddListener(ShowTriggerList);
        }

        EnsureFallbackControls();

        if (patternEditor != null)
            patternEditor.SelectionChanged += RefreshValidation;

        if (nameInput != null)
            nameInput.onValueChanged.AddListener(_ => RefreshValidation());
        if (scoreInput != null)
            scoreInput.onValueChanged.AddListener(_ => RefreshValidation());
        if (thresholdInput != null)
            thresholdInput.onValueChanged.AddListener(_ => RefreshValidation());

        CaptureOutlineDefaults();
        HideError();
    }

    private void EnsureFallbackControls()
    {
        if (fallbackControlsCreated)
            return;

        fallbackControlsCreated = true;

        if (newTriggerButton == null)
        {
            Transform newParent = mainPanel != null ? mainPanel.transform : triggerListContent;
            newTriggerButton = CreateFallbackButton(newParent, "New", new Color(0.2f, 0.6f, 1f, 1f), OnNewTrigger);
        }

        EnsureThresholdControls();

        if (triggerPanel == null)
            return;

        if (saveTriggerButton != null && backToListButton != null)
            return;

        Transform editorContent = triggerPanel.transform.Find("Content");
        Transform buttonParent = editorContent != null ? editorContent : triggerPanel.transform;

        var rowGo = new GameObject("TriggerButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGo.transform.SetParent(buttonParent, false);
        var row = rowGo.GetComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 20f;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;
        row.childControlWidth = true;
        row.childControlHeight = true;
        rowGo.GetComponent<LayoutElement>().minHeight = 60f;

        if (saveTriggerButton == null)
            saveTriggerButton = CreateFallbackButton(rowGo.transform, "Save", new Color(0.2f, 0.6f, 0.2f, 1f), OnSaveTrigger);

        if (backToListButton == null)
            backToListButton = CreateFallbackButton(rowGo.transform, "Back", new Color(0.4f, 0.4f, 0.4f, 1f), ShowTriggerList);
    }

    private void EnsureThresholdControls()
    {
        if (thresholdInput != null || thresholdGroup == null || scoreInput == null)
            return;

        thresholdGroup.name = "Threshold";

        foreach (Transform child in thresholdGroup.transform)
            child.gameObject.SetActive(false);

        var clone = Instantiate(scoreInput.gameObject, thresholdGroup.transform, false);
        clone.name = "ThresholdInput";

        var sourceRect = scoreInput.GetComponent<RectTransform>();
        var cloneRect = clone.GetComponent<RectTransform>();
        if (sourceRect != null && cloneRect != null)
        {
            cloneRect.anchorMin = sourceRect.anchorMin;
            cloneRect.anchorMax = sourceRect.anchorMax;
            cloneRect.pivot = sourceRect.pivot;
            cloneRect.anchoredPosition = sourceRect.anchoredPosition;
            cloneRect.sizeDelta = sourceRect.sizeDelta;
            cloneRect.localScale = Vector3.one;
        }

        thresholdInput = clone.GetComponent<TMP_InputField>();
        thresholdOutline = clone.GetComponent<Image>();

        if (thresholdInput != null)
            thresholdInput.text = "0";
    }

    private static Button CreateFallbackButton(Transform parent, string label, Color color, Action onClick)
    {
        if (parent == null)
            return null;

        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = color;

        var layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 160f;
        layout.preferredHeight = 50f;

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private void CaptureOutlineDefaults()
    {
        if (nameOutline != null)
            nameOutlineColor = nameOutline.color;
        if (scoreOutline != null)
            scoreOutlineColor = scoreOutline.color;
        if (thresholdOutline != null)
            thresholdOutlineColor = thresholdOutline.color;
    }

    private void ClearOutlines()
    {
        RestoreOutline(nameOutline, nameOutlineColor);
        RestoreOutline(scoreOutline, scoreOutlineColor);
        RestoreOutline(thresholdOutline, thresholdOutlineColor);
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

    private void ShowScreen()
    {
        if (windowHandler != null)
            windowHandler.GoTo(ScreenId.AudioEventEditor);
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
        SetHeadline(topBarHeadlineLocalize, editingNew ? "New_audio_event" : "Edit_audio_event");
        EnsureFallbackControls();
        SetConfirmView(false);
        pendingDeleteId = null;
        ShowTriggerList();
    }

    public void OnHide()
    {
    }

    private void OnDestroy()
    {
        if (patternEditor != null)
            patternEditor.SelectionChanged -= RefreshValidation;
    }

    public void ShowNew(CustomizationAudioEventsPage page)
    {
        ShowNew(page, AudioEventCategory.Global);
    }

    public void ShowNew(CustomizationAudioEventsPage page, AudioEventCategory category)
    {
        eventsPage = page != null ? page : eventsPage;
        editingCategory = category;
        editingId = System.Guid.NewGuid().ToString("N");
        editingBuiltIn = false;
        editingManual = false;
        editingNew = true;
        SetHeadline(topBarHeadlineLocalize, "New_audio_event");
        workingTriggers = new List<AudioEventTrigger>();
        editingTriggerIndex = null;

        ApplyCategoryToUI(category);

        isLoading = true;
        ResetNewAudioUI();
        ShowTriggerList();
        isLoading = false;
        RefreshDeleteButton();
        RefreshValidation();
        ShowScreen();
    }

    public void ShowEdit(CustomizationAudioEventsPage page, string id)
    {
        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null)
            return;

        eventsPage = page != null ? page : eventsPage;
        editingCategory = def.category;
        editingId = def.id;
        editingBuiltIn = def.builtIn;
        editingManual = def.triggers != null && def.triggers.Count > 0 && def.triggers.FirstOrDefault().kind == AudioEventTriggerKind.Manual;
        editingNew = false;
        SetHeadline(topBarHeadlineLocalize, "Edit_audio_event");
        workingTriggers = def.triggers != null
            ? def.triggers.Select(t => new AudioEventTrigger
            {
                kind = t.kind,
                score = t.score,
                threshold = t.threshold,
                matchOrder = t.matchOrder,
                pattern = t.pattern != null ? new List<TurnPatternEntry>(t.pattern) : new List<TurnPatternEntry>()
            }).ToList()
            : new List<AudioEventTrigger>();
        editingTriggerIndex = null;

        ApplyCategoryToUI(def.category);

        isLoading = true;
        ApplyAudioToUI(def);
        ShowTriggerList();
        isLoading = false;
        RefreshDeleteButton();
        RefreshValidation();
        ShowScreen();
    }

    private void ApplyAudioToUI(AudioEventDef def)
    {
        if (def == null)
            return;

        editingName = def.name;

        if (nameInput != null)
            nameInput.text = def.name ?? "";
        if (enabledToggle != null)
            enabledToggle.Initialize(def.enabled);
    }

    private void ResetNewAudioUI()
    {
        editingName = "New event";

        if (nameInput != null)
            nameInput.text = editingName;
        if (enabledToggle != null)
            enabledToggle.Initialize(true);
    }

    /// <summary>Aktuelle Kategorie (Dropdown gewinnt, sonst Edit-Stand).</summary>
    private AudioEventCategory CurrentCategory()
    {
        if (categoryDropdown != null)
            return (AudioEventCategory)Mathf.Clamp(categoryDropdown.value, 0, 3);
        return editingCategory;
    }

    private void ApplyCategoryToUI(AudioEventCategory category)
    {
        editingCategory = category;

        if (categoryDropdown != null)
            categoryDropdown.SetValueWithoutNotify((int)category);

        if (triggerKindDropdown != null)
        {
            triggerKindDropdown.ClearOptions();
            triggerKindDropdown.AddOptions(TriggerOptions(category));
        }

        if (categoryLabel != null)
            categoryLabel.text = CategoryDisplayName(category);

        UpdateTriggerGroups();
    }

    /// <summary>
    /// Kategorie-Wechsel im Popup: Trigger-Optionen neu aufbauen.
    /// </summary>
    public void OnCategoryChanged()
    {
        var newCategory = CurrentCategory();
        var oldKind = TriggerForCategory(editingCategory,
            triggerKindDropdown != null ? triggerKindDropdown.value : 0);
        editingCategory = newCategory;

        if (triggerKindDropdown != null)
        {
            triggerKindDropdown.ClearOptions();
            triggerKindDropdown.AddOptions(TriggerOptions(newCategory));
            triggerKindDropdown.SetValueWithoutNotify(TriggerIndex(newCategory, oldKind));
        }

        if (categoryLabel != null)
            categoryLabel.text = CategoryDisplayName(newCategory);

        UpdateTriggerGroups();
    }

    private static string CategoryDisplayName(AudioEventCategory category)
    {
        return category == AudioEventCategory.Global
            ? LocalizationText.Get("Global", "Global")
            : category.ToString();
    }

    /// <summary>Trigger-Optionen je Kategorie (Dropdown-Reihenfolge = Index).</summary>
    private static List<string> TriggerOptions(AudioEventCategory category)
    {
        return category switch
        {
            AudioEventCategory.X01 => new List<string> { LocalizationText.Get("Score_reached", "Score reached"), LocalizationText.Get("Turn_pattern", "Turn pattern") },
            AudioEventCategory.Cricket => new List<string> { LocalizationText.Get("Marks_at_least_short", "Marks ≥"), LocalizationText.Get("Points_at_least_short", "Points ≥"), LocalizationText.Get("Score_reached", "Score reached"), LocalizationText.Get("Turn_pattern", "Turn pattern") },
            AudioEventCategory.ATC => new List<string> { LocalizationText.Get("Hits_per_turn_at_least_short", "Hits/Turn ≥"), LocalizationText.Get("Streak_at_least_short", "Streak ≥"), LocalizationText.Get("Turn_pattern", "Turn pattern") },
            _ => new List<string> { LocalizationText.Get("Score_reached", "Score reached"), LocalizationText.Get("Turn_pattern", "Turn pattern") }
        };
    }

    private static AudioEventTriggerKind TriggerForCategory(AudioEventCategory category, int index)
    {
        return category switch
        {
            AudioEventCategory.Cricket => index switch
            {
                1 => AudioEventTriggerKind.PointsReached,
                2 => AudioEventTriggerKind.ScoreReached,
                3 => AudioEventTriggerKind.TurnPattern,
                _ => AudioEventTriggerKind.MarksThreshold
            },
            AudioEventCategory.ATC => index switch
            {
                1 => AudioEventTriggerKind.StreakReached,
                2 => AudioEventTriggerKind.TurnPattern,
                _ => AudioEventTriggerKind.HitsInTurnThreshold
            },
            _ => index == 1
                ? AudioEventTriggerKind.TurnPattern : AudioEventTriggerKind.ScoreReached
        };
    }

    private static int TriggerIndex(AudioEventCategory category, AudioEventTriggerKind kind)
    {
        return category switch
        {
            AudioEventCategory.Cricket => kind switch
            {
                AudioEventTriggerKind.PointsReached => 1,
                AudioEventTriggerKind.ScoreReached => 2,
                AudioEventTriggerKind.TurnPattern => 3,
                _ => 0
            },
            AudioEventCategory.ATC => kind switch
            {
                AudioEventTriggerKind.StreakReached => 1,
                AudioEventTriggerKind.TurnPattern => 2,
                _ => 0
            },
            _ => kind == AudioEventTriggerKind.TurnPattern ? 1 : 0
        };
    }

    public void OnTriggerKindChanged()
    {
        UpdateTriggerGroups();
    }

    private void UpdateTriggerGroups()
    {
        var kind = TriggerForCategory(CurrentCategory(),
            triggerKindDropdown != null ? triggerKindDropdown.value : 0);

        if (scoreGroup != null)
            scoreGroup.SetActive(kind == AudioEventTriggerKind.ScoreReached);
        if (thresholdGroup != null)
            thresholdGroup.SetActive(kind == AudioEventTriggerKind.MarksThreshold
                || kind == AudioEventTriggerKind.PointsReached
                || kind == AudioEventTriggerKind.HitsInTurnThreshold
                || kind == AudioEventTriggerKind.StreakReached);
        if (patternGroup != null)
            patternGroup.SetActive(kind == AudioEventTriggerKind.TurnPattern);

        RefreshValidation();
    }

    public void ToggleEnabled()
    {
        enabledToggle?.Toggle();
    }

    private void ShowTriggerList()
    {
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
        if (triggerPanel != null)
            triggerPanel.SetActive(true);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        SetHeadline(triggerHeadlineLocalize, editingTriggerIndex.HasValue ? "Edit_trigger" : "Add_trigger");

        var trigger = editingTriggerIndex.HasValue
            ? workingTriggers[editingTriggerIndex.Value]
            : new AudioEventTrigger { kind = TriggerForCategory(CurrentCategory(), 0) };

        isLoading = true;
        LoadTriggerEditor(trigger);
        isLoading = false;
        RefreshValidation();
    }

    private void RefreshTriggerListUI()
    {
        if (triggerListContent == null)
            return;

        foreach (Transform child in triggerListContent)
            Destroy(child.gameObject);

        if (workingTriggers == null || workingTriggers.Count == 0)
        {
            var go = new GameObject("EmptyHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(triggerListContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            go.GetComponent<LayoutElement>().preferredHeight = 130f;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = LocalizationText.Get("No_triggers", "Keine Trigger");
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 45;
            return;
        }

        for (int i = 0; i < workingTriggers.Count; i++)
        {
            var trigger = workingTriggers[i];
            GameObject go;
            if (triggerRowPrefab != null)
            {
                go = Instantiate(triggerRowPrefab, triggerListContent);
            }
            else
            {
                go = new GameObject("TriggerRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(triggerListContent, false);
                go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                go.GetComponent<LayoutElement>().preferredHeight = 130f;
            }

            var rowRect = go.GetComponent<RectTransform>();
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 0.5f);
                rowRect.sizeDelta = new Vector2(0f, 130f);
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
            summaryText.text = SummarizeTrigger(trigger);

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

    private static string SummarizeTrigger(AudioEventTrigger t)
    {
        switch (t.kind)
        {
            case AudioEventTriggerKind.ScoreReached:
                return LocalizationText.Format("Reach_exact_score_value", "Reach exact score: {0}", t.score);
            case AudioEventTriggerKind.TurnPattern:
                return LocalizationText.Format("Turn_pattern_any_order", "Turn [{0}] (any order)", CustomizationInfo.DescribePattern(t.pattern));
            case AudioEventTriggerKind.MarksThreshold:
                return LocalizationText.Format("Marks_at_least", "Marks ≥ {0}", t.threshold);
            case AudioEventTriggerKind.PointsReached:
                return LocalizationText.Format("Points_at_least", "Points ≥ {0}", t.threshold);
            case AudioEventTriggerKind.HitsInTurnThreshold:
                return LocalizationText.Format("Hits_per_turn_at_least", "Hits/Turn ≥ {0}", t.threshold);
            case AudioEventTriggerKind.StreakReached:
                return LocalizationText.Format("Streak_at_least", "Streak ≥ {0}", t.threshold);
            case AudioEventTriggerKind.Manual:
            default:
                return LocalizationText.Get("Manual", "Manual");
        }
    }

    private void LoadTriggerEditor(AudioEventTrigger trigger)
    {
        if (triggerKindDropdown != null)
        {
            // Kategorie bestimmt welche Trigger möglich sind; der erste Eintrag
            // der Kategorie passt zum Trigger-Typ (falls vorhanden).
            var kind = trigger.kind;
            var category = CurrentCategory();
            int idx = TriggerIndex(category, kind);
            triggerKindDropdown.SetValueWithoutNotify(idx);
        }

        if (scoreInput != null)
            scoreInput.text = trigger.score.ToString();

        if (thresholdInput != null)
            thresholdInput.text = trigger.threshold.ToString();

        patternEditor?.LoadEntries(trigger.pattern, trigger.matchOrder);

        UpdateTriggerGroups();
    }

    private void OnNewTrigger()
    {
        editingTriggerIndex = null;
        ShowTriggerEditor();
    }

    private void OnEditTrigger(int index)
    {
        editingTriggerIndex = index;
        ShowTriggerEditor();
    }

    private void OnDeleteTrigger(int index)
    {
        workingTriggers.RemoveAt(index);
        if (editingTriggerIndex == index)
            editingTriggerIndex = null;
        else if (editingTriggerIndex.HasValue && editingTriggerIndex > index)
            editingTriggerIndex--;
        ShowTriggerList();
    }

    private void OnSaveTrigger()
    {
        var kind = TriggerForCategory(CurrentCategory(),
            triggerKindDropdown != null ? triggerKindDropdown.value : 0);

        var trigger = new AudioEventTrigger { kind = kind };

        switch (kind)
        {
            case AudioEventTriggerKind.ScoreReached:
                if (!int.TryParse(scoreInput != null ? scoreInput.text : "", out int score) || score < 0)
                {
                    ShowError(LocalizationText.Get("Invalid_number", "Please enter a valid number."), null);
                    return;
                }
                trigger.score = score;
                break;

            case AudioEventTriggerKind.MarksThreshold:
            case AudioEventTriggerKind.PointsReached:
            case AudioEventTriggerKind.HitsInTurnThreshold:
            case AudioEventTriggerKind.StreakReached:
                if (!int.TryParse(thresholdInput != null ? thresholdInput.text : "", out int threshold) || threshold < 0)
                {
                    ShowError(LocalizationText.Get("Invalid_number", "Please enter a valid number."), null);
                    return;
                }
                trigger.threshold = threshold;
                break;

            case AudioEventTriggerKind.TurnPattern:
                trigger.pattern = patternEditor != null
                    ? patternEditor.SaveEntries()
                    : new List<TurnPatternEntry>();
                trigger.matchOrder = patternEditor != null && patternEditor.LoadMatchOrder();
                break;

            case AudioEventTriggerKind.Manual:
                break;

            default:
                ShowError(LocalizationText.Get("Save_failed", "Could not save. Please check your input."), null);
                return;
        }

        if (editingTriggerIndex.HasValue)
            workingTriggers[editingTriggerIndex.Value] = trigger;
        else
            workingTriggers.Add(trigger);

        ShowTriggerList();
    }

    private void RefreshValidation()
    {
        if (isLoading)
            return;

        bool triggerPanelActive = triggerPanel != null && triggerPanel.activeInHierarchy;
        if (triggerPanelActive)
        {
            var kind = TriggerForCategory(CurrentCategory(),
                triggerKindDropdown != null ? triggerKindDropdown.value : 0);

            if (kind == AudioEventTriggerKind.ScoreReached
                && (!int.TryParse(scoreInput != null ? scoreInput.text : "", out int score) || score < 0))
            {
                ShowError(LocalizationText.Get("Invalid_number", "Please enter a valid number."), null);
                return;
            }

            if ((kind == AudioEventTriggerKind.MarksThreshold
                    || kind == AudioEventTriggerKind.PointsReached
                    || kind == AudioEventTriggerKind.HitsInTurnThreshold
                    || kind == AudioEventTriggerKind.StreakReached)
                && (!int.TryParse(thresholdInput != null ? thresholdInput.text : "", out int threshold) || threshold < 0))
            {
                ShowError(LocalizationText.Get("Invalid_number", "Please enter a valid number."), null);
                return;
            }

            if (kind == AudioEventTriggerKind.TurnPattern
                && patternEditor != null
                && patternEditor.HasUnresolvedSector())
            {
                ShowError(LocalizationText.Get("Select_target_area", "Please select a target area."), null);
                return;
            }

            HideError();
        }
        else
        {
            HideError();
        }
    }

    public void OnClickSave()
    {
        if (workingTriggers == null || workingTriggers.Count == 0)
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
        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            ShowError(LocalizationText.Get("Name_required", "Name is required."), null);
            return false;
        }

        if (CustomizationStore.IsAudioEventNameTaken(name, editingId))
        {
            ShowError(LocalizationText.Get("Name_taken", "Name is already taken."), null);
            return false;
        }

        var category = CurrentCategory();
        editingCategory = category;

        bool enabled = enabledToggle == null || enabledToggle.IsActive();
        if (workingTriggers == null || workingTriggers.Count == 0)
            enabled = false;

        if (!CustomizationStore.SaveAudioEvent(new AudioEventDef
        {
            id = editingId,
            name = name,
            builtIn = editingBuiltIn,
            enabled = enabled,
            category = editingCategory,
            triggers = workingTriggers
        }))
        {
            ShowError(LocalizationText.Get("Save_failed", "Could not save. Please check your input."), null);
            return false;
        }

        HideError();
        editingName = name;

        eventsPage?.Refresh();
        return true;
    }

    public void OnClickOpenClips()
    {
        if (string.IsNullOrEmpty(editingId) || !TrySave())
            return;

        string label = nameInput != null && !string.IsNullOrEmpty(nameInput.text)
            ? nameInput.text.Trim()
            : editingName;

        windowHandler?.Replace(ScreenId.CustomClipBrowser);
        eventsPage?.OnClickOpenClips(editingId, label);
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

        HideError();

        if (confirmText != null)
            confirmText.text = LocalizationText.Format("Delete_audio_event_named", "Delete audio event {0}?", displayName);

        SetConfirmView(true);
    }

    public void ConfirmDelete()
    {
        if (string.IsNullOrEmpty(pendingDeleteId))
        {
            SetConfirmView(false);
            return;
        }

        CustomizationStore.DeleteAudioEvent(pendingDeleteId);

        eventsPage?.Refresh();

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
        editingBuiltIn = false;
        editingManual = false;
        workingTriggers = null;
        editingTriggerIndex = null;

        if (windowHandler != null)
            windowHandler.GoTo(ScreenId.Customization);
    }
}
