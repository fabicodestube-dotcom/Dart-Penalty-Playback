

using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class TMP_AndroidUppercase : MonoBehaviour
{
    private TMP_InputField inputField;

    void Start()
    {
        inputField = GetComponent<TMP_InputField>();
        
        // Register the event when the text changes
        inputField.onValueChanged.AddListener(OnTextChanged);
        
        // Initial check for the start
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

            // Important: ContentType controls auto-capitalization
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