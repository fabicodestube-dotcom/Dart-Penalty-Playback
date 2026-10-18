using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Score-Clip-Grid (0–180) als eigenes Popup/Seite: Wird über die
/// Score-Sammel-Row der AudioEventsPage geöffnet. Buttons pro Row: 5
/// (HorizontalLayoutGroup-Rows in einem VerticalLayoutGroup-Parent,
/// volle Breite, auflösungsunabhängig).
/// Jeder Button zeigt die Clip-Anzahl und öffnet den ClipBrowser.
/// Verdrahtung (Szene, macht der UI-Umbau): scoresParent + buttonPrefab
/// (AudioClipButton-Prefab mit CustomClipButton) + windowHandler +
/// clipBrowser zuweisen.
/// </summary>
public class ScoreClipGridHandler : MonoBehaviour
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Clip Browser")]
    public ClipBrowser clipBrowser;

    [Header("Grid")]
    public Transform scoresParent;
    public GameObject buttonPrefab;

    [Header("Layout (5 pro Row, volle Breite)")]
    public int buttonsPerRow = 5;
    public float rowButtonSpacing = 20f;
    public float rowMinHeight = 64f;

    public int ScoreMin = 0;
    public int ScoreMax = 180;

    private readonly List<CustomClipButton> buttons = new List<CustomClipButton>();
    private bool scoresBuilt = false;

    private void Awake()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowser>();
    }

    private void OnEnable()
    {
        EnsureSubscribed();

        // Während versteckt ggf. verpasst: Counts auffrischen.
        RefreshCounts();
    }

    private void OnDisable()
    {
        if (AudioLibrary.Instance != null)
            AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
    }

    /// <summary>Idempotent subscriben (Doppel-Subscription vermeiden).</summary>
    private void EnsureSubscribed()
    {
        if (AudioLibrary.Instance == null)
            return;

        AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
        AudioLibrary.Instance.OnLibraryChanged += HandleLibraryChanged;
    }

    private void Start()
    {
        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            EnsureScoresBuilt();
        }
        else
        {
            StartCoroutine(WaitForLibraryLoad());
        }
    }

    /// <summary>Grid einblenden (Page ruft auf): baut einmalig, frischt Counts auf.</summary>
    public void Show()
    {
        EnsureScoresBuilt();
        RefreshCounts();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void OpenScoreClips(int score)
    {
        if (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
            return;

        List<AudioClip> clips = AudioLibrary.Instance.GetRuntimeScoreList(score);

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowser>();
        if (windowHandler == null || clipBrowser == null)
            return;

        windowHandler.GoTo(ScreenId.CustomClipBrowser);
        clipBrowser.SetScoreClips(score, clips);
    }

    public void RefreshCounts()
    {
        foreach (var button in buttons)
        {
            if (button != null)
                button.Refresh();
        }
    }

    private void EnsureScoresBuilt()
    {
        if (buttonPrefab == null || scoresParent == null)
            return;

        if (scoresBuilt && scoresParent.childCount > 0)
        {
            RefreshCounts();
            return;
        }

        scoresBuilt = false;
        buttons.RemoveAll(b => b == null);

        BeginGrid(scoresParent);
        for (int score = ScoreMin; score <= ScoreMax; score++)
        {
            var button = AddGridButton();
            if (button == null)
                continue;
            button.clipNav = null;
            button.scoreGridHandler = this;
            button.SetupScore(score, score.ToString());
        }
        FinishGridRow();

        scoresBuilt = true;
    }

    private void HandleLibraryChanged(AudioCategory category)
    {
        RefreshCounts();
    }

    private IEnumerator WaitForLibraryLoad()
    {
        while (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
            yield return null;

        EnsureSubscribed();
        EnsureScoresBuilt();
    }

    // =========================
    // ROW-CHUNKING (5 pro Row, volle Breite → auflösungsunabhängig)
    // =========================

    private Transform gridParent;
    private GameObject gridRow;
    private int gridRowFill;
    private int gridRowIndex;

    private void BeginGrid(Transform parent)
    {
        FinishGridRow();

        gridParent = parent;
        gridRow = null;
        gridRowFill = 0;
        gridRowIndex = 0;

        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        EnsureGridParent(parent);
    }

    private void EnsureGridParent(Transform parent)
    {
        if (parent.GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = rowButtonSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        if (parent.GetComponent<ContentSizeFitter>() == null)
        {
            var fitter = parent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private CustomClipButton AddGridButton()
    {
        if (gridParent == null || buttonPrefab == null)
            return null;

        if (gridRow == null || gridRowFill >= Mathf.Max(1, buttonsPerRow))
        {
            gridRow = CreateRow(gridParent, gridRowIndex++);
            gridRowFill = 0;
        }

        var go = Instantiate(buttonPrefab, gridRow.transform, false);
        var button = go.GetComponent<CustomClipButton>();

        if (button == null)
        {
            Debug.LogError("ScoreClipGridHandler: buttonPrefab ohne CustomClipButton.");
            Destroy(go);
            return null;
        }

        buttons.Add(button);
        gridRowFill++;
        return button;
    }

    private void FinishGridRow()
    {
        if (gridRow == null)
            return;

        int perRow = Mathf.Max(1, buttonsPerRow);
        int missing = perRow - (gridRowFill % perRow);

        if (gridRowFill <= 0 || missing >= perRow)
            return;

        for (int i = 0; i < missing; i++)
        {
            var placeholder = new GameObject($"Placeholder_{gridRowFill + i}", typeof(RectTransform));
            placeholder.transform.SetParent(gridRow.transform, false);

            var layout = placeholder.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.minHeight = 0f;
            layout.preferredWidth = 0f;
            layout.preferredHeight = 0f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
        }

        gridRowFill = perRow;
    }

    private GameObject CreateRow(Transform parent, int index)
    {
        var go = new GameObject($"Row_{index}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = rowButtonSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childScaleWidth = false;
        hlg.childScaleHeight = false;

        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = rowMinHeight;

        return go;
    }
}
