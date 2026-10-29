using UnityEngine;

public class LanguageButton : MonoBehaviour
{
    [Tooltip("The language code for this button, e.g., 'en', 'de', 'es'")]
    public string localeCode;

    [Tooltip("The visual border GameObject that indicates this button is selected")]
    public GameObject borderObject;
}