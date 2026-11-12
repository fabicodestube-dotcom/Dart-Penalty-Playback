using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens the ClipBrowserScreen for a custom key (Penalty/Event/Sector ID)
/// or a score slot. substitute for the Open* methods of the deleted
/// Tabs (CustomClipHandler): The customization rows call via their page
/// Here instead of navigating via a button grid.
/// Wiring in the scene: assign WindowHandler + ClipBrowserScreen (fallback:
/// FindAnyObjectByType. Pages hold a reference (clipNav) and reach
/// they pass them on to their rows.
/// </summary>
public class ClipNavigationHelper : MonoBehaviour
{
    [Header("Control")]
    public WindowHandler windowHandler;

    [Header("Clip Browser")]
    public ClipBrowserScreen clipBrowser;

    private void Awake()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowserScreen>();
    }

    public void OpenCustomClips(string customKey, string label)
    {
        if (AudioLibrary.Instance == null || !AudioLibrary.Instance.IsReady)
            return;

        List<AudioClip> clips = AudioLibrary.Instance.GetRuntimeCustomList(customKey);

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (clipBrowser == null)
            clipBrowser = FindAnyObjectByType<ClipBrowserScreen>();
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
            clipBrowser = FindAnyObjectByType<ClipBrowserScreen>();
        if (windowHandler == null || clipBrowser == null)
            return;

        windowHandler.GoTo(ScreenId.CustomClipBrowser);
        clipBrowser.SetScoreClips(score, clips);
    }
}
