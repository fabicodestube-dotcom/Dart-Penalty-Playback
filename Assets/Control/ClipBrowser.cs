using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.IO;
using System.Collections;
using UnityEngine.EventSystems;

public class ClipBrowser : MonoBehaviour
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("UI")]
    public TMP_Text headline;
    public TMP_Text clipCountText;

    [Header("Clip Info")]
    public TMP_Text clipNameText;
    public TMP_Text clipLengthText;

    [TextArea]
    public string noClipText = "No clip available";

    [Header("Navigation Buttons")]
    public CanvasGroup previousButtonGroup;
    public CanvasGroup nextButtonGroup;
    public CanvasGroup playButtonGroup;

    [Header("Play Button Icons")]
    public GameObject playIcon;
    public GameObject pauseIcon;

    [Header("Visualization")]
    public ClipVisualization waveform;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Popups")]
    public UIScreen popupDeleteClip;

    // =========================
    // CUT MODE UI
    // =========================
    [Header("Cut Mode UI")]
    public GameObject cutOverlay;
    public RectTransform cutLeftHandle;
    public RectTransform cutRightHandle;
    public RectTransform cutWaveformRect;

    private bool cutMode;
    private bool draggingLeft;
    private bool draggingRight;

    private float cutStart01 = 0f;
    private float cutEnd01 = 1f;

    private Vector2 lastPointerLocalPos;

    private List<AudioClip> clips = new();
    private int currentIndex = 0;

    [Header("Internal State")]
    private AudioCategory currentCategory;
    private int currentScore;
    private PenaltyType currentPenaltyType;
    private SpecialAudioType currentSpecialType;
    private Coroutine playStateRoutine;
    private bool isLocked;

    private Coroutine refreshRoutine;

    public bool IsInCutMode => cutMode;

    private void Start()
    {
        if (windowHandler == null)
        {
            windowHandler = FindAnyObjectByType<WindowHandler>();
        }

        if (cutOverlay != null)
            cutOverlay.SetActive(false);

        UpdateButtonState();
        UpdateClipInfo(null);
        UpdatePlayIconState();
    }

    // =========================
    // CUT MODE
    // =========================

    public void EnterCutMode()
    {
        if (clips.Count == 0) return;

        cutMode = true;

        if (cutOverlay != null)
            cutOverlay.SetActive(true);

        if (waveform != null)
            waveform.showPlayhead = false;

        Stop();

        cutStart01 = 0f;
        cutEnd01 = 1f;

        UpdateCutHandles();
    }

    public void CancelCut()
    {
        cutMode = false;

        if (cutOverlay != null)
            cutOverlay.SetActive(false);

        if (waveform != null)
            waveform.showPlayhead = true;
    }

    public void ConfirmCut()
    {
        if (clips.Count == 0) return;

        AudioClip source = GetCurrentClip();
        if (source == null) return;

        int startSample = Mathf.Clamp((int)(cutStart01 * source.samples), 0, source.samples);
        int endSample = Mathf.Clamp((int)(cutEnd01 * source.samples), 0, source.samples);

        if (endSample <= startSample)
            return;

        int channels = source.channels;
        int sampleCount = (endSample - startSample) * channels;

        float[] data = new float[sampleCount];
        source.GetData(data, startSample);

        AudioClip trimmed = AudioClip.Create(
            source.name + "_cut",
            endSample - startSample,
            channels,
            source.frequency,
            false
        );

        trimmed.SetData(data, 0);

        switch (currentCategory)
        {
            case AudioCategory.Score:
                AudioLibrary.Instance.ReplaceScoreClip(currentScore, currentIndex, trimmed);
                //Debug.Log("Fehlt noch: Speichern des zugeschnittenen Clips in der AudioLibrary.");
                break;

            case AudioCategory.Penalty:
                AudioLibrary.Instance.ReplacePenaltyClip(currentPenaltyType, currentIndex, trimmed);
                //Debug.Log("Fehlt noch: Speichern des zugeschnittenen Clips in der AudioLibrary.");
                break;

            case AudioCategory.Special:
                AudioLibrary.Instance.ReplaceSpecialClip(currentSpecialType, currentIndex, trimmed);
                //Debug.Log("Fehlt noch: Speichern des zugeschnittenen Clips in der AudioLibrary.");
                break;
        }

        CancelCut();
        ReloadCurrentList();
        RefreshView();
    }

    public void ExitMenu()
    {
        CancelCut();
        windowHandler.GoBack();
    }

    // =========================
    // HANDLE DRAG (UI EVENTS)
    // =========================

    public void BeginDragLeft(BaseEventData data)
    {
        draggingLeft = true;
    }

    public void BeginDragRight(BaseEventData data)
    {
        draggingRight = true;
    }

    public void EndDrag()
    {
        draggingLeft = false;
        draggingRight = false;
    }

    public void OnDrag(BaseEventData data)
    {
        if (!cutMode) return;

        PointerEventData ped = (PointerEventData)data;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cutWaveformRect, ped.position, ped.pressEventCamera, out Vector2 local))
            return;

        float width = cutWaveformRect.rect.width;
        float x01 = Mathf.Clamp01((local.x - cutWaveformRect.rect.xMin) / width);

        if (draggingLeft)
        {
            cutStart01 = Mathf.Min(x01, cutEnd01);
        }
        else if (draggingRight)
        {
            cutEnd01 = Mathf.Max(x01, cutStart01);
        }

        UpdateCutHandles();
    }

    private void UpdateCutHandles()
    {
        if (cutWaveformRect == null) return;

        float width = cutWaveformRect.rect.width;

        if (cutLeftHandle != null)
            cutLeftHandle.anchoredPosition = new Vector2(cutStart01 * width, cutLeftHandle.anchoredPosition.y);

        if (cutRightHandle != null)
            cutRightHandle.anchoredPosition = new Vector2(cutEnd01 * width, cutRightHandle.anchoredPosition.y);
    }

    // =========================
    // CLIPS SETZEN
    // =========================

    public void SetScoreClips(int score, List<AudioClip> newClips)
    {
        currentCategory = AudioCategory.Score;
        currentScore = score;

        SetClipsInternal(newClips);
    }

    public void SetPenaltyClips(PenaltyType type, List<AudioClip> newClips)
    {
        currentCategory = AudioCategory.Penalty;
        currentPenaltyType = type;

        SetClipsInternal(newClips);
    }

    public void SetSpecialClips(SpecialAudioType type, List<AudioClip> newClips)
    {
        currentCategory = AudioCategory.Special;
        currentSpecialType = type;

        SetClipsInternal(newClips);
    }

    private void SetClipsInternal(List<AudioClip> newClips)
    {
        clips = newClips ?? new List<AudioClip>();

        currentIndex = 0;

        UpdateHeadline();
        UpdateButtonState();

        if (clips.Count == 0)
        {
            clipCountText.text = "0/0";

            waveform.GenerateWaveform(null);

            UpdateClipInfo(null);

            return;
        }

        RefreshView();
    }

    // =========================
    // NAVIGATION
    // =========================

    public void NextClip()
    {
        if (isLocked || cutMode)
            return;

        if (clips.Count == 0)
            return;

        currentIndex++;

        if (currentIndex >= clips.Count)
            currentIndex = 0;

        RefreshView();
    }

    public void PreviousClip()
    {
        if (isLocked || cutMode)
            return;

        if (clips.Count == 0)
        {
            Debug.Log("No clips available.");
            return;
        }

        currentIndex--;

        if (currentIndex < 0)
            currentIndex = clips.Count - 1;

        RefreshView();
    }

    // =========================
    // AUDIO CONTROL
    // =========================

    public void Play()
    {
        if (isLocked)
            return;

        AudioClip clip = GetCurrentClip();

        if (clip == null)
            return;

        if (audioSource.clip != clip)
            audioSource.clip = clip;

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            UpdatePlayIconState();
            return;
        }

        audioSource.Play();
        UpdatePlayIconState();

        if (playStateRoutine != null)
            StopCoroutine(playStateRoutine);

        playStateRoutine = StartCoroutine(WatchPlayback(clip));
    }

    private IEnumerator WatchPlayback(AudioClip clip)
    {
        yield return new WaitUntil(() =>
            audioSource == null ||
            !audioSource.isPlaying
        );

        UpdatePlayIconState();
        playStateRoutine = null;
    }

    public void Pause()
    {
        audioSource.Pause();

        if (playStateRoutine != null)
            StopCoroutine(playStateRoutine);

        playStateRoutine = null;

        UpdatePlayIconState();
    }

    private void Stop()
    {
        audioSource.Stop();

        if (playStateRoutine != null)
            StopCoroutine(playStateRoutine);

        playStateRoutine = null;

        UpdatePlayIconState();
    }

    // =========================
    // CLIP MANAGEMENT
    // =========================

    public void DeleteCurrentClip()
    {
        if (clips.Count == 0)
            return;

        windowHandler.ShowPopup(popupDeleteClip);
    }

    public void CancelDelete()
    {
        windowHandler.HidePopup();
    }

    public void ConfirmDeleteCurrentClip()
    {
        if (clips.Count == 0)
            return;

        Stop();

        bool deleted = false;

        switch (currentCategory)
        {
            case AudioCategory.Score:
                deleted = AudioLibrary.Instance.DeleteScoreClip(currentScore, currentIndex);
                break;

            case AudioCategory.Penalty:
                deleted = AudioLibrary.Instance.DeletePenaltyClip(currentPenaltyType, currentIndex);
                break;

            case AudioCategory.Special:
                deleted = AudioLibrary.Instance.DeleteSpecialClip(currentSpecialType, currentIndex);
                break;
        }

        windowHandler.HidePopup();

        if (!deleted)
        {
            Debug.LogWarning("ClipBrowser: Löschen des Clips in AudioLibrary fehlgeschlagen.");
            return;
        }

        ReloadCurrentList();

        if (currentIndex >= clips.Count)
            currentIndex = clips.Count - 1;

        UpdateButtonState();

        if (clips.Count <= 0)
        {
            clipCountText.text = "0/0";

            waveform.GenerateWaveform(null);

            UpdateClipInfo(null);

            return;
        }

        RefreshView();
    }

    public bool HasClips()
    {
        return clips.Count > 0;
    }

    public void AddClip(AudioClip clip)
    {
        if (clip == null)
            return;

        switch (currentCategory)
        {
            case AudioCategory.Score:
                AudioLibrary.Instance.AddScoreClip(currentScore, clip);
                break;

            case AudioCategory.Penalty:
                AudioLibrary.Instance.AddPenaltyClip(currentPenaltyType, clip);
                break;

            case AudioCategory.Special:
                AudioLibrary.Instance.AddSpecialClip(currentSpecialType, clip);
                break;
        }

        ReloadCurrentList();

        currentIndex = clips.Count - 1;

        UpdateButtonState();

        RefreshView();

        PlayNewClipOnce();
    }

    private void PlayNewClipOnce()
    {
        AudioClip clip = GetCurrentClip();

        if (clip == null)
            return;

        Stop();

        audioSource.clip = clip;
        audioSource.Play();

        UpdatePlayIconState();

        if (playStateRoutine != null)
            StopCoroutine(playStateRoutine);

        playStateRoutine = StartCoroutine(WatchPlayback(clip));
    }

    private void ReloadCurrentList()
    {
        switch (currentCategory)
        {
            case AudioCategory.Score:
                clips = AudioLibrary.Instance.GetRuntimeScoreList(currentScore);
                break;

            case AudioCategory.Penalty:
                clips = AudioLibrary.Instance.GetRuntimePenaltyList(currentPenaltyType);
                break;

            case AudioCategory.Special:
                clips = AudioLibrary.Instance.GetRuntimeSpecialList(currentSpecialType);
                break;
        }

        clips ??= new List<AudioClip>();
    }

    // =========================
    // INTERNAL
    // =========================

    private void RefreshView()
    {
        Stop();

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(RefreshViewDeferred());
    }

    private IEnumerator RefreshViewDeferred()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        AudioClip clip = GetCurrentClip();

        if (clip == null)
            yield break;

        UpdateHeadline();

        clipCountText.text = $"{currentIndex + 1}/{clips.Count}";

        waveform.GenerateWaveform(clip);

        UpdateClipInfo(clip);

        UpdateButtonState();

        refreshRoutine = null;
    }

    private void UpdatePlayIconState()
    {
        bool isPlaying =
            audioSource != null &&
            audioSource.isPlaying;

        if (playIcon != null)
            playIcon.SetActive(!isPlaying);

        if (pauseIcon != null)
            pauseIcon.SetActive(isPlaying);
    }

    private AudioClip GetCurrentClip()
    {
        if (clips.Count == 0)
            return null;

        return clips[currentIndex];
    }

    private void UpdateHeadline()
    {
        string categoryText = currentCategory.ToString();
        string specification = "";

        switch (currentCategory)
        {
            case AudioCategory.Score:
                specification = currentScore.ToString();
                break;

            case AudioCategory.Penalty:
                specification = currentPenaltyType.ToString();
                break;

            case AudioCategory.Special:
                specification = currentSpecialType.ToString();
                break;
        }

        headline.text = $"{categoryText} ({specification})";
    }

    private void UpdateButtonState()
    {
        bool hasClips = clips.Count > 0;

        SetCanvasGroup(previousButtonGroup, hasClips);
        SetCanvasGroup(nextButtonGroup, hasClips);
        SetCanvasGroup(playButtonGroup, hasClips);
    }

    private void SetCanvasGroup(CanvasGroup group, bool enabled)
    {
        if (group == null)
            return;

        group.alpha = enabled ? 1f : 0.4f;
        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    private void UpdateClipInfo(AudioClip clip)
    {
        if (clip == null)
        {
            clipNameText.text = noClipText;
            clipLengthText.text = "--:--";
            return;
        }

        string categoryPart = currentCategory.ToString();
        string specPart = "";

        switch (currentCategory)
        {
            case AudioCategory.Score:
                specPart = currentScore.ToString();
                break;

            case AudioCategory.Penalty:
                specPart = currentPenaltyType.ToString();
                break;

            case AudioCategory.Special:
                specPart = currentSpecialType.ToString();
                break;
        }

        int displayIndex = currentIndex + 1;

        clipNameText.text = $"{categoryPart} ({specPart}) - Clip {displayIndex}";

        int totalSeconds = Mathf.RoundToInt(clip.length);
        clipLengthText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;

        SetCanvasGroup(previousButtonGroup, !locked && clips.Count > 0);
        SetCanvasGroup(nextButtonGroup, !locked && clips.Count > 0);
        SetCanvasGroup(playButtonGroup, !locked && clips.Count > 0);
    }
}


// using System.Collections.Generic;
// using TMPro;
// using UnityEngine;
// using System.IO;
// using System.Collections;

// public class ClipBrowser : MonoBehaviour
// {
//     [Header("Control")]
//     public WindowHandler windowHandler;

//     [Header("UI")]
//     public TMP_Text headline;
//     public TMP_Text clipCountText;

//     [Header("Clip Info")]
//     public TMP_Text clipNameText;
//     public TMP_Text clipLengthText;

//     [TextArea]
//     public string noClipText = "Kein Clip vorhanden";

//     [Header("Navigation Buttons")]
//     public CanvasGroup previousButtonGroup;
//     public CanvasGroup nextButtonGroup;
//     public CanvasGroup playButtonGroup;

//     [Header("Play Button Icons")]
//     public GameObject playIcon;
//     public GameObject pauseIcon;

//     [Header("Visualization")]
//     public ClipVisualization waveform;

//     [Header("Audio")]
//     public AudioSource audioSource;

//     [Header("Popups")]
//     public UIScreen popupDeleteClip;

//     private List<AudioClip> clips = new();
//     private int currentIndex = 0;

//     [Header("Internal State")]
//     private AudioCategory currentCategory;
//     private int currentScore;
//     private PenaltyType currentPenaltyType;
//     private SpecialAudioType currentSpecialType;
//     private Coroutine playStateRoutine;
//     private bool isLocked;

//     private Coroutine refreshRoutine;

//     private void Start()
//     {
//         if (windowHandler == null)
//         {
//             windowHandler = FindAnyObjectByType<WindowHandler>();
//         }

//         UpdateButtonState();
//         UpdateClipInfo(null);
//         UpdatePlayIconState();
//     }

//     // =========================
//     // CLIPS SETZEN
//     // =========================

//     public void SetScoreClips(int score, List<AudioClip> newClips)
//     {
//         currentCategory = AudioCategory.Score;
//         currentScore = score;

//         SetClipsInternal(newClips);
//     }

//     public void SetPenaltyClips(PenaltyType type, List<AudioClip> newClips)
//     {
//         currentCategory = AudioCategory.Penalty;
//         currentPenaltyType = type;

//         SetClipsInternal(newClips);
//     }

//     public void SetSpecialClips(SpecialAudioType type, List<AudioClip> newClips)
//     {
//         currentCategory = AudioCategory.Special;
//         currentSpecialType = type;

//         SetClipsInternal(newClips);
//     }

//     private void SetClipsInternal(List<AudioClip> newClips)
//     {
//         clips = newClips ?? new List<AudioClip>();

//         currentIndex = 0;

//         UpdateHeadline();
//         UpdateButtonState();

//         if (clips.Count == 0)
//         {
//             clipCountText.text = "0/0";

//             waveform.GenerateWaveform(null);

//             UpdateClipInfo(null);

//             return;
//         }

//         RefreshView();
//     }

//     // =========================
//     // NAVIGATION
//     // =========================

//     public void NextClip()
//     {
//         if (isLocked)
//             return;
//         if (clips.Count == 0)
//             return;

//         currentIndex++;

//         if (currentIndex >= clips.Count)
//             currentIndex = 0;

//         RefreshView();
//     }

//     public void PreviousClip()
//     {       
//         if (isLocked)
//             return;
//         if (clips.Count == 0)
//         {
//             Debug.Log("No clips available.");
//             return;
//         }

//         currentIndex--;

//         if (currentIndex < 0)
//             currentIndex = clips.Count - 1;

//         RefreshView();
//     }

//     // =========================
//     // AUDIO CONTROL
//     // =========================

//     public void Play()
//     {
//         if (isLocked)
//             return;
        
//         AudioClip clip = GetCurrentClip();

//         if (clip == null)
//             return;

//         if (audioSource.clip != clip)
//             audioSource.clip = clip;

//         if (audioSource.isPlaying)
//         {
//             audioSource.Pause();
//             UpdatePlayIconState();
//             return;
//         }

//         audioSource.Play();
//         UpdatePlayIconState();

//         if (playStateRoutine != null)
//             StopCoroutine(playStateRoutine);

//         playStateRoutine = StartCoroutine(WatchPlayback(clip));
//     }

//     private IEnumerator WatchPlayback(AudioClip clip)
//     {
//         // Warten bis Audio wirklich fertig ist
//         yield return new WaitUntil(() =>
//             audioSource == null ||
//             !audioSource.isPlaying
//         );

//         UpdatePlayIconState();
//         playStateRoutine = null;
//     }

//     public void Pause()
//     {
//         audioSource.Pause();

//         if (playStateRoutine != null)
//             StopCoroutine(playStateRoutine);

//         playStateRoutine = null;

//         UpdatePlayIconState();
//     }

//     private void Stop()
//     {
//         audioSource.Stop();

//         if (playStateRoutine != null)
//             StopCoroutine(playStateRoutine);

//         playStateRoutine = null;

//         UpdatePlayIconState();
//     }

//     // =========================
//     // CLIP MANAGEMENT
//     // =========================

//     public void DeleteCurrentClip()
//     {
//         if (clips.Count == 0)
//             return;

//         windowHandler.ShowPopup(popupDeleteClip);
//     }

//     public void CancelDelete()
//     {
//         windowHandler.HidePopup();
//     }

//     public void ConfirmDeleteCurrentClip()
//     {
//         if (clips.Count == 0)
//             return;

//         Stop();

//         bool deleted = false;

//         switch (currentCategory)
//         {
//             case AudioCategory.Score:
//                 deleted = AudioLibrary.Instance.DeleteScoreClip(currentScore, currentIndex);
//                 break;

//             case AudioCategory.Penalty:
//                 deleted = AudioLibrary.Instance.DeletePenaltyClip(currentPenaltyType, currentIndex);
//                 break;

//             case AudioCategory.Special:
//                 deleted = AudioLibrary.Instance.DeleteSpecialClip(currentSpecialType, currentIndex);
//                 break;
//         }

//         windowHandler.HidePopup();

//         if (!deleted)
//         {
//             Debug.LogWarning("ClipBrowser: Löschen des Clips in AudioLibrary fehlgeschlagen.");
//             return;
//         }

//         ReloadCurrentList();

//         if (currentIndex >= clips.Count)
//             currentIndex = clips.Count - 1;

//         UpdateButtonState();

//         if (clips.Count <= 0)
//         {
//             clipCountText.text = "0/0";

//             waveform.GenerateWaveform(null);

//             UpdateClipInfo(null);

//             return;
//         }

//         RefreshView();
//     }

//     public bool HasClips()
//     {
//         return clips.Count > 0;
//     }

//     public void AddClip(AudioClip clip)
//     {
//         if (clip == null)
//             return;

//         switch (currentCategory)
//         {
//             case AudioCategory.Score:
//                 AudioLibrary.Instance.AddScoreClip(currentScore, clip);
//                 break;

//             case AudioCategory.Penalty:
//                 AudioLibrary.Instance.AddPenaltyClip(currentPenaltyType, clip);
//                 break;

//             case AudioCategory.Special:
//                 AudioLibrary.Instance.AddSpecialClip(currentSpecialType, clip);
//                 break;
//         }

//         ReloadCurrentList();

//         currentIndex = clips.Count - 1;

//         UpdateButtonState();

//         RefreshView();

//         PlayNewClipOnce();
//     }

//     private void PlayNewClipOnce()
//     {
//         AudioClip clip = GetCurrentClip();

//         if (clip == null)
//             return;

//         Stop();

//         audioSource.clip = clip;
//         audioSource.Play();

//         UpdatePlayIconState();

//         if (playStateRoutine != null)
//             StopCoroutine(playStateRoutine);

//         playStateRoutine = StartCoroutine(WatchPlayback(clip));
//     }

//     private void ReloadCurrentList()
//     {
//         switch (currentCategory)
//         {
//             case AudioCategory.Score:
//                 clips = AudioLibrary.Instance.GetRuntimeScoreList(currentScore);
//                 break;

//             case AudioCategory.Penalty:
//                 clips = AudioLibrary.Instance.GetRuntimePenaltyList(currentPenaltyType);
//                 break;

//             case AudioCategory.Special:
//                 clips = AudioLibrary.Instance.GetRuntimeSpecialList(currentSpecialType);
//                 break;
//         }

//         clips ??= new List<AudioClip>();
//     }

//     // =========================
//     // INTERNAL
//     // =========================

//     private void RefreshView()
//     {
//         Stop();

//         if (refreshRoutine != null)
//             StopCoroutine(refreshRoutine);

//         refreshRoutine = StartCoroutine(RefreshViewDeferred());
//     }

//     private IEnumerator RefreshViewDeferred()
//     {
//         // WICHTIG: Layout zuerst finalisieren lassen
//         yield return null;
//         Canvas.ForceUpdateCanvases();

//         AudioClip clip = GetCurrentClip();

//         if (clip == null)
//             yield break;

//         UpdateHeadline();

//         clipCountText.text = $"{currentIndex + 1}/{clips.Count}";

//         waveform.GenerateWaveform(clip);

//         UpdateClipInfo(clip);

//         UpdateButtonState();

//         refreshRoutine = null;
//     }

//     private void UpdatePlayIconState()
//     {
//         bool isPlaying =
//             audioSource != null &&
//             audioSource.isPlaying;

//         if (playIcon != null)
//             playIcon.SetActive(!isPlaying);

//         if (pauseIcon != null)
//             pauseIcon.SetActive(isPlaying);
//     }

//     private AudioClip GetCurrentClip()
//     {
//         if (clips.Count == 0)
//             return null;

//         return clips[currentIndex];
//     }

//     private void UpdateHeadline()
//     {
//         string categoryText = currentCategory.ToString();
//         string specification = "";

//         switch (currentCategory)
//         {
//             case AudioCategory.Score:
//                 specification = currentScore.ToString();
//                 break;

//             case AudioCategory.Penalty:
//                 specification = currentPenaltyType.ToString();
//                 break;

//             case AudioCategory.Special:
//                 specification = currentSpecialType.ToString();
//                 break;
//         }

//         headline.text = $"{categoryText} ({specification})";
//     }

//     private void UpdateButtonState()
//     {
//         bool hasClips = clips.Count > 0;

//         SetCanvasGroup(previousButtonGroup, hasClips);
//         SetCanvasGroup(nextButtonGroup, hasClips);
//         SetCanvasGroup(playButtonGroup, hasClips);
//     }

//     private void SetCanvasGroup(CanvasGroup group, bool enabled)
//     {
//         if (group == null)
//             return;

//         group.alpha = enabled ? 1f : 0.4f;
//         group.interactable = enabled;
//         group.blocksRaycasts = enabled;
//     }

//     private void UpdateClipInfo(AudioClip clip)
//     {
//         if (clip == null)
//         {
//             clipNameText.text = noClipText;
//             clipLengthText.text = "--:--";
//             return;
//         }

//         string categoryPart = currentCategory.ToString();
//         string specPart = "";

//         switch (currentCategory)
//         {
//             case AudioCategory.Score:
//                 specPart = currentScore.ToString();
//                 break;

//             case AudioCategory.Penalty:
//                 specPart = currentPenaltyType.ToString();
//                 break;

//             case AudioCategory.Special:
//                 specPart = currentSpecialType.ToString();
//                 break;
//         }

//         int displayIndex = currentIndex + 1;   // exakt gleiche Logik wie clipCountText

//         clipNameText.text = $"{categoryPart} ({specPart}) - Clip {displayIndex}";

//         int totalSeconds = Mathf.RoundToInt(clip.length);
//         clipLengthText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
//     }

//     public void SetLocked(bool locked)
//     {
//         isLocked = locked;

//         SetCanvasGroup(previousButtonGroup, !locked && clips.Count > 0);
//         SetCanvasGroup(nextButtonGroup, !locked && clips.Count > 0);
//         SetCanvasGroup(playButtonGroup, !locked && clips.Count > 0);
//     }
// }
