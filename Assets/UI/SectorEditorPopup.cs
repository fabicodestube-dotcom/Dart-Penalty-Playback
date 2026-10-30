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

    [Header("Wert (nur Custom-Sektoren)")]
    public TMP_Dropdown valueDropdown;
    public TMP_Dropdown multiplierDropdown;

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

        var def = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == editingId);

        if (valueDropdown != null)
        {
            // Optionen: 0-20 + 25. Index 0..20 = Wert 0..20, Index 21 = 25.
            valueDropdown.ClearOptions();
            var vOpts = new System.Collections.Generic.List<string>();
            for (int v = 0; v <= 20; v++)
                vOpts.Add(v.ToString());
            vOpts.Add("25");
            valueDropdown.AddOptions(vOpts);

            int idx = 0;
            if (def != null)
            {
                if (def.value == 25)
                    idx = 21;
                else
                    idx = Mathf.Clamp(def.value, 0, 20);
            }
            valueDropdown.SetValueWithoutNotify(idx);
            valueDropdown.interactable = !editingBuiltIn;

            if (valueDropdown.captionText != null)
                valueDropdown.captionText.text = valueDropdown.options.Count > idx
                    ? valueDropdown.options[idx].text : "";
        }

        if (multiplierDropdown != null)
        {
            multiplierDropdown.ClearOptions();
            multiplierDropdown.AddOptions(new System.Collections.Generic.List<string> { "Single", "Double", "Triple" });

            int idx = 0;
            if (def != null)
            {
                string target = def.multiplier switch
                {
                    DartMultiplier.Double => "Double",
                    DartMultiplier.Triple => "Triple",
                    _ => "Single"
                };

                for (int i = 0; i < multiplierDropdown.options.Count; i++)
                {
                    if (string.Equals(multiplierDropdown.options[i].text, target, System.StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            multiplierDropdown.SetValueWithoutNotify(idx);
            multiplierDropdown.interactable = !editingBuiltIn;

            if (multiplierDropdown.captionText != null)
                multiplierDropdown.captionText.text = multiplierDropdown.options.Count > idx
                    ? multiplierDropdown.options[idx].text : "";
        }
    }

    public void ToggleEnabled()
    {
        enabledToggle?.Toggle();
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

        bool enabled = enabledToggle == null || enabledToggle.IsActive();

        // Wert/Multiplier nur für Custom-Sektoren (built-ins bleiben 0/Single).
        int value = 0;
        DartMultiplier multiplier = DartMultiplier.Single;
        if (!editingBuiltIn)
        {
            if (valueDropdown != null)
            {
                int idx = valueDropdown.value;
                value = (idx >= 21) ? 25 : Mathf.Clamp(idx, 0, 20);
            }
            if (multiplierDropdown != null)
            {
                string label = multiplierDropdown.options.Count > multiplierDropdown.value
                    ? multiplierDropdown.options[multiplierDropdown.value].text
                    : "";

                multiplier = label switch
                {
                    var l when string.Equals(l, "Double", System.StringComparison.OrdinalIgnoreCase) => DartMultiplier.Double,
                    var l when string.Equals(l, "Triple", System.StringComparison.OrdinalIgnoreCase) => DartMultiplier.Triple,
                    _ => DartMultiplier.Single
                };
            }
            // Triple nicht auf 25.
            if (multiplier == DartMultiplier.Triple && value == 25)
                value = 20;
        }
        else
        {
            var existing = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == editingId);
            if (existing != null)
            {
                value = existing.value;
                multiplier = existing.multiplier;
            }
        }

        // Namen sind pro Bereich eindeutig (eigene ID ausgenommen).
        if (CustomizationStore.IsHitSectorNameTaken(name, editingId))
            return false;

        CustomizationStore.SaveHitSector(new CustomHitDef
        {
            id = editingId,
            name = name,
            enabled = enabled,
            builtIn = editingBuiltIn,
            value = value,
            multiplier = multiplier
        });

        editingName = name;

        // Direkt-Refresh der eigenen Liste (sofort sichtbar, siehe PenaltyEditorPopup).
        sectorsPage?.Refresh();
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
        sectorsPage?.OnClickOpenClips(editingId, label);
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
                ? LocalizationText.Format("Delete_sector_named_usage", "Delete target area “{0}”? Used by penalties: {1}. These will also be deleted.", editingName, usage)
                : LocalizationText.Format("Delete_sector_named", "Delete target area “{0}”?", editingName);
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
