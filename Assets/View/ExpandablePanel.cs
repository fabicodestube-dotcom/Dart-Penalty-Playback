using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class ExpandablePanel : MonoBehaviour
{
    public LayoutElement layoutElement;

    public float collapsedHeight = 200f;

    public List<int> sectionHeights = new List<int>();
    public List<bool> sectionExpanded = new List<bool>();
    public List<GameObject> sectionContents = new List<GameObject>();

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine heightAnimationCoroutine;

    public void ToggleSection(int index)
    {
        if (index < 0 || index >= sectionHeights.Count)
            return;

        sectionExpanded[index] = !sectionExpanded[index];
        
        // WICHTIG: Das GameObject muss SOFORT aktiviert werden, 
        // damit es sichtbar wird, während das Panel smooth aufbreitet wird.
        // Beim Schließen deaktivieren wir es erst NACH der Animation (siehe Coroutine).
        if (sectionExpanded[index])
        {
            sectionContents[index].SetActive(true);
        }

        StartHeightAnimation();
    }

    public void ResetSections()
    {
        for (int i = 0; i < sectionExpanded.Count; i++)
        {
            sectionExpanded[i] = false;
        }

        StartHeightAnimation();
    }

    private void StartHeightAnimation()
    {
        if (heightAnimationCoroutine != null)
        {
            StopCoroutine(heightAnimationCoroutine);
        }

        heightAnimationCoroutine = StartCoroutine(AnimateHeight());
    }

    private IEnumerator AnimateHeight()
    {
        float time = 0;
        float startHeight = layoutElement.preferredHeight;
        
        // 1. Berechne die finale Zielhöhe (deine bestehende Logik)
        float maxExtra = 0f;
        for (int i = 0; i < sectionHeights.Count; i++)
        {
            if (sectionExpanded[i])
            {
                float extra = sectionHeights[i] - collapsedHeight;
                maxExtra = Mathf.Max(maxExtra, extra);
            }
        }
        float targetHeight = collapsedHeight + maxExtra;

        // 2. Die flüssige Animationsschleife
        RectTransform rectTransform = transform as RectTransform;
        RectTransform parentRect = transform.parent as RectTransform;

        while (time < animationDuration)
        {
            time += Time.deltaTime;
            float progress = time / animationDuration;
            float evaluatedProgress = animationCurve.Evaluate(progress);

            // Höhe flüssig interpolieren
            layoutElement.preferredHeight = Mathf.Lerp(startHeight, targetHeight, evaluatedProgress);

            // Layout-Rebuild erzwingen, damit die UI live mitwächst
            if (rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

            yield return null;
        }

        // 3. Exakten Endwert setzen
        layoutElement.preferredHeight = targetHeight;
        if (rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        // 4. SAUBERES SCHLIESSEN: Erst wenn die Animation komplett vorbei ist,
        // schalten wir die GameObjects geschlossener Sektionen hart aus.
        for (int i = 0; i < sectionExpanded.Count; i++)
        {
            if (!sectionExpanded[i])
            {
                sectionContents[i].SetActive(false);
            }
        }
    }
}
