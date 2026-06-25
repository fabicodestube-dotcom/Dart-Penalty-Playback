using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecorderPenaltyButton : MonoBehaviour
{
    [Header("Control")]
    public CustomClipHandler customClipHandler;

    [Header("Button Settings")]
    public PenaltyType penaltyType;


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

        switch (penaltyType)
        {
            case PenaltyType.Wall:
                staticText.text = "Wand";
                break;
            case PenaltyType.Ceiling:
                staticText.text = "Decke";
                break;
            case PenaltyType.AllMiss:
                staticText.text = "3xMiss";
                break;
            case PenaltyType.ThreeOnes:
                staticText.text = "3x1";
                break;
            case PenaltyType.Schnapszahl:
                staticText.text = "Schnapszahl";
                break;
            case PenaltyType.LostGame:
                staticText.text = "LostGame";
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
        customClipHandler.OpenPenaltyClips(penaltyType);
    }
}
