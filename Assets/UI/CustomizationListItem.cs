using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anbindung einer Customization-Liste (Sektoren, Penalties, Audio-Events
/// inkl. Score-Sammel-Row) an das einheitliche Listen-Item. Die Page liefert
/// Navigation (Edit/Delete/Clips), Toggle-Speicherung und Clip-Anzahl;
/// das Item ist dumm (Anzeige + Klicks) und damit prefab-einheitlich.
/// </summary>
public interface ICustomizationRowOwner
{
    void OnRowEdit(string id);
    void OnRowDelete(string id);
    void OnRowOpenClips(string id);
    void OnRowToggle(string id, bool enabled);
    int GetRowClipCount(string id);
}

/// <summary>
/// Einheitliche Zeile für alle Customization-Listen (Prefab
/// CustomizationListItem, ein Prefab für alle 3 Tabs inkl. Score-Row).
/// Verdrahtung im Prefab: Buttons OnClick → OnClickRecorder/OnClickEdit/
/// OnClickDelete, Toggle-Flächen-Button → OnClickToggle.
/// - nameText: Anzeigename
/// - countText: Clip-Anzahl („Clips: N“)
/// - clipsImage: Icon, gefärbt per Theme (Accent1 bei ≥1 Clip,
///   Background3 bei 0) — Farblogik analog zum alten Clip-Grid
/// - deleteButton: nur sichtbar wenn deletable (Seeds: Events nie,
///   Penalties/Sektoren immer)
/// - toggleRoot/toggle: CustomToggle für aktiv/deaktiviert direkt in der
///   Zeile (Score-Row: Score-Ansagen an/aus); ausblendbar pro Row
/// Felder ohne Verdrahtung in der Szene sind optional (null-safe).
/// </summary>
public class CustomizationListItem : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text countText;
    public Image clipsImage;
    public GameObject deleteButton;

    [Header("Toggle (aktiv/deaktiviert in der Zeile)")]
    public GameObject toggleRoot;
    public CustomToggle toggle;

    private ICustomizationRowOwner owner;
    private string entryId;
    private bool deletable;
    private bool _subscribed;

    public void Setup(ICustomizationRowOwner rowOwner, string id, string name,
        bool canDelete, bool enabled, bool toggleVisible)
    {
        owner = rowOwner;
        entryId = id;
        deletable = canDelete;

        if (nameText != null)
            nameText.text = name;

        if (deleteButton != null)
            deleteButton.SetActive(canDelete);

        if (toggleRoot != null)
            toggleRoot.SetActive(toggleVisible);

        toggle?.Initialize(enabled);

        EnsureSubscribed();
        RefreshClipState();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
        RefreshClipState();
    }

    private void Start()
    {
        // Spätes Retry: AudioLibrary.Instance ist zu diesem Zeitpunkt (nach
        // allen Awake()) garantiert gesetzt. Holt Rows nach, deren Setup /
        // OnEnable vor dem Library-Singleton lief.
        EnsureSubscribed();
        RefreshClipState();
    }

    private void OnDisable()
    {
        _subscribed = false;
        if (AudioLibrary.Instance != null)
            AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
    }

    /// <summary>
    /// Idempotent subscriben (abmelden zuerst): Deckt Rows ab, deren
    /// OnEnable lief, als die AudioLibrary noch nicht existierte.
    /// </summary>
    private void EnsureSubscribed()
    {
        if (AudioLibrary.Instance == null)
        {
            _subscribed = false;
            return;
        }

        if (_subscribed)
            return;

        AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
        AudioLibrary.Instance.OnLibraryChanged += HandleLibraryChanged;
        _subscribed = true;
    }

    public void OnClickRecorder()
    {
        if (owner != null)
            owner.OnRowOpenClips(entryId);
    }

    public void OnClickEdit()
    {
        if (owner != null)
            owner.OnRowEdit(entryId);
    }

    public void OnClickDelete()
    {
        if (owner != null && deletable)
            owner.OnRowDelete(entryId);
    }

    public void OnClickToggle()
    {
        if (owner == null || toggle == null)
            return;

        toggle.Toggle();
        owner.OnRowToggle(entryId, toggle.IsActive());
    }

    public void RefreshClipState()
    {
        EnsureSubscribed();

        if (!gameObject.activeInHierarchy)
            return;

        int count = 0;
        if (owner != null && !string.IsNullOrEmpty(entryId))
            count = owner.GetRowClipCount(entryId);

        if (countText != null)
            countText.text = $"Clips: {count}";

        if (clipsImage != null && ThemeManager.Instance != null)
        {
            clipsImage.color = ThemeManager.Instance.GetColor(count > 0
                ? ThemeColorRole.Accent1
                : ThemeColorRole.Background3);
        }
    }

    private void HandleLibraryChanged(AudioCategory category)
    {
        RefreshClipState();
    }
}
