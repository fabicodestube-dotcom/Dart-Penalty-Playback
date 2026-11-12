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
        
        // IMPORTANT: The GameObject must be activated IMMEDIATELY,
        // so that it becomes visible while the panel is spread smoothly.
        // When we close, we deactivate it only AFTER the animation (see Coroutine).
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
        
        // Calculate the final target height (your existing logic)
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

        // The Liquid Animation Loop
        RectTransform rectTransform = transform as RectTransform;
        RectTransform parentRect = transform.parent as RectTransform;

        while (time < animationDuration)
        {
            time += Time.deltaTime;
            float progress = time / animationDuration;
            float evaluatedProgress = animationCurve.Evaluate(progress);

            // Height liquid interpolation
            layoutElement.preferredHeight = Mathf.Lerp(startHeight, targetHeight, evaluatedProgress);

            // Force layout rebuild so that the UI grows live
            if (rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

            yield return null;
        }

        // 3. Set exact final value
        layoutElement.preferredHeight = targetHeight;
        if (rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        // Clean Closure: Only when the animation is completely over,
        // We will turn off the GameObjects of closed sections.
        for (int i = 0; i < sectionExpanded.Count; i++)
        {
            if (!sectionExpanded[i])
            {
                sectionContents[i].SetActive(false);
            }
        }
    }
}
