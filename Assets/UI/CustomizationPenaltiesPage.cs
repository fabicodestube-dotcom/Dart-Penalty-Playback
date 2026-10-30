using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Tab 2 des Customization-Screens: Penalties (Turn-Patterns, exakte Scores,
/// Leg-/Set-/Game-Events). Einheitliche Rows (CustomizationListItem): Name,
/// Clip-Anzahl/-Icon, Recorder, Edit, Delete, Toggle direkt in der Zeile.
/// Neu/Edit/Delete laufen über den Editor (PenaltyEditorPopup); Löschen nur
/// wenn nicht builtIn, mit Warnung bei Clips.
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
    /// Lebende Rows pro Penalty-ID. Refresh gleicht ab statt alles neu zu
    /// bauen: Neue sind im selben Frame sichtbar, Bestehende werden nur
    /// aktualisiert, Verschwundene fliegen raus. Kein Flackern, kein Warten.
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

        // Verschwundene Rows entfernen.
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

        Transform parent = ParentFor(def.condition != null ? def.condition.kind : PenaltyConditionKind.TurnPattern);
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
        // Der Editor ist ein UIScreen (id Popup) und zeigt sich selbst
        // über den WindowHandler an (analog zu deletePopup).
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

    /// <summary>Neu-Buttons Game-Events (OnClick mit int: 0=Leg, 1=Set, 2=Game).</summary>
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

    // ---- ICustomizationRowOwner (einheitliches Listen-Item) ----

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

        return CustomizationInfo.LocalizeInfo(string.IsNullOrEmpty(def.info) ? CustomizationInfo.BuildPenalty(def) : def.info);
    }

    /// <summary>Toggle direkt in der Zeile: enabled speichern (Changed → Refresh).</summary>
    public void OnRowToggle(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id))
            return;

        var def = CustomizationStore.GetPenalties().Find(x => x != null && x.id == id);
        if (def == null || def.enabled == enabled)
            return;

        def.enabled = enabled;
        CustomizationStore.SavePenalty(def);

        // Direkt-Refresh (sofort sichtbar; Changed-Event aktualisiert ggf. zusätzlich).
        Refresh();
    }

    /// <summary>Clip-Button einer Penalty-Row: öffnet den ClipBrowser für diese ID.</summary>
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

    /// <summary>Clip-Anzahl für den Row-Indikator.</summary>
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
        if (!string.IsNullOrEmpty(pendingDeleteId)
            && AppSettingsManager.Instance != null)
        {
            CustomizationStore.DeletePenalty(pendingDeleteId);
        }

        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Direkt-Refresh (sofort sichtbar; Changed-Event aktualisiert ggf.
        // zusätzlich — dank Abgleich praktisch gratis).
        Refresh();
    }

    public void CancelDeletePenalty()
    {
        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();
    }
}
