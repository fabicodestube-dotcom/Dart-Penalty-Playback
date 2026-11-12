using System.Collections;
using TMPro;
using UnityEngine;

public class CheckoutHandler : MonoBehaviour
{
    [Header("Assignments")]
    public TMP_Text checkoutText;

    [Header("Pulse Settings")]
    public float pulseSpeed = 10f;       // Speed of the pulse oscillation
    public float baseScale = 1.0f;       // Starting scale (1.0 = original size)
    public float maxScale = 1.2f;        // Maximum target scale during the pulse

    private Coroutine pulseCoroutine;
    private RectTransform textRectTransform;

    private void Awake()
    {
        // Cache the RectTransform component of the text to avoid internal overhead
        if (checkoutText != null)
        {
            textRectTransform = checkoutText.GetComponent<RectTransform>();
        }
    }

    public void Show(string option)
    {
        // Stop any active pulse effect from previous text changes
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (option == null)
        {
            checkoutText.text = " ";
            if (textRectTransform != null)
            {
                textRectTransform.localScale = Vector3.one;
            }
            return;
        }

        // Assign the new text and start the parallel pulse effect
        checkoutText.text = option;

        if (textRectTransform != null)
        {
            pulseCoroutine = StartCoroutine(AnimatePulse());
        }
    }

    private IEnumerator AnimatePulse()
    {
        float timeCounter = 0f;
        
        // Pre-calculate the center and amplitude of the pulse to optimize performance
        float midScale = (baseScale + maxScale) * 0.5f;
        float amplitude = (maxScale - baseScale) * 0.5f;

        while (true)
        {
            timeCounter += Time.deltaTime * pulseSpeed;

            // Mathf.Sin outputs values from -1 to 1.
            // Shifting it by amplitude and midScale perfectly maps it between baseScale and maxScale.
            float currentScale = midScale + Mathf.Sin(timeCounter) * amplitude;
            
            // Scaled smoothly without any GC allocations
            textRectTransform.localScale = Vector3.one * currentScale;

            yield return null;
        }
    }

    private void OnDisable()
    {
        // Safety clean up if the component or GameObject gets disabled
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }
}
