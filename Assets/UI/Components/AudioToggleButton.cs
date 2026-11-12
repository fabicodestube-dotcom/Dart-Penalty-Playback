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

    // Eigenschaft, um den Zustand von außen auszulesen
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
    /// Initialisiert den Button mit einem Startzustand (wichtig für Lade- oder Save-Systeme).
    /// </summary>
    /// <param name="startMuted">True, wenn der Ton anfangs aus sein soll.</param>
    public void Initialize(bool startMuted)
    {
        // Zustand setzen (Invertiert, da SetAudioState true für "Sound an" erwartet)
        SetAudioState(!startMuted);
    }

    private void OnButtonClick()
    {
        // Zustand umkehren
        SetAudioState(!isSoundOn);
        AppSettingsManager.Instance.Settings.Sound.Enabled = isSoundOn;
    }

    private void SetAudioState(bool state)
    {
        isSoundOn = state;

        // Sprite basierend auf dem Zustand wechseln
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
