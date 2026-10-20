using TMPro;
using UnityEngine;

/// <summary>
/// Editor-Popup für Treffer-Bereiche (Tab 1): neu + custom-edit + built-in-edit
/// (Name/Enabled; Custom-Sektoren haben immer Value 0 / Multiplier Single).
/// Ist ein UIScreen mit id Popup und wird über den WindowHandler
/// (ShowPopup/HidePopup) angezeigt, analog zu den übrigen Popups.
/// Buttons: Save → OnClickSave(), Cancel → OnClickCancel(),
/// Toggle → ToggleEnabled().
/// </summary>
public class SectorEditorPopup : UIScreen, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;
    public CustomizationSectorsPage sectorsPage;

    [Header("Bestätigung")]
    public GameObject mainPanel;
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    [Header("Basis")]
    public TMP_InputField nameInput;
    public CustomToggle enabledToggle;

    private string editingId;
    private bool editingBuiltIn;

    private string pendingDeleteId;
    private string editingName;

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

        if (sectorsPage == null)
            sectorsPage = FindAnyObjectByType<CustomizationSectorsPage>();
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

    public void ShowNew(CustomizationSectorsPage page)
    {
        sectorsPage = page != null ? page : sectorsPage;

        var fresh = CustomHitDef.CreateNew();
        editingId = fresh.id;
        editingBuiltIn = false;

        LoadDef(fresh.name, true);
        ShowViaWindowHandler();
    }

    public void ShowEdit(CustomizationSectorsPage page, string id)
    {
        sectorsPage = page != null ? page : sectorsPage;

        var def = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == id);
        if (def == null)
            return;

        editingId = def.id;
        editingBuiltIn = def.builtIn;

        LoadDef(def.name, def.enabled);
        ShowViaWindowHandler();
    }

    private void ShowViaWindowHandler()
    {
        if (windowHandler != null)
            windowHandler.ShowPopup(this);
        else
            gameObject.SetActive(true);
    }

    private void LoadDef(string name, bool enabled)
    {
        editingName = name;

        if (nameInput != null)
            nameInput.text = name;

        enabledToggle?.Initialize(enabled);
    }

    public void ToggleEnabled()
    {
        enabledToggle?.Toggle();
    }

    public void OnClickSave()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
            return;

        bool enabled = enabledToggle == null || enabledToggle.IsActive();

        // Namen sind pro Bereich eindeutig (eigene ID ausgenommen).
        if (CustomizationStore.IsHitSectorNameTaken(name, editingId))
            return;

        CustomizationStore.SaveHitSector(new CustomHitDef
        {
            id = editingId,
            name = name,
            enabled = enabled,
            builtIn = editingBuiltIn
        });

        // Direkt-Refresh der eigenen Liste (sofort sichtbar, siehe PenaltyEditorPopup).
        sectorsPage?.Refresh();

        CloseAndRefresh();
    }

    public void OnClickDelete()
    {
        if (editingBuiltIn)
            return;
        if (string.IsNullOrEmpty(editingId))
            return;

        pendingDeleteId = editingId;

        var usage = CustomizationStore.GetPenaltyUsageCount(editingId);
        if (confirmText != null)
        {
            confirmText.text = usage > 0
                ? $"Sektor „{editingName}“ löschen?\n({usage} Penalty{ (usage > 1 ? "en" : "") } werden mitgelöscht.)"
                : $"Sektor „{editingName}“ löschen?";
        }

        SetConfirmView(true);
    }

    public void ConfirmDelete()
    {
        if (string.IsNullOrEmpty(pendingDeleteId))
        {
            SetConfirmView(false);
            return;
        }

        CustomizationStore.DeletePenaltiesUsingSector(pendingDeleteId);
        CustomizationStore.DeleteHitSector(pendingDeleteId);

        sectorsPage?.Refresh();

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
        // das die Sektor-Liste bereits aktualisiert (Cancel ändert nichts).
    }
}
