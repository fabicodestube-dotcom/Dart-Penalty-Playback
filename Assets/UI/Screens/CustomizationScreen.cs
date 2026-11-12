using UnityEngine;

/// <summary>
/// Koordinator des Customization-Screens (BottomPanel-Button):
/// Swipe-Reiter (Sektoren, Penalties, Audio Events). Hält Referenzen
/// auf die Seiten und stößt Refresh nach Def-Änderungen an.
/// Clips werden direkt über die Rows aufgenommen (kein eigener Tab).
/// Implementiert IUIScreen (OnShow/OnHide via WindowHandler).
/// </summary>
public class CustomizationHandler : MonoBehaviour, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Seiten")]
    public CustomizationSectorsPage sectorsPage;
    public CustomizationPenaltiesPage penaltiesPage;
    public CustomizationAudioEventsPage audioEventsPage;

    private void OnEnable()
    {
        CustomizationStore.Changed += HandleStoreChanged;
        EnsureLibrarySubscribed();
    }

    private void Start()
    {
        EnsureLibrarySubscribed();
    }

    private void OnDisable()
    {
        CustomizationStore.Changed -= HandleStoreChanged;
        StopRefreshAfterLoad();
        if (AudioLibrary.Instance != null && _librarySubscribed)
        {
            AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
            _librarySubscribed = false;
        }
    }

    public void OnShow()
    {
        RefreshAll();

        // Race Library-Load vs. UI-Aufbau: Wurde die Liste geöffnet, bevor
        // die Clips fertig geladen waren (Counts 0), kommt hier der
        // garantierte Zweit-Refresh nach Load-Ende — auch wenn die
        // Load-End-Events verpasst wurden (niemand subscribed o.ä.).
        if (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
        {
            StopRefreshAfterLoad();
            refreshAfterLoad = StartCoroutine(RefreshAfterLibraryReady());
        }
    }

    public void OnHide()
    {
        StopRefreshAfterLoad();
    }

    /// <summary>Wartet auf fertigen Library-Load, dann Listen neu aufbauen.</summary>
    private Coroutine refreshAfterLoad;
    private bool _librarySubscribed;

    private System.Collections.IEnumerator RefreshAfterLibraryReady()
    {
        while (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
            yield return null;

        refreshAfterLoad = null;
        RefreshAll();
    }

    private void StopRefreshAfterLoad()
    {
        if (refreshAfterLoad != null)
        {
            StopCoroutine(refreshAfterLoad);
            refreshAfterLoad = null;
        }
    }

    /// <summary>Idempotent Abonnieren der AudioLibrary-Änderungs-Events.</summary>
    private void EnsureLibrarySubscribed()
    {
        if (AudioLibrary.Instance == null)
        {
            _librarySubscribed = false;
            return;
        }

        if (_librarySubscribed)
            return;

        AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
        AudioLibrary.Instance.OnLibraryChanged += HandleLibraryChanged;
        _librarySubscribed = true;
    }

    /// <summary>Reagiert auf AudioLibrary-Änderungen: alle Clip-Anzahlen neu einlesen.</summary>
    private void HandleLibraryChanged(AudioCategory category)
    {
        if (!gameObject.activeInHierarchy)
            return;

        RefreshCustomizationCounts();
    }

    public void RefreshAll()
    {
        sectorsPage?.Refresh();
        penaltiesPage?.Refresh();
        audioEventsPage?.Refresh();
        audioEventsPage?.RefreshScoreGridCounts();
    }

    /// <summary>Aktiver Refresh aller Clip-Anzahlen (z. B. nach AudioLibrary-Load).</summary>
    public void RefreshCustomizationCounts()
    {
        RefreshAll();
    }

    private void HandleStoreChanged(CustomizationChangeKind kind)
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Gezielt statt RefreshAll: Ein Penalty-Save baut nur die
        // Penalty-Liste neu (~25 Rows), nicht alle 4 Seiten (~400 Objekte).
        switch (kind)
        {
            case CustomizationChangeKind.Penalties:
                penaltiesPage?.Refresh();
                break;
            case CustomizationChangeKind.Sectors:
                // Sektor-Löschung kann Penalties kaskadieren → mit auffrischen.
                sectorsPage?.Refresh();
                penaltiesPage?.Refresh();
                break;
            case CustomizationChangeKind.AudioEvents:
                audioEventsPage?.Refresh();
                break;
            default:
                RefreshAll();
                break;
        }
    }
}
