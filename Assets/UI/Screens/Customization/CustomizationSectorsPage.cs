using System.Linq;
using UnityEngine;

/// <summary>
/// Tab 1 of the customization screen ("areas"): hit sectors
/// (seed areas wall/ceiling + customs). Uniform rows
/// (CustomizationListItem): name, clip count/icon, recorder, edit,
/// delete, toggle directly in the row. Delete only if not builtIn.
/// The yes/no confirmation including linked penalties runs through the
/// DeletePanel of the SectorEditorPopup (no separate scene popup required).
/// </summary>
public class CustomizationSectorsPage : MonoBehaviour, ICustomizationRowOwner
{
    [Header("Liste")]
    public Transform listParent;
    public GameObject rowPrefab;

    [Header("Editor")]
    public SectorEditorPopup editor;

    [Header("Clips (Szene-Umbau, optional)")]
    public ClipNavigationHelper clipNav;

    /// <summary>
    /// Living rows per sector ID (reconciliation instead of rebuilding, see Penalties).
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, CustomizationListItem> rowsById =
        new System.Collections.Generic.Dictionary<string, CustomizationListItem>();

    private void Awake()
    {
        if (editor == null)
            editor = FindAnyObjectByType<SectorEditorPopup>();

        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
    }

    private void OnEnable()
    {
        if (editor == null)
            editor = FindAnyObjectByType<SectorEditorPopup>();

        Refresh();
    }

    public void Refresh()
    {
        if (listParent == null || rowPrefab == null)
            return;

        var wanted = new System.Collections.Generic.HashSet<string>();

        int sibling = 0;
        foreach (var def in CustomizationStore.GetHitSectors()
            .Where(d => d != null && !string.IsNullOrEmpty(d.id))
            .OrderBy(d => d.enabled ? 0 : 1))
        {
            wanted.Add(def.id);

            if (!rowsById.TryGetValue(def.id, out var row) || row == null)
            {
                row = AddRow(def.id, def.name, def.enabled, def.builtIn);
                if (row == null)
                    continue;
                rowsById[def.id] = row;
            }
            else
            {
                row.Setup(this, def.id, def.name, def.enabled);
            }

            row.transform.SetSiblingIndex(sibling++);
        }

        var gone = new System.Collections.Generic.List<string>();
        foreach (var kvp in rowsById)
        {
            if (!wanted.Contains(kvp.Key))
                gone.Add(kvp.Key);
        }
        foreach (var id in gone)
        {
            if (rowsById.TryGetValue(id, out var row) && row != null)
                Destroy(row.gameObject);
            rowsById.Remove(id);
        }
    }

    private CustomizationListItem AddRow(string id, string name, bool enabled, bool builtIn)
    {
        var go = Instantiate(rowPrefab, listParent);
        var row = go.GetComponent<CustomizationListItem>();

        if (row == null)
        {
            Debug.LogError("CustomizationSectorsPage: rowPrefab ohne CustomizationListItem.");
            Destroy(go);
            return null;
        }

        row.Setup(this, id, name, enabled);
        return row;
    }

    public void OnClickNewSector()
    {
        // The editor is a UIScreen (id popup) and shows itself
        // via the WindowHandler.
        if (editor == null)
            editor = FindAnyObjectByType<SectorEditorPopup>();

        if (editor != null)
            editor.ShowNew(this);
    }

    public void OnClickEditSector(string id)
    {
        if (editor == null)
            editor = FindAnyObjectByType<SectorEditorPopup>();

        if (editor != null)
            editor.ShowEdit(this, id);
    }

    // ---- ICustomizationRowOwner (uniform list item) ----

    public void OnRowEdit(string id) => OnClickEditSector(id);

    public void OnRowDelete(string id) => OnClickDeleteSector(id);

    public void OnRowOpenClips(string id) => OnClickOpenClips(id);

    public int GetRowClipCount(string id) => GetClipCount(id);

    public string GetRowInfo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        var def = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == id);
        if (def == null)
            return "";

        return CustomizationInfo.LocalizeInfo(string.IsNullOrEmpty(def.info) ? CustomizationInfo.BuildSector(def) : def.info);
    }

    /// <summary>Toggle directly in the row: save enabled (Changed → Refresh).</summary>
    public void OnRowToggle(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id))
            return;

        var def = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == id);
        if (def == null || def.enabled == enabled)
            return;

        def.enabled = enabled;
        CustomizationStore.SaveHitSector(def);

        // Direct refresh (immediately visible; changed event updated if necessary).
        Refresh();
    }

    /// <summary>
    /// Clip button of a sector row: open the ClipBrowser. Wall/Ceiling
    /// hang on their instant penalties (there are costs + sound,
    /// custom_wall/custom_ceiling); free sectors use their own
    /// Sector ID as Clip Key.
    /// </summary>
    public void OnClickOpenClips(string id, string labelOverride = null)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
        if (clipNav == null)
            return;

        string key = ResolveSectorClipKey(id);
        string label = !string.IsNullOrEmpty(labelOverride)
            ? labelOverride
            : CustomizationStore.GetHitSectors()
                .Find(x => x != null && x.id == id)?.name ?? id;

        clipNav.OpenCustomClips(key, label);
    }

    /// <summary>Clip number for the row indicator (including linked penalty).</summary>
    public int GetClipCount(string id)
    {
        if (string.IsNullOrEmpty(id) || AudioLibrary.Instance == null)
            return 0;

        return AudioLibrary.Instance.GetCustomClipCount(ResolveSectorClipKey(id));
    }

    /// <summary>
    /// Sector sound always runs through the sector ID (wall = builtin:wall),
    /// Ceiling = builtin:ceiling, free sectors = own ID). Penalty clips
    /// remain separately under the penalty ID and are additionally played.
    /// </summary>
    public string ResolveSectorClipKey(string sectorId)
    {
        return sectorId;
    }

    public void OnClickDeleteSector(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        var def = CustomizationStore.GetHitSectors().Find(x => x != null && x.id == id);
        if (def == null || def.builtIn || CustomHitDef.IsBuiltInSector(id))
            return;

        if (editor == null)
            editor = FindAnyObjectByType<SectorEditorPopup>();

        editor?.ShowDeleteConfirm(this, id);
    }
}
