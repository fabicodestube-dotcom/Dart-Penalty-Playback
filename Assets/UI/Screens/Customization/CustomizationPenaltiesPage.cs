using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Tab 2 of the customization screen: penalties (turn patterns, exact scores,
/// leg/set/game events). Uniform rows (CustomizationListItem): name,
/// clip count/icon, recorder, edit, delete, toggle directly in the row.
/// New/edit/delete run through the editor (PenaltyEditorPopup); delete only
/// if not builtIn, with a warning on clips.
/// </summary>
public class CustomizationPenaltiesPage : MonoBehaviour, ICustomizationRowOwner
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Liste")]
    public Transform listParent;
    public Transform turnPatternParent;
    public Transform exactScoreParent;
    public Transform gameEventParent;
    public GameObject rowPrefab;

    [Header("Editor")]
    public PenaltyEditorPopup editor;

    [Header("Clips (Szene-Umbau, optional)")]
    public ClipNavigationHelper clipNav;

    [Header("Delete-Popup (Ja/Nein)")]
    public UIScreen deletePopup;
    public TMP_Text deletePopupText;

    private string pendingDeleteId;

    /// <summary>
    /// Living rows per penalty ID. Refresh rebalances instead of everything
    /// build: new ones are visible in the same frame, existing ones will only
    /// Updated, disappeared people fly out. No flicker, no waiting.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, CustomizationListItem> rowsById =
        new System.Collections.Generic.Dictionary<string, CustomizationListItem>();

    private void Awake()
    {
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
    }

    private void OnEnable()
    {
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        CustomizationStore.Changed += HandleStoreChanged;
        Refresh();
    }

    private void OnDisable()
    {
        CustomizationStore.Changed -= HandleStoreChanged;
    }

    private void HandleStoreChanged(CustomizationChangeKind kind)
    {
        if (kind == CustomizationChangeKind.Penalties || kind == CustomizationChangeKind.Sectors)
            Refresh();
    }

    public void Refresh()
    {
        if (rowPrefab == null)
            return;

        var wanted = new System.Collections.Generic.HashSet<string>();
        var siblingByParent = new System.Collections.Generic.Dictionary<Transform, int>();

        foreach (var def in CustomizationStore.GetPenalties()
            .Where(d => d != null && !string.IsNullOrEmpty(d.id))
            .OrderBy(d => d.enabled ? 0 : 1))
        {
            UpsertRow(def, wanted);

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

        // Remove missing rows.
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

    private void UpsertRow(CustomPenaltyDef def, System.Collections.Generic.HashSet<string> wanted)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return;

        wanted.Add(def.id);

        Transform parent = ParentFor(def.conditions != null && def.conditions.Count > 0 ? def.conditions.FirstOrDefault().kind : PenaltyConditionKind.TurnPattern);
        if (parent == null)
            parent = listParent;
        if (parent == null || rowPrefab == null)
            return;

        if (!rowsById.TryGetValue(def.id, out var row) || row == null)
        {
            var go = Instantiate(rowPrefab, parent);
            row = go.GetComponent<CustomizationListItem>();
            if (row == null)
            {
                Debug.LogError("CustomizationPenaltiesPage: rowPrefab ohne CustomizationListItem.");
                Destroy(go);
                return;
            }
            row.Setup(this, def.id, def.name, def.enabled);
            rowsById[def.id] = row;
        }
        else
        {
            if (row.transform.parent != parent)
                row.transform.SetParent(parent, false);
            row.Setup(this, def.id, def.name, def.enabled);
        }
    }

    private Transform ParentFor(PenaltyConditionKind kind)
    {
        return kind switch
        {
            PenaltyConditionKind.ExactScore => exactScoreParent != null ? exactScoreParent : listParent,
            PenaltyConditionKind.RemainingExactScore => exactScoreParent != null ? exactScoreParent : listParent,
            PenaltyConditionKind.LegLost => gameEventParent != null ? gameEventParent : listParent,
            PenaltyConditionKind.SetLost => gameEventParent != null ? gameEventParent : listParent,
            PenaltyConditionKind.GameLost => gameEventParent != null ? gameEventParent : listParent,
            _ => turnPatternParent != null ? turnPatternParent : listParent
        };
    }

    public void OnClickNewPenalty()
    {
        // The editor is a UIScreen (id popup) and shows itself
        // via WindowHandler (analogous to deletePopup ).
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        if (editor != null)
            editor.ShowNew(this);
    }

    public void OnClickNewScore()
    {
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        if (editor != null)
            editor.ShowNew(this, PenaltyConditionKind.ExactScore);
    }

    /// <summary>New buttons Game events (OnClick with int: 0=Leg, 1=Set, 2=Game).</summary>
    public void OnClickNewGameEvent(int kindIndex)
    {
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        if (editor != null)
        {
            editor.ShowNew(this, kindIndex switch
            {
                1 => PenaltyConditionKind.SetLost,
                2 => PenaltyConditionKind.GameLost,
                _ => PenaltyConditionKind.LegLost
            });
        }
    }

    public void OnClickEditPenalty(string id)
    {
        if (editor == null)
            editor = FindAnyObjectByType<PenaltyEditorPopup>();

        if (editor != null)
            editor.ShowEdit(this, id);
    }

    // ---- ICustomizationRowOwner (uniform list item) ----

    public void OnRowEdit(string id) => OnClickEditPenalty(id);

    public void OnRowDelete(string id) => OnClickDeletePenalty(id);

    public void OnRowOpenClips(string id) => OnClickOpenClips(id);

    public int GetRowClipCount(string id) => GetClipCount(id);

    public string GetRowInfo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        var def = CustomizationStore.GetPenalties().Find(x => x != null && x.id == id);
        if (def == null)
            return "";

        if (def.conditions != null && def.conditions.Count > 0
            && def.conditions.FirstOrDefault().kind == PenaltyConditionKind.HitTypeMiss
            && def.conditions.FirstOrDefault().hitType == HitType.Custom
            && !string.IsNullOrEmpty(def.conditions.FirstOrDefault().customHitId))
        {
            return PenaltyDisplay.SummarizeCondition(def.conditions.FirstOrDefault());
        }

        return CustomizationInfo.LocalizeInfo(string.IsNullOrEmpty(def.info) ? CustomizationInfo.BuildPenalty(def) : def.info);
    }

    /// <summary>Toggle directly in the row: save enabled (Changed → Refresh).</summary>
    public void OnRowToggle(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id))
            return;

        var def = CustomizationStore.GetPenalties().Find(x => x != null && x.id == id);
        if (def == null || def.enabled == enabled)
            return;

        def.enabled = enabled;
        CustomizationStore.SavePenalty(def);

        // Direct refresh (immediately visible; changed event updated if necessary).
        Refresh();
    }

    /// <summary>Clip button of a penalty row: open the ClipBrowser for this ID.</summary>
    public void OnClickOpenClips(string id, string labelOverride = null)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (clipNav == null)
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
        if (clipNav == null)
            return;

        string label = !string.IsNullOrEmpty(labelOverride)
            ? labelOverride
            : CustomizationStore.GetPenalties()
                .Find(x => x != null && x.id == id)?.name ?? id;

        clipNav.OpenCustomClips(id, label);
    }

    /// <summary>Clip number for the row indicator.</summary>
    public int GetClipCount(string id)
    {
        if (string.IsNullOrEmpty(id) || AudioLibrary.Instance == null)
            return 0;

        return AudioLibrary.Instance.GetCustomClipCount(id);
    }

    public void OnClickDeletePenalty(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        pendingDeleteId = id;

        int clipCount = AudioLibrary.Instance != null
            ? AudioLibrary.Instance.GetCustomClipCount(id)
            : 0;

        if (deletePopupText != null)
        {
            deletePopupText.text = clipCount > 0
                ? LocalizationText.Format("Delete_penalty_with_clips", "Delete penalty? Linked audio clips: {0}", clipCount)
                : LocalizationText.Get("Delete_penalty_confirm", "Delete this penalty?");
        }

        if (windowHandler != null && deletePopup != null)
            windowHandler.ShowPopup(deletePopup);
        else
            ConfirmDeletePenalty();
    }

    public void ConfirmDeletePenalty()
    {
        if (!string.IsNullOrEmpty(pendingDeleteId))
            CustomizationStore.DeletePenalty(pendingDeleteId);

        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Direct refresh (immediately visible; Changed event updates if needed.
        // Then it is virtually free thanks to the match.
        Refresh();
    }

    public void CancelDeletePenalty()
    {
        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();
    }
}
