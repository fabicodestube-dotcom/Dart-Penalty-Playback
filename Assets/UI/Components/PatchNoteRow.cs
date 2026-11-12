using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Wichtig für TextMeshPro

public class PatchNoteRow : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private RectTransform patchNotesPanel; 
    [SerializeField] private RectTransform arrowIndicator;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private LayoutElement panelLayoutElement;
    private bool isOpened = false;
    private Coroutine toggleCoroutine;

    // Referenzen auf mögliche Text-Komponenten
    private TMP_Text tmpText;
    private Text textOld;

    private void Awake()
    {
        panelLayoutElement = patchNotesPanel.GetComponent<LayoutElement>();
        if (panelLayoutElement == null)
        {
            panelLayoutElement = patchNotesPanel.gameObject.AddComponent<LayoutElement>();
        }

        panelLayoutElement.flexibleHeight = 0;

        // Text-Komponenten direkt auf oder innerhalb des Panels suchen
        tmpText = patchNotesPanel.GetComponentInChildren<TMP_Text>();
        if (tmpText == null)
        {
            textOld = patchNotesPanel.GetComponentInChildren<Text>();
        }

        // Startzustand: Geschlossen
        panelLayoutElement.preferredHeight = 0f;
    }

    public void TogglePatchNotes()
    {
        isOpened = !isOpened;

        if (toggleCoroutine != null)
        {
            StopCoroutine(toggleCoroutine);
        }

        toggleCoroutine = StartCoroutine(AnimateToggle());
    }

    private IEnumerator AnimateToggle()
    {
        float time = 0;
        float startHeight = panelLayoutElement.preferredHeight;
        float endHeight = 0f;

        if (isOpened)
        {
            // Wir berechnen die Zielhöhe direkt über die Text-Komponente.
            // Das umgeht den Fehler, bei dem das Panel '0' meldet.
            if (tmpText != null)
            {
                endHeight = tmpText.preferredHeight;
            }
            else if (textOld != null)
            {
                endHeight = textOld.preferredHeight;
            }
            else
            {
                // Fallback, falls es kein reiner Text ist, sondern ein komplexes GO
                endHeight = LayoutUtility.GetPreferredHeight(patchNotesPanel);
            }

            // Falls zusätzliche Paddings/Abstände in deiner Row-Layoutgruppe sind,
            // kannst du hier einen kleinen Puffer addieren (z.B. + 20f);
        }

        Quaternion startRotation = arrowIndicator.localRotation;
        Quaternion endRotation = Quaternion.Euler(0, 0, isOpened ? 180f : 0f);

        RectTransform rowRect = transform as RectTransform;
        RectTransform contentRect = transform.parent as RectTransform;

        while (time < animationDuration)
        {
            time += Time.deltaTime;
            float progress = time / animationDuration;
            float evaluatedProgress = animationCurve.Evaluate(progress);

            panelLayoutElement.preferredHeight = Mathf.Lerp(startHeight, endHeight, evaluatedProgress);
            arrowIndicator.localRotation = Quaternion.Lerp(startRotation, endRotation, evaluatedProgress);

            // Zwinge das Layout zum Update
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            yield return null;
        }

        panelLayoutElement.preferredHeight = endHeight;
        arrowIndicator.localRotation = endRotation;
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }
}
