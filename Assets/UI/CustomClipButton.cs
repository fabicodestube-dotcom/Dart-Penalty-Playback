using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clip-Button des Score-Grids (ScoreClipGridHandler): ein Button je
/// Score-Slot (0–180), zeigt die Clip-Anzahl (Farbwechsel
/// imageGotClips/imageNoClips) und öffnet den ClipBrowser für den Slot.
/// SetupCustom bleibt für Nachnutzung (öffnet via ClipNavigationHelper).
/// </summary>
public class CustomClipButton : MonoBehaviour
{
    [Header("Control")]
    [Tooltip("Navigation für Custom-Keys (Fallback, wenn gesetzt).")]
    public ClipNavigationHelper clipNav;
    [Tooltip("Wird vom ScoreClipGridHandler beim Erzeugen gesetzt.")]
    public ScoreClipGridHandler scoreGridHandler;

    [Header("UI Elements")]
    public TMP_Text clipCountText;
    public TMP_Text staticText;
    public Image imageGotClips;
    public Image imageNoClips;

    private AudioCategory category;
    private int score;
    private string customKey;
    private string displayLabel;

    private void Awake()
    {
        if (clipNav == null)
        {
            clipNav = FindAnyObjectByType<ClipNavigationHelper>();
        }
        if (scoreGridHandler == null)
        {
            scoreGridHandler = FindAnyObjectByType<ScoreClipGridHandler>();
        }
    }

    public void SetupScore(int scoreValue, string label)
    {
        category = AudioCategory.Score;
        score = scoreValue;
        SetLabel(label);
        Refresh();
    }

    public void SetupCustom(string key, string label)
    {
        category = AudioCategory.Custom;
        customKey = key;
        SetLabel(label);
        Refresh();
    }

    private void SetLabel(string label)
    {
        displayLabel = string.IsNullOrEmpty(label) ? customKey : label;

        if (staticText != null)
            staticText.text = displayLabel;
    }

    public void Refresh()
    {
        int count = 0;

        if (AudioLibrary.Instance != null && AudioLibrary.Instance.IsReady)
        {
            switch (category)
            {
                case AudioCategory.Score:
                    count = AudioLibrary.Instance.GetRuntimeScoreList(score)?.Count ?? 0;
                    break;
                case AudioCategory.Custom:
                    count = AudioLibrary.Instance.GetCustomClipCount(customKey);
                    break;
            }
        }

        UpdateView(count);
    }

    public void UpdateView(int count)
    {
        if (clipCountText != null)
            clipCountText.text = $"Clips: {count}";

        bool hasClips = count > 0;

        if (imageGotClips != null)
            imageGotClips.gameObject.SetActive(hasClips);

        if (imageNoClips != null)
            imageNoClips.gameObject.SetActive(!hasClips);
    }

    public void OnClick()
    {
        switch (category)
        {
            case AudioCategory.Score:
                if (scoreGridHandler != null)
                    scoreGridHandler.OpenScoreClips(score);
                else if (clipNav != null)
                    clipNav.OpenScoreClips(score);
                break;
            case AudioCategory.Custom:
                if (clipNav != null)
                    clipNav.OpenCustomClips(customKey, displayLabel);
                break;
        }
    }
}
