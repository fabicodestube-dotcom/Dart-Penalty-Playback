using System.Collections;
using UnityEngine;

public class ATCStreakEffect : MonoBehaviour
{
    [Header("Assignments")]
    public RectTransform fireIconLeft;
    public RectTransform fireIconRight;

    [Header("Settings")]
    public float pulseSpeed = 10f;       // Speed of the pulse oscillation
    public float baseScale = 1.0f;       // Starting scale (1.0 = original size)
    public float maxScale = 1.4f;        // Maximum target scale during the pulse

    private Coroutine streakCoroutine;

    // Call this method when the streak starts
    public void StartStreak()
    {
        if (streakCoroutine != null)
        {
            StopCoroutine(streakCoroutine);
        }

        if (fireIconLeft != null && fireIconRight != null)
        {
            fireIconLeft.gameObject.SetActive(true);
            fireIconRight.gameObject.SetActive(true);
            streakCoroutine = StartCoroutine(AnimateStreak());
        }
    }

    // Call this method to stop the effect
    public void StopStreak()
    {
        if (streakCoroutine != null)
        {
            StopCoroutine(streakCoroutine);
            streakCoroutine = null;
        }

        if (fireIconLeft != null && fireIconRight != null)
        {
            // Reset the scale to default when stopping
            fireIconLeft.localScale = Vector3.one;
            fireIconRight.localScale = Vector3.one;
            
            fireIconLeft.gameObject.SetActive(false);
            fireIconRight.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateStreak()
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
            Vector3 newScale = Vector3.one * currentScale;

            // Scale both transforms simultaneously without any GC allocations
            fireIconLeft.localScale = newScale;
            fireIconRight.localScale = newScale;

            yield return null; 
        }
    }
}
