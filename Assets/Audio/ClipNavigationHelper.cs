using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Öffnet den ClipBrowser für einen Custom-Key (Penalty-/Event-/Sektor-ID)
/// oder einen Score-Slot. Ersatz für die Open*-Methoden des entfallenen
/// 4. Tabs (CustomClipHandler): Die Customization-Rows rufen über ihre Page
/// hierher, statt über ein Button-Grid zu navigieren.
/// Verdrahtung in der Szene: WindowHandler + ClipBrowser zuweisen (Fallback:
/// FindAnyObjectByType). Pages halten eine Referenz (clipNav) und reichen
/// sie an ihre Rows weiter.
/// </summary>
public class ClipNavigationHelper : MonoBehaviour
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Clip Browser")]
    public ClipBrowser clipBrowser;

    private void Awake()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowser>();
    }

    public void OpenCustomClips(string customKey, string label)
    {
        if (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
            return;

        List<AudioClip> clips = AudioLibrary.Instance.GetRuntimeCustomList(customKey);

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowser>();
        if (windowHandler == null || clipBrowser == null)
            return;

        windowHandler.GoTo(ScreenId.CustomClipBrowser);
        clipBrowser.SetCustomClips(customKey, label, clips);
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
}
