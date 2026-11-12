

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
        if (inputField == null) return;

        if (string.IsNullOrEmpty(inputField.text))
        {
            inputField.inputType = TMP_InputField.InputType.Standard;

            // Wichtig: ContentType steuert Auto-Capitalization
            inputField.contentType = TMP_InputField.ContentType.Name; 
            inputField.ForceLabelUpdate();
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