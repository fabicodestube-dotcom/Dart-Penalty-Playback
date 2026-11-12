
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anbindung einer Customization-Liste (Sektoren, Penalties, Audio-Events
/// inkl. Score-Sammel-Row) an das einheitliche Listen-Item.
/// Die Page liefert Navigation, Toggle-Speicherung und Clip-Anzahl.
/// Das Item übernimmt Anzeige und Klicks.
/// </summary>
public interface ICustomizationRowOwner
{
    void OnRowEdit(string id);
    void OnRowDelete(string id);
    void OnRowOpenClips(string id);
    void OnRowToggle(string id, bool enabled);
    int GetRowClipCount(string id);

    /// <summary>Short English description of the entry's current settings.</summary>
    string GetRowInfo(string id);
}

/// <summary>
/// Einheitliche Zeile für alle Customization-Listen.
/// Aktualisiert Anzeige, Clip-Anzahl, Icons und Theme-Farben zentral.
/// </summary>
public class CustomizationListItem : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text infoText;
    public GameObject statusActiveText;
    public GameObject statusInactiveText;
    public TMP_Text countText;
    public GameObject imageGotClips;
    public GameObject imageNoClips;

    private ICustomizationRowOwner owner;
    private string entryId;
    private bool _active = true;

    private bool _subscribed;
    private bool _themeSubscribed;

    public void Setup(
        ICustomizationRowOwner rowOwner,
        string id,
        string name,
        bool enabled)
    {
        owner = rowOwner;
        entryId = id;
        _active = enabled;

        if (nameText != null)
            nameText.text = name;

        UpdateStatusObjects();

        EnsureSubscribed();
        EnsureThemeSubscribed();

        UpdateUI();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
        EnsureThemeSubscribed();

        UpdateUI();
    }

    private void Start()
    {
        // Erneuter Versuch, falls die AudioLibrary beim OnEnable
        // noch nicht initialisiert war.
        EnsureSubscribed();
        EnsureThemeSubscribed();

        UpdateUI();
    }

    private void OnDisable()
    {
        _subscribed = false;
        _themeSubscribed = false;

        if (AudioLibrary.Instance != null)
            AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;

        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnThemeChanged -= HandleThemeChanged;
    }

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

    private void EnsureThemeSubscribed()
    {
        if (ThemeManager.Instance == null)
        {
            _themeSubscribed = false;
            return;
        }

        if (_themeSubscribed)
            return;

        ThemeManager.Instance.OnThemeChanged -= HandleThemeChanged;
        ThemeManager.Instance.OnThemeChanged += HandleThemeChanged;

        _themeSubscribed = true;
    }

    public void OnClickRecorder()
    {
        owner?.OnRowOpenClips(entryId);
    }

    public void OnClickEdit()
    {
        owner?.OnRowEdit(entryId);
    }

    public void OnClickDelete()
    {
        owner?.OnRowDelete(entryId);
    }

    public void OnClickToggle(bool enabled)
    {
        owner?.OnRowToggle(entryId, enabled);
    }

    /// <summary>
    /// Aktualisiert die gesamte UI anhand des aktuellen Clip-Zustands.
    /// </summary>
    public void RefreshClipState()
    {
        EnsureSubscribed();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        int count = 0;

        if (owner != null && !string.IsNullOrEmpty(entryId))
            count = owner.GetRowClipCount(entryId);

        if (countText != null)
            countText.text = $"{count} Clips";

        if (infoText != null)
            infoText.text = owner?.GetRowInfo(entryId) ?? "";

        UpdateClipImages(count);
        ApplyClipColors(count);
    }

    /// <summary>
    /// Shows the active/inactive status object based on the entry state.
    /// Colors/localization are handled by the binders on those objects.
    /// </summary>
    private void UpdateStatusObjects()
    {
        if (statusActiveText != null)
            statusActiveText.SetActive(_active);

        if (statusInactiveText != null)
            statusInactiveText.SetActive(!_active);
    }

    private void UpdateClipImages(int count)
    {
        bool hasClips = count > 0;

        if (imageGotClips != null)
            imageGotClips.SetActive(hasClips);

        if (imageNoClips != null)
            imageNoClips.SetActive(!hasClips);
    }

    private void HandleLibraryChanged(AudioCategory category)
    {
        UpdateUI();
    }

    private void HandleThemeChanged()
    {
        UpdateUI();
    }

    /// <summary>
    /// Färbt ausschließlich countText und das jeweils aktive Clip-Icon.
    /// nameText behält seine konfigurierte Farbe.
    /// </summary>
    private void ApplyClipColors(int count)
    {
        if (ThemeManager.Instance == null)
            return;

        bool hasClips = count > 0;

        if (countText != null)
        {
            countText.color = ThemeManager.Instance.GetColor(
                hasClips
                    ? ThemeColorRole.TextOnAccent1
                    : ThemeColorRole.Text2);
        }

        // GameObject activeImage = hasClips
        //     ? imageGotClips
        //     : imageNoClips;

        // if (activeImage == null)
        //     return;

        // Image image = activeImage.GetComponent<Image>();

        // if (image == null)
        //     return;

        // image.color = ThemeManager.Instance.GetColor(
        //     hasClips
        //         ? ThemeColorRole.Accent1
        //         : ThemeColorRole.Background3);
    }
}
