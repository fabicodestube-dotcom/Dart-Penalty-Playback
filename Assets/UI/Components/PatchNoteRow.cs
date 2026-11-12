using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Important for TextMeshPro

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

    // References to possible text components
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

        // Search text components directly on or within the panel
        tmpText = patchNotesPanel.GetComponentInChildren<TMP_Text>();
        if (tmpText == null)
        {
            textOld = patchNotesPanel.GetComponentInChildren<Text>();
        }

        // Initial state: closed
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
            // We calculate the target height directly via the text component.
            // This bypasses the error where the panel reports '0'.
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
                // Fallback, if it is not a pure text, but a complex GO
                endHeight = LayoutUtility.GetPreferredHeight(patchNotesPanel);
            }

            // If there are additional paddings/spaces in your row layout group,
            // you can add a small buffer here (e.g. + 20f);
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

            // Force the layout to the update
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
