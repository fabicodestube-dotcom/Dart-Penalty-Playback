

using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class TMP_AndroidUppercase : MonoBehaviour
{
    private TMP_InputField inputField;

    void Start()
    {
        inputField = GetComponent<TMP_InputField>();
        
        // Registriere das Event, wenn sich der Text ändert
        inputField.onValueChanged.AddListener(OnTextChanged);
        
        // Initialer Check für den Start
        ConfigureKeyboard();
    }

    void OnTextChanged(string currentText)
    {
        ConfigureKeyboard();
    }

    private void ConfigureKeyboard()
    {
        // Wir ändern die Konfiguration nur, wenn die Tastatur für mobile Geräte gedacht ist
        if (string.IsNullOrEmpty(inputField.text))
        {
            // Erzwingt die native Autokorrektur/Großschreibung für das erste Zeichen
            inputField.inputType = TMP_InputField.InputType.Standard;
            
            #if UNITY_ANDROID && !UNITY_EDITOR
            // Setzt den nativen Android-Tastaturtyp auf Autocapitalize
            TouchScreenKeyboard.Android.inputType = TouchScreenKeyboard.Android.InputType.AutoCapitalize;
            #endif
        }
    }

    void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnTextChanged);
        }
    }
}