using UnityEngine;

/// <summary>
/// Coordinator of the Customization Screen (BottomPanel button):
/// Swipe riders (sectors, penalties, audio events).
/// to the pages and triggers refresh after def changes.
/// Clips are recorded directly via the rows (not their own tab).
/// Implements IUIScreen (OnShow/OnHide via WindowHandler).
/// </summary>
public class CustomizationScreen : MonoBehaviour, IUIScreen
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

        // Race Library Load vs. UI Building: Was the list opened before
        // the clips were loaded (counts 0), comes here the
        // guaranteed second refresh to load end – even if the
        // Load-end events were missed (no one subscribed or similar).
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

    /// <summary>Wait for finished library load, then rebuild lists.</summary>
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

    /// <summary>Idempotent Subscribe to the AudioLibrary Change Events.</summary>
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

    /// <summary>Responds to AudioLibrary changes: re-read all clip counts.</summary>
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

    /// <summary>Active refresh of all clip counts (e.g. after AudioLibrary load).</summary>
    public void RefreshCustomizationCounts()
    {
        RefreshAll();
    }

    private void HandleStoreChanged(CustomizationChangeKind kind)
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Targeted instead of RefreshAll: A penalty save only builds the
        // Penalty list new (~25 rows), not all 4 pages (~400 objects).
        switch (kind)
        {
            case CustomizationChangeKind.Penalties:
                penaltiesPage?.Refresh();
                break;
            case CustomizationChangeKind.Sectors:
                // Sector Deletion Can Cascade Penalties → With Fresh Up.
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
