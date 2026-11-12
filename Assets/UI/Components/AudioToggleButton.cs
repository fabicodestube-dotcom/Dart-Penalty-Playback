using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AudioToggleButton : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private Button toggleButton;
    private bool isSoundOn = true;

    // Properties to read the condition from the outside
    public bool IsSoundOn => isSoundOn;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();
        toggleButton.onClick.AddListener(OnButtonClick);
    }

    public void Start()
    {
        isSoundOn = AppSettingsManager.Instance.Settings.Sound.Enabled;
        SetAudioState(isSoundOn);
    }

    /// <summary>
    /// Initializes the button with a start state (important for charging or save systems).
    /// </summary>
    /// <param name="startMuted">True, if the sound should be off at first.</param>
    public void Initialize(bool startMuted)
    {
        // Set state (inverted, because SetAudioState expects true for "Sound on")
        SetAudioState(!startMuted);
    }

    private void OnButtonClick()
    {
        // Toggle state
        SetAudioState(!isSoundOn);
        AppSettingsManager.Instance.Settings.Sound.Enabled = isSoundOn;
    }

    private void SetAudioState(bool state)
    {
        isSoundOn = state;

        // Switch Sprite Based on State
        if (buttonImage != null)
        {
            buttonImage.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
        }
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(OnButtonClick);
        }
    }
}
