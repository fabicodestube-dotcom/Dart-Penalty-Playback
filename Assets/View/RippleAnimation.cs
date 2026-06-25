using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RippleAnimation : MonoBehaviour
{
    [Header("Settings")]
    public Image ripplePrefab;       
    public float speed = 1.5f;       
    public float maxScale = 2.5f;    
    public float interval = 2.0f;   

    private Coroutine _spawnCoroutine;
    private bool _isSpawning = false;

    // Startet die Wellen-Produktion von außen
    public void StartRipples()
    {
        Debug.Log($"START RIPPLES  frame={Time.frameCount}");

        if (_isSpawning) return;

        _isSpawning = true;
        _spawnCoroutine = StartCoroutine(SpawnRipples());
    }

    public void StopRipples()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        _isSpawning = false;

        StopAllCoroutines();

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    IEnumerator SpawnRipples()
    {
        while (_isSpawning)
        {
            Image newRipple =
                Instantiate(ripplePrefab, transform);

            StartCoroutine(AnimateRipple(newRipple));

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator AnimateRipple(Image ripple)
    {
        RectTransform rectTransform = ripple.rectTransform;
        rectTransform.localScale = Vector3.one;
        Color startColor = ripple.color;

        float timer = 0f;
        while (timer < 1f)
        {
            // Verhindert Fehler, falls das Skript/Objekt mitten im Fade zerstört wird
            if (ripple == null) yield break; 

            timer += Time.deltaTime * speed;
            rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * maxScale, timer);
            startColor.a = Mathf.Lerp(0.6f, 0f, timer);
            ripple.color = startColor;

            yield return null;
        }

        if (ripple != null)
        {
            Destroy(ripple.gameObject);
        }
    }
}
