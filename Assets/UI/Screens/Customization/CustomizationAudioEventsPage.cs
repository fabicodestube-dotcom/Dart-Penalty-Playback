using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Tab 3 of the customization screen: audio events per category
/// (Global/X01/Cricket/ATC). Uniform rows (CustomizationListItem):
/// name, clip count/icon, recorder, edit, delete, toggle directly in the
/// row. Seed events are not deletable (builtIn), but enabled/trigger-
/// editable; create/edit/delete customs (with clip warning). Plus a
/// score collection row (0–180, same prefab, toggle = announcements on/off, never
/// deleted), which opens the score grid. Clips directly from the row.
/// </summary>
public class CustomizationAudioEventsPage : MonoBehaviour, ICustomizationRowOwner
{
    /// <summary>Pseudo-ID of the Score-Collect-Row (no AudioEventDef ).</summary>
    public const string ScoreRowId = "__scores__";

    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Listen je Kategorie")]
    public Transform globalParent;
    public Transform x01Parent;
    public Transform cricketParent;
    public Transform atcParent;
    public GameObject rowPrefab;

    [Header("Editor")]
    public AudioEventEditorPopup editor;

    [Header("Clips (Szene-Umbau, optional)")]
    public ClipNavigationHelper clipNav;
    public ScoreClipGridHandler scoreGrid;

    [Header("Delete-Popup (Ja/Nein)")]
    public UIScreen deletePopup;
    public TMP_Text deletePopupText;

    private string pendingDeleteId;

    /// <summary>
    /// Live rows per event ID (cross-parent).
    /// Entries are built once and then only updated,
    /// Customs appear in the same frame as the save.
    /// No delayed filling.
    /// </summary>
    private readonly Dictionary<string, CustomizationListItem> rowsById =
        new Dictionary<string, CustomizationListItem>();

    private void Awake()
    {
        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
        if (scoreGrid == null)
            scoreGrid = FindAnyObjectByType<ScoreClipGridHandler>();
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (rowPrefab == null)
            return;

        var wanted = new HashSet<string>();
        var siblingByParent = new Dictionary<Transform, int>();

        // Store defs (seeds + customs): Built-in seeds are not erasable,
        // user-like seeds already; both are displayed normally, edited
        // And it's evaluated by the engine.
        var defs = CustomizationStore.GetAudioEvents();
        if (defs != null)
        {
            foreach (var def in defs
                .Where(d => d != null && !string.IsNullOrEmpty(d.id))
                .OrderBy(d => d.enabled ? 0 : 1))
            {
                UpsertRow(def, !def.builtIn, wanted);

                if (rowsById.TryGetValue(def.id, out var row) && row != null)
                {
                    var parent = row.transform.parent;
                    if (parent == null)
                        continue;
                    if (!siblingByParent.TryGetValue(parent, out int sib))
                        sib = 0;
                    row.transform.SetSiblingIndex(sib);
                    siblingByParent[parent] = sib + 1;
                }
            }
        }

        // Fallback for empty store (should not occur after seeding).
        if (wanted.Count == 0)
        {
            foreach (var def in AudioEventCatalog.BuildBuiltIns()
                .Where(d => d != null && !string.IsNullOrEmpty(d.id))
                .OrderBy(d => d.enabled ? 0 : 1))
            {
                UpsertRow(def, false, wanted);

                if (rowsById.TryGetValue(def.id, out var row) && row != null)
                {
                    var parent = row.transform.parent;
                    if (parent == null)
                        continue;
                    if (!siblingByParent.TryGetValue(parent, out int sib))
                        sib = 0;
                    row.transform.SetSiblingIndex(sib);
                    siblingByParent[parent] = sib + 1;
                }
            }
        }

        // Score summary row (special rule 0–180) in Global.
        // UpsertScoreRow(wanted); — removed, since score announcements are only handled via
        // the separate score grid, not in the Global list.

        // Remove missing rows.
        var gone = new List<string>();
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

    /// <summary>
    /// Score summary row (0–180, same prefab): toggle = announcements on/off
    /// (persistent in the sound settings), never erasable. edit + recorder
    /// Open the score grid.
    /// </summary>
    private void UpsertScoreRow(HashSet<string> wanted)
    {
        if (globalParent == null || rowPrefab == null)
            return;

        wanted.Add(ScoreRowId);

        if (!rowsById.TryGetValue(ScoreRowId, out var row) || row == null)
        {
            var go = Instantiate(rowPrefab, globalParent);
            row = go.GetComponent<CustomizationListItem>();

            if (row == null)
            {
                Debug.LogError("CustomizationAudioEventsPage: rowPrefab ohne CustomizationListItem.");
                Destroy(go);
                return;
            }

            rowsById[ScoreRowId] = row;
        }
        else if (row.transform.parent != globalParent)
        {
            row.transform.SetParent(globalParent, false);
        }

        row.Setup(this, ScoreRowId, "Score-Ansagen", AreScoreAnnouncementsEnabled());
    }

    private static bool AreScoreAnnouncementsEnabled()
    {
        var settings = AppSettingsManager.Instance != null
            ? AppSettingsManager.Instance.Settings
            : null;
        return settings?.Sound?.ScoreAnnouncementsEnabled ?? true;
    }

    /// <summary>
    /// Create the row (immediately visible) or update it.
    /// Returns false if nothing was to be done (null/without ID).
    /// </summary>
    private bool UpsertRow(AudioEventDef def, bool deletable, HashSet<string> wanted)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return false;

        wanted.Add(def.id);

        if (!rowsById.TryGetValue(def.id, out var row) || row == null)
        {
            Transform parent = ParentFor(def.category);
            if (parent == null || rowPrefab == null)
                return false;

            var go = Instantiate(rowPrefab, parent);
            row = go.GetComponent<CustomizationListItem>();

            if (row == null)
            {
                Debug.LogError("CustomizationAudioEventsPage: rowPrefab ohne CustomizationListItem.");
                Destroy(go);
                return false;
            }

            row.Setup(this, def.id, def.name, def.enabled);
            rowsById[def.id] = row;
        }
        else
        {
            // Category may have changed → reassigning.
            Transform parent = ParentFor(def.category);
            if (parent != null && row.transform.parent != parent)
                row.transform.SetParent(parent, false);

            row.Setup(this, def.id, def.name, def.enabled);
        }

        return true;
    }

    private Transform ParentFor(AudioEventCategory category)
    {
        return category switch
        {
            AudioEventCategory.X01 => x01Parent,
            AudioEventCategory.Cricket => cricketParent,
            AudioEventCategory.ATC => atcParent,
            _ => globalParent
        };
    }

    /// <summary>New button (Single entry, scene): Category to choose in popup.</summary>
    public void OnClickNewAudioEvent()
    {
        if (editor != null)
            editor.ShowNew(this);
    }

    /// <summary>New buttons with category pre-assignment (OnClick with int: 1=X01, 2=Cricket, 3=ATC).</summary>
    public void OnClickNewAudioEvent(int categoryIndex)
    {
        var category = (AudioEventCategory)Mathf.Clamp(categoryIndex, 1, 3);

        if (editor != null)
            editor.ShowNew(this, category);
    }

    public void OnClickEditAudioEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (id == ScoreRowId)
        {
            OpenScoreGrid();
            return;
        }

        if (editor != null)
            editor.ShowEdit(this, id);
    }

    // ---- ICustomizationRowOwner (uniform list item) ----

    public void OnRowEdit(string id) => OnClickEditAudioEvent(id);

    public void OnRowDelete(string id) => OnClickDeleteAudioEvent(id);

    public void OnRowOpenClips(string id) => OnClickOpenClips(id);

    public int GetRowClipCount(string id) => GetClipCount(id);

    public string GetRowInfo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        if (id == ScoreRowId)
            return LocalizationText.Get("Sound_score_announcements", "Sound for score announcements 0-180");

        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null)
            def = AudioEventCatalog.BuildBuiltIns().Find(x => x != null && x.id == id);
        if (def == null)
            return "";

        return CustomizationInfo.LocalizeInfo(string.IsNullOrEmpty(def.info) ? CustomizationInfo.BuildAudioEvent(def) : def.info);
    }

    /// <summary>
    /// Toggle right in line: Score-Row → Sound-Setting, otherwise
    /// Save enabled (Changed → Refresh).
    /// </summary>
    public void OnRowToggle(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (id == ScoreRowId)
        {
            bool current = AreScoreAnnouncementsEnabled();
            if (current == enabled || AppSettingsManager.Instance == null)
                return;

            AppSettingsManager.Instance.SetScoreAnnouncementsEnabled(enabled);

            // Direct refresh (toggle state immediately visible).
            Refresh();
            return;
        }

        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null || def.enabled == enabled)
            return;

        def.enabled = enabled;
        CustomizationStore.SaveAudioEvent(def);

        // Direct refresh (immediately visible; changed event updated if necessary).
        Refresh();
    }

    /// <summary>Clip button of an event row: Score-Row → Grid, otherwise browser for the ID.</summary>
    public void OnClickOpenClips(string id, string labelOverride = null)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (id == ScoreRowId)
        {
            OpenScoreGrid();
            return;
        }

        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
        if (clipNav == null)
            return;

        string label = !string.IsNullOrEmpty(labelOverride)
            ? labelOverride
            : CustomizationStore.GetAudioEvents()
                .Find(x => x != null && x.id == id)?.name ?? id;

        clipNav.OpenCustomClips(id, label);
    }

    /// <summary>Clip number for the row indicator (score row: sum 0–180).</summary>
    public int GetClipCount(string id)
    {
        if (string.IsNullOrEmpty(id) || AudioLibrary.Instance == null)
            return 0;

        if (id == ScoreRowId)
        {
            int total = 0;
            for (int score = 0; score <= 180; score++)
                total += AudioLibrary.Instance.GetRuntimeScoreList(score)?.Count ?? 0;
            return total;
        }

        return AudioLibrary.Instance.GetCustomClipCount(id);
    }

    private void OpenScoreGrid()
    {
        if (scoreGrid == null)
            scoreGrid = FindAnyObjectByType<ScoreClipGridHandler>();
        if (scoreGrid != null)
            scoreGrid.Show();
        else
            Debug.LogWarning("CustomizationAudioEventsPage: kein ScoreClipGridHandler in der Szene.");
    }

    public void RefreshScoreGridCounts()
    {
        if (scoreGrid == null)
            scoreGrid = FindAnyObjectByType<ScoreClipGridHandler>();
        scoreGrid?.RefreshCounts();
    }

    public void OnClickDeleteAudioEvent(string id)
    {
        if (string.IsNullOrEmpty(id) || id == ScoreRowId)
            return;

        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null || def.builtIn)
            return;

        pendingDeleteId = id;

        int clipCount = AudioLibrary.Instance != null
            ? AudioLibrary.Instance.GetCustomClipCount(id)
            : 0;

        if (deletePopupText != null)
        {
            deletePopupText.text = clipCount > 0
                ? LocalizationText.Format("Delete_audio_event_with_clips", "Delete audio event? Linked audio clips: {0}", clipCount)
                : LocalizationText.Get("Delete_audio_event_confirm", "Delete this audio event?");
        }

        if (windowHandler != null && deletePopup != null)
            windowHandler.ShowPopup(deletePopup);
        else
            ConfirmDeleteAudioEvent();
    }

    public void ConfirmDeleteAudioEvent()
    {
        if (!string.IsNullOrEmpty(pendingDeleteId))
            CustomizationStore.DeleteAudioEvent(pendingDeleteId);

        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Direct refresh (immediately visible; see PenaltiesPage).
        Refresh();
    }

    public void CancelDeleteAudioEvent()
    {
        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();
    }
}
