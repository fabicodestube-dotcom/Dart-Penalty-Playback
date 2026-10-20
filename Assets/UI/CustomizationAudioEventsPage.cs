using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tab 3 des Customization-Screens: Audio-Events je Kategorie
/// (Global/X01/Cricket/ATC). Einheitliche Rows (CustomizationListItem):
/// Name, Clip-Anzahl/-Icon, Recorder, Edit, Delete, Toggle direkt in der
/// Zeile. Seed-Events sind nicht löschbar (builtIn), aber enabled-/trigger-
/// editierbar; Customs anlegen/editieren/löschen (mit Clip-Warnung). Dazu eine
/// Score-Sammel-Row (0–180, gleiches Prefab, Toggle = Ansagen an/aus, nie
/// löschbar), die das Score-Grid öffnet. Clips direkt über die Row.
/// </summary>
public class CustomizationAudioEventsPage : MonoBehaviour, ICustomizationRowOwner
{
    /// <summary>Pseudo-ID der Score-Sammel-Row (kein AudioEventDef).</summary>
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
    /// Lebende Rows pro Event-ID (Parent-übergreifend). Refresh gleicht ab:
    /// Einträge werden einmalig aufgebaut und danach nur aktualisiert,
    /// Customs erscheinen im selben Frame wie der Save. Kein Flackern,
    /// kein verzögertes Auffüllen.
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

        // Store-Defs (Seeds + Customs): Seeds sind nicht löschbar, werden
        // aber normal angezeigt, editiert und von der Engine ausgewertet.
        var defs = CustomizationStore.GetAudioEvents();
        if (defs != null)
        {
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.id))
                    continue;
                UpsertRow(def, !def.builtIn, wanted);
            }
        }

        // Fallback für leeren Store (sollte nach Seeding nicht vorkommen).
        if (wanted.Count == 0)
        {
            foreach (var def in AudioEventCatalog.BuildBuiltIns())
            {
                if (def == null || string.IsNullOrEmpty(def.id))
                    continue;
                UpsertRow(def, false, wanted);
            }
        }

        // Score-Sammel-Row (Sonderregelung 0–180) in Global.
        // UpsertScoreRow(wanted); — entfernt, da Score-Ansagen nur über
        // das separate Score-Grid erreichbar sein sollen, nicht in der Global-Liste.

        // Verschwundene Rows entfernen.
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
    /// Score-Sammel-Row (0–180, gleiches Prefab): Toggle = Ansagen an/aus
    /// (persistiert in den Sound-Settings), nie löschbar. Edit + Recorder
    /// öffnen das Score-Grid.
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
    /// Legt die Row an (sofort sichtbar) oder aktualisiert sie.
    /// Gibt false zurück, wenn nichts zu tun war (null/ohne ID).
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
            // Kategorie kann sich geändert haben → umhängen.
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

    /// <summary>Neu-Button (Single-Einstieg, Szene): Kategorie wählt man im Popup.</summary>
    public void OnClickNewAudioEvent()
    {
        if (editor != null)
            editor.ShowNew(this);
    }

    /// <summary>Neu-Buttons mit Kategorie-Vorbelegung (OnClick mit int: 1=X01, 2=Cricket, 3=ATC).</summary>
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

    // ---- ICustomizationRowOwner (einheitliches Listen-Item) ----

    public void OnRowEdit(string id) => OnClickEditAudioEvent(id);

    public void OnRowDelete(string id) => OnClickDeleteAudioEvent(id);

    public void OnRowOpenClips(string id) => OnClickOpenClips(id);

    public int GetRowClipCount(string id) => GetClipCount(id);

    public string GetRowInfo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        if (id == ScoreRowId)
            return "Sound for score announcements 0-180";

        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null)
            def = AudioEventCatalog.BuildBuiltIns().Find(x => x != null && x.id == id);
        if (def == null)
            return "";

        return string.IsNullOrEmpty(def.info) ? CustomizationInfo.BuildAudioEvent(def) : def.info;
    }

    /// <summary>
    /// Toggle direkt in der Zeile: Score-Row → Sound-Setting, sonst
    /// enabled speichern (Changed → Refresh).
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

            // Direkt-Refresh (Toggle-Stand sofort sichtbar).
            Refresh();
            return;
        }

        var def = CustomizationStore.GetAudioEvents().Find(x => x != null && x.id == id);
        if (def == null || def.enabled == enabled)
            return;

        def.enabled = enabled;
        CustomizationStore.SaveAudioEvent(def);

        // Direkt-Refresh (sofort sichtbar; Changed-Event aktualisiert ggf. zusätzlich).
        Refresh();
    }

    /// <summary>Clip-Button einer Event-Row: Score-Row → Grid, sonst Browser für die ID.</summary>
    public void OnClickOpenClips(string id)
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

        string label = CustomizationStore.GetAudioEvents()
            .Find(x => x != null && x.id == id)?.name ?? id;

        clipNav.OpenCustomClips(id, label);
    }

    /// <summary>Clip-Anzahl für den Row-Indikator (Score-Row: Summe 0–180).</summary>
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
                ? $"Event löschen? Es sind noch {clipCount} Audio-Clips damit verbunden."
                : "Event wirklich löschen?";
        }

        if (windowHandler != null && deletePopup != null)
            windowHandler.ShowPopup(deletePopup);
        else
            ConfirmDeleteAudioEvent();
    }

    public void ConfirmDeleteAudioEvent()
    {
        if (!string.IsNullOrEmpty(pendingDeleteId)
            && AppSettingsManager.Instance != null)
        {
            CustomizationStore.DeleteAudioEvent(pendingDeleteId);
        }

        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();

        // Direkt-Refresh (sofort sichtbar; siehe PenaltiesPage).
        Refresh();
    }

    public void CancelDeleteAudioEvent()
    {
        pendingDeleteId = null;

        if (windowHandler != null)
            windowHandler.HidePopup();
    }
}
