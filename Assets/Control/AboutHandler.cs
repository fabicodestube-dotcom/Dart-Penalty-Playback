using UnityEngine;

public class AboutHandler : MonoBehaviour
{
    public WindowHandler windowHandler;

    // Definiere deine Links hier im Inspector oder direkt im Code
    [Header("Web Links")]
    public string githubUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback";
    public string kofiUrl = "https://ko-fi.com/fabiscodestube";
    public string privacyPolicyUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/blob/main/PRIVACY_POLICY.md";
    public string licenseUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/blob/main/LICENSE";

    public void OnGoBackClicked()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (windowHandler != null)
        {
            windowHandler.GoBack();
        }
        else
        {
            Debug.LogError("Kein WindowHandler in der Szene gefunden.");
        }
    }

    public void OpenGithub()
    {
        OpenBrowser(githubUrl);
    }

    public void OpenKoFi()
    {
        OpenBrowser(kofiUrl);
    }

    public void OpenPrivacyPolicy()
    {
        OpenBrowser(privacyPolicyUrl);
    }

    public void OpenLicense()
    {
        OpenBrowser(licenseUrl);
    }

    // Eine kleine Hilfsmethode, um den Code sauber zu halten und Fehler abzufangen
    private void OpenBrowser(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
        else
        {
            Debug.LogWarning("Der eingegebene Link ist leer!");
        }
    }
}
