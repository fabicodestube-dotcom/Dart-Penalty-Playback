using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecorderSpecialButton : MonoBehaviour
{
    [Header("Control")]
    public CustomClipHandler customClipHandler;

    [Header("Button Settings")]
    public SpecialAudioType audioType;

    [Header("UI Elements")]
    public TMP_Text clipCountText;
    public TMP_Text staticText;
    public Image imageGotClips;
    public Image imageNoClips;

    private void Awake()
    {
        if (customClipHandler == null)
        {
            customClipHandler = FindAnyObjectByType<CustomClipHandler>();
        }

        if (imageGotClips == null || imageNoClips == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            if (images.Length >= 2)
            {
                imageGotClips ??= images[0];
                imageNoClips ??= images[1];
            }
            else
            {
                Debug.LogError(
                    $"{nameof(UpdateView)} benötigt mindestens 2 Image-Komponenten in den Children.",
                    this);
            }
        }

        switch (audioType)
        {
            case SpecialAudioType.StartGame:
                staticText.text = "Game Start";
                break;
            case SpecialAudioType.Undo:
                staticText.text = "Undo";
                break;
            case SpecialAudioType.Bust:
                staticText.text = "Bust";
                break;
            case SpecialAudioType.FourTwenty:
                staticText.text = "420";
                break;
            case SpecialAudioType.SuddenDeath:
                staticText.text = "SuddenDeath";
                break;
            case SpecialAudioType.MatchWon:
                staticText.text = "Game Over";
                break;
            case SpecialAudioType.Hits:
                staticText.text = "Hits";
                break;
            case SpecialAudioType.Marks:
                staticText.text = "Marks";
                break;
        }
    }

    public void UpdateView(int count)
    {
        clipCountText.text = $"Clips: {count}";

        bool hasClips = count > 0;

        imageGotClips.gameObject.SetActive(hasClips);
        imageNoClips.gameObject.SetActive(!hasClips);
    }

    public void OnClick()
    {
        customClipHandler.OpenSpecialClips(audioType);
    }
}
