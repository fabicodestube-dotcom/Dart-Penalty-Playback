using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecorderScoreButton : MonoBehaviour
{
    [Header("Control")]
    public CustomClipHandler customClipHandler;

    [Header("Button Settings")]
    public int score = 0;

    [Header("UI Elements")]
    public TMP_Text clipCountText;
    public TMP_Text staticScoreText;
    public Image imageGotClips;
    public Image imageNoClips;

    private void Awake()
    {
        if (customClipHandler == null)
        {
            customClipHandler = FindAnyObjectByType<CustomClipHandler>();
        }

        staticScoreText.text = score.ToString();
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
        customClipHandler.OpenScoreClips(score);
    }
}
