using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomClipHandler : MonoBehaviour
{
    [Header("Window Handler")]
    public WindowHandler windowHandler;

    [Header("Clip Browser")]
    public ClipBrowser clipBrowser;

    [Header("Score Buttons")]
    public List<RecorderScoreButton> scoreButtons;

    [Header("Penalty Buttons")]
    public RecorderPenaltyButton penaltyButtonWall;
    public RecorderPenaltyButton penaltyButtonCeiling;
    public RecorderPenaltyButton penaltyButtonAllMiss;
    public RecorderPenaltyButton penaltyButtonTripleOnes;
    public RecorderPenaltyButton penaltyButtonSchnapszahl;
    public RecorderPenaltyButton penaltyButtonLostGame;

    [Header("Special Buttons")]
    public RecorderSpecialButton specialButtonStartGame;
    public RecorderSpecialButton specialButtonUndo;
    public RecorderSpecialButton specialButtonBust;
    public RecorderSpecialButton specialButton420;
    public RecorderSpecialButton specialButtonSuddenDeath;
    public RecorderSpecialButton specialButtonMatchWon;
    public RecorderSpecialButton specialButtonMarks;
    public RecorderSpecialButton specialButtonHits;
    public RecorderSpecialButton specialButtonATCStreak;

    private void Start()
    {
        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            InitializeAll();
            RegisterEvents();
        }
        else
        {
            StartCoroutine(WaitForLibraryLoad());
        }
    }

    public void OnGoBackClicked()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (windowHandler != null)
        {
            windowHandler.GoBack();
        }
        else
        {
            Debug.LogError("Kein WindowHandler in der Szene gefunden.");
        }
    }

    public void ClosePopup()
    {
        windowHandler.HidePopup();
    }

    public void OpenScoreClips(int score)
    {
        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            List<AudioClip> clips = AudioLibrary.Instance.GetRuntimeScoreList(score);

            windowHandler.GoTo(ScreenId.CustomClipBrowser);
            clipBrowser.SetScoreClips(score, clips);
        }
    }

    public void OpenPenaltyClips(PenaltyType type)
    {
        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            List<AudioClip> clips = AudioLibrary.Instance.GetRuntimePenaltyList(type);

            windowHandler.GoTo(ScreenId.CustomClipBrowser);
            clipBrowser.SetPenaltyClips(type, clips);
        }
    }

    public void OpenSpecialClips(SpecialAudioType type)
    {
        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            List<AudioClip> clips = AudioLibrary.Instance.GetRuntimeSpecialList(type);

            windowHandler.GoTo(ScreenId.CustomClipBrowser);
            clipBrowser.SetSpecialClips(type, clips);
        }
    }

    private void OnEnable()
    {
        RegisterEvents();
    }

    private void OnDisable()
    {
        UnregisterEvents();
    }

    private void InitializeScoreButtons()
    {
        if (scoreButtons.Count != 181)
            return;

        for (int i = 0; i < 181; i++)
        {
            int count = AudioLibrary.Instance.GetRuntimeScoreList(i)?.Count ?? 0;
            scoreButtons[i].UpdateView(count);
        }
    }

    private void InitializePenaltyButtons()
    {
        penaltyButtonWall.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.Wall)?.Count ?? 0);

        penaltyButtonCeiling.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.Ceiling)?.Count ?? 0);

        penaltyButtonAllMiss.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.AllMiss)?.Count ?? 0);

        penaltyButtonTripleOnes.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.ThreeOnes)?.Count ?? 0);

        penaltyButtonSchnapszahl.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.Schnapszahl)?.Count ?? 0);

        penaltyButtonLostGame.UpdateView(
            AudioLibrary.Instance.GetRuntimePenaltyList(PenaltyType.LostGame)?.Count ?? 0);
    }

    private void InitializeSpecialButtons()
    {
        specialButtonStartGame.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.StartGame)?.Count ?? 0);

        specialButtonUndo.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.Undo)?.Count ?? 0);

        specialButtonBust.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.Bust)?.Count ?? 0);

        specialButton420.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.FourTwenty)?.Count ?? 0);

        specialButtonSuddenDeath.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.SuddenDeath)?.Count ?? 0);

        specialButtonMatchWon.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.MatchWon)?.Count ?? 0);

        specialButtonHits.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.Hits)?.Count ?? 0);

        specialButtonMarks.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.Marks)?.Count ?? 0);

        specialButtonATCStreak.UpdateView(
            AudioLibrary.Instance.GetRuntimeSpecialList(SpecialAudioType.ATCStreak)?.Count ?? 0);
    }

    private void InitializeAll()
    {
        InitializeScoreButtons();
        InitializePenaltyButtons();
        InitializeSpecialButtons();
    }

    private void HandleLibraryChanged(AudioCategory category)
    {
        switch (category)
        {
            case AudioCategory.Score:
                InitializeScoreButtons();
                break;
            case AudioCategory.Penalty:
                InitializePenaltyButtons();
                break;
            case AudioCategory.Special:
                InitializeSpecialButtons();
                break;
        }
    }

    private System.Collections.IEnumerator WaitForLibraryLoad()
    {
        Debug.Log("Warte auf AudioLibrary, um CustomClipHandler zu initialisieren...");
        // wartet bis AudioLibrary existiert UND ready ist
        while (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
        {
            yield return null; // wartet 1 Frame
        }

        if (AudioLibrary.Instance != null)
        {
            Debug.Log("AudioLibrary ist jetzt bereit, abonniere Events im CustomClipHandler...");
            AudioLibrary.Instance.OnLibraryChanged += HandleLibraryChanged;
        }
            
        InitializeAll();
    }

    private void RegisterEvents()
    {
        if (AudioLibrary.Instance != null)
        {
            AudioLibrary.Instance.OnLibraryChanged += HandleLibraryChanged;
        }
        else
        {
            Debug.LogWarning("CustomClipHandler konnte AudioLibrary.Instance nicht finden, daher werden keine Events abonniert.");
        }
    }

    private void UnregisterEvents()
    {
        if (AudioLibrary.Instance != null)
        {
            AudioLibrary.Instance.OnLibraryChanged -= HandleLibraryChanged;
        }
        else
        {
            Debug.LogWarning("CustomClipHandler konnte AudioLibrary.Instance nicht finden, daher werden keine Events deabonniert.");
        }
    }
}
