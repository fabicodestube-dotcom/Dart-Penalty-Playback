using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Editor-Popup für Audio-Events (Tab 3). Seeds (builtIn) sind nicht
/// löschbar, aber enabled-/trigger-editierbar; Customs frei editierbar.
/// Ein Kategorie-Dropdown (Global/X01/Cricket/ATC, Code füllt die Optionen)
/// schaltet die Trigger-Art um; die Trigger-Gruppen (score/threshold/
/// pattern) folgen der gewählten Kombination. Manual-Trigger (Seeds wie
/// Undo/Bust/Marks) bleiben beim Editieren erhalten (kein Dropdown-Eintrag).
/// Kategorie ist beim Anlegen UND beim Editieren änderbar (die Page hängt
/// die Row per SetParent um). Buttons: Save → OnClickSave(),
/// Cancel → OnClickCancel(). Ist ein UIScreen (id Popup) und zeigt sich
/// über den WindowHandler an (analog zu PenaltyEditorPopup).
/// </summary>
public class AudioEventEditorPopup : UIScreen, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;
    public CustomizationAudioEventsPage eventsPage;

    [Header("Bestätigung")]
    public GameObject mainPanel;
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    [Header("Basis")]
    public TMP_InputField nameInput;
    public TMP_Dropdown categoryDropdown;
    [Tooltip("Legacy-Anzeige (wird nur noch mitbeschriftet, falls verdrahtet).")]
    public TMP_Text categoryLabel;
    public CustomToggle enabledToggle;

    [Header("Trigger")]
    public TMP_Dropdown triggerKindDropdown;
    public TMP_InputField scoreInput;
    public TMP_InputField thresholdInput;
    public TurnPatternEditor patternEditor;

    [Header("Gruppen (Umschalter Sichtbarkeit)")]
    public GameObject scoreGroup;
    public GameObject thresholdGroup;
    public GameObject patternGroup;

    private string editingId;
    private AudioEventCategory editingCategory;
    private bool editingBuiltIn;
    private bool editingManual;
    private string pendingDeleteId;
    private string editingName;

    // Dropdown-Reihenfolge = Enum-Reihenfolge (Global=0, X01=1, Cricket=2, ATC=3).
    private static readonly List<string> CategoryOptions = new List<string>
    {
        "Global", "X01", "Cricket", "ATC"
    };

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

        if (eventsPage == null)
            eventsPage = FindAnyObjectByType<CustomizationAudioEventsPage>();

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (categoryDropdown != null)
        {
            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(CategoryOptions);
            categoryDropdown.onValueChanged.AddListener(_ => OnCategoryChanged());
        }

        if (triggerKindDropdown != null)
            triggerKindDropdown.onValueChanged.AddListener(_ => UpdateTriggerGroups());
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

    /// <summary>Single-Button-Einstieg (Szene): Kategorie wählt man im Popup.</summary>
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

        LoadDef("New event", true, TriggerForCategory(category, 0), new AudioEventTrigger());
        ShowViaWindowHandler();
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
        editingManual = def.trigger != null && def.trigger.kind == AudioEventTriggerKind.Manual;

        LoadDef(def.name, def.enabled, def.trigger.kind, def.trigger);
        ShowViaWindowHandler();
    }

    private void ShowViaWindowHandler()
    {
        if (windowHandler != null)
            windowHandler.ShowPopup(this);
        else
            gameObject.SetActive(true);
    }

    /// <summary>Aktuelle Kategorie (Dropdown gewinnt, sonst Edit-Stand).</summary>
    private AudioEventCategory CurrentCategory()
    {
        if (categoryDropdown != null)
            return (AudioEventCategory)Mathf.Clamp(categoryDropdown.value, 0, 3);
        return editingCategory;
    }

    /// <summary>
    /// Kategorie-Wechsel im Popup: Trigger-Optionen neu aufbauen, gleiche
    /// Trigger-Art übernehmen wenn die neue Kategorie sie anbietet.
    /// Eingetippte Werte (Score/Threshold/Pattern) bleiben erhalten.
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
            triggerKindDropdown.SetValueWithoutNotify((int)TriggerIndex(newCategory, oldKind));
        }

        if (categoryLabel != null)
            categoryLabel.text = newCategory.ToString();

        UpdateTriggerGroups();
    }

    /// <summary>Trigger-Optionen je Kategorie (Dropdown-Reihenfolge = Index).</summary>
    private static List<string> TriggerOptions(AudioEventCategory category)
    {
        return category switch
        {
            AudioEventCategory.X01 => new List<string> { "Score reached (=)", "Turn pattern" },
            AudioEventCategory.Cricket => new List<string> { "Marks ≥", "Points ≥", "Turn pattern" },
            AudioEventCategory.ATC => new List<string> { "Hits/Turn ≥", "Streak ≥", "Turn pattern" },
            _ => new List<string> { "Score reached (=)", "Turn pattern" }
        };
    }

    private static AudioEventTriggerKind TriggerForCategory(AudioEventCategory category, int index)
    {
        return category switch
        {
            AudioEventCategory.Cricket => index switch
            {
                1 => AudioEventTriggerKind.PointsReached,
                2 => AudioEventTriggerKind.TurnPattern,
                _ => AudioEventTriggerKind.MarksThreshold
            },
            AudioEventCategory.ATC => index switch
            {
                1 => AudioEventTriggerKind.StreakReached,
                2 => AudioEventTriggerKind.TurnPattern,
                _ => AudioEventTriggerKind.HitsInTurnThreshold
            },
            // Global wie X01: exakter Turn-Score oder Turn-Pattern (Engine wertet
            // rein nach Trigger-Art aus, Kategorie ist nur Gruppierung).
            _ => index == 1
                ? AudioEventTriggerKind.TurnPattern : AudioEventTriggerKind.ScoreReached
        };
    }

    private void LoadDef(string name, bool enabled, AudioEventTriggerKind kind, AudioEventTrigger trigger)
    {
        editingName = name;

        if (nameInput != null)
            nameInput.text = name;

        enabledToggle?.Initialize(enabled);

        if (categoryDropdown != null)
            categoryDropdown.SetValueWithoutNotify((int)editingCategory);

        if (categoryLabel != null)
            categoryLabel.text = editingCategory.ToString();

        if (triggerKindDropdown != null)
        {
            triggerKindDropdown.ClearOptions();
            triggerKindDropdown.AddOptions(TriggerOptions(editingCategory));
            triggerKindDropdown.SetValueWithoutNotify((int)TriggerIndex(editingCategory, kind));
        }

        if (scoreInput != null)
            scoreInput.text = trigger.score.ToString();

        if (thresholdInput != null)
            thresholdInput.text = trigger.threshold.ToString();

        patternEditor?.LoadEntries(trigger.pattern, trigger.matchExact);

        UpdateTriggerGroups();
    }

    private static int TriggerIndex(AudioEventCategory category, AudioEventTriggerKind kind)
    {
        return category switch
        {
            AudioEventCategory.Cricket => kind switch
            {
                AudioEventTriggerKind.PointsReached => 1,
                AudioEventTriggerKind.TurnPattern => 2,
                _ => 0
            },
            AudioEventCategory.ATC => kind switch
            {
                AudioEventTriggerKind.StreakReached => 1,
                AudioEventTriggerKind.TurnPattern => 2,
                _ => 0
            },
            // Global/X01: ScoreReached=0, TurnPattern=1 (Rest fällt auf Score zurück).
            _ => kind == AudioEventTriggerKind.TurnPattern ? 1 : 0
        };
    }

    public void OnTriggerKindChanged()
    {
        UpdateTriggerGroups();
    }

    public void ToggleEnabled()
    {
        enabledToggle?.Toggle();
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
    }

    public void OnClickSave()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
            return;

        var category = CurrentCategory();
        editingCategory = category;

        // Manual-Seeds (Undo/Bust/Marks/...): Trigger bleibt Manual
        // (kein Dropdown-Eintrag, kein versehentliches Umschreiben).
        AudioEventTriggerKind kind;
        var trigger = new AudioEventTrigger();
        if (editingManual)
        {
            kind = AudioEventTriggerKind.Manual;
        }
        else
        {
            kind = TriggerForCategory(category,
                triggerKindDropdown != null ? triggerKindDropdown.value : 0);
            trigger.kind = kind;
        }

        switch (kind)
        {
            case AudioEventTriggerKind.ScoreReached:
                if (!int.TryParse(scoreInput != null ? scoreInput.text : "", out int score) || score < 0)
                    return;
                trigger.score = score;
                break;

            case AudioEventTriggerKind.MarksThreshold:
            case AudioEventTriggerKind.PointsReached:
            case AudioEventTriggerKind.HitsInTurnThreshold:
            case AudioEventTriggerKind.StreakReached:
                if (!int.TryParse(thresholdInput != null ? thresholdInput.text : "", out int threshold) || threshold < 0)
                    return;
                trigger.threshold = threshold;
                break;

            case AudioEventTriggerKind.TurnPattern:
                trigger.pattern = patternEditor != null
                    ? patternEditor.SaveEntries()
                    : new List<TurnPatternEntry>();
                trigger.matchExact = patternEditor != null && patternEditor.LoadMatchExact();
                break;

            case AudioEventTriggerKind.Manual:
                // Manual-Seeds: Trigger bleibt leer (direkter Engine-Aufruf).
                break;

            default:
                return;
        }

        // Toggle nicht verdrahtet (Szene): bestehenden enabled-Stand behalten,
        // sonst ungewolltes Re-Aktivieren beim Editieren.
        bool enabled;
        if (enabledToggle != null)
            enabled = enabledToggle.IsActive();
        else
            enabled = CustomizationStore.GetAudioEvents()
                .Find(x => x != null && x.id == editingId)?.enabled ?? true;

        CustomizationStore.SaveAudioEvent(new AudioEventDef
        {
            id = editingId,
            name = name,
            builtIn = editingBuiltIn,
            enabled = enabled,
            category = editingCategory,
            trigger = trigger
        });

        // Direkt-Refresh der eigenen Liste (sofort sichtbar, siehe PenaltyEditorPopup).
        eventsPage?.Refresh();

        CloseAndRefresh();
    }

    public void OnClickDelete()
    {
        if (editingBuiltIn)
            return;
        if (string.IsNullOrEmpty(editingId))
            return;

        pendingDeleteId = editingId;

        if (confirmText != null)
            confirmText.text = $"Audio-Event „{editingName}“ löschen?";

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
        editingBuiltIn = false;
        editingManual = false;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Kein explizites Refresh: Save feuert CustomizationStore.Changed,
        // das die Event-Liste bereits aktualisiert (Cancel ändert nichts).
    }
}
