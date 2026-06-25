using UnityEngine;
using UnityEngine.UI;


public class X01InputHandler : InputHandler
{

    [Header("Multiplier Buttons")]
    public Button doubleButton;
    public Button tripleButton;

    [Header("Bull Button")]
    public Button bull25Button;
    public CanvasGroup bull25CanvasGroup;

    private DartMultiplier currentMultiplier = DartMultiplier.Single;

    
    // ========== FARBEN =========
    // Gelb
    private static readonly Color YellowLight  = new Color(200f / 255f, 180f / 255f, 70f  / 255f); // heller Gelbton, gut auf dunklem Hintergrund
    private static readonly Color YellowSolid  = new Color(180f / 255f, 150f / 255f, 20f  / 255f); // deckendes Gelb

    // Orange
    private static readonly Color OrangeLight  = new Color(220f / 255f, 120f / 255f, 40f  / 255f); // heller Orange-Ton
    private static readonly Color OrangeSolid  = new Color(200f / 255f, 90f  / 255f, 0f   / 255f); // deckendes Orange

    // Rot
    private static readonly Color RedSolid     = new Color(180f / 255f, 40f  / 255f, 40f  / 255f); // klares, dunkleres Rot



    // =========================
    // MULTIPLIER
    // =========================


    public void ToggleDouble()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            if (currentMultiplier == DartMultiplier.Double)
                currentMultiplier = DartMultiplier.Single;
            else
                currentMultiplier = DartMultiplier.Double;

            UpdateMultiplierUI();
        }
    }

    public void ToggleTriple()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            if (currentMultiplier == DartMultiplier.Triple)
                currentMultiplier = DartMultiplier.Single;
            else
                currentMultiplier = DartMultiplier.Triple;

        UpdateMultiplierUI();
        }
    }

    private void ResetMultiplier()
    {
        currentMultiplier = DartMultiplier.Single;
        UpdateMultiplierUI();
    }

    // =========================
    // ZAHLEN
    // =========================

    public void OnNumberPressed(int value)
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            var multiplierToUse = currentMultiplier;

            // Triple Bull verhindern
            if (value == 25 && multiplierToUse == DartMultiplier.Triple)
            {
                Debug.Log("Triple Bull nicht erlaubt");
                return;
            }

            AddThrow(value, multiplierToUse, HitType.Board);
            ResetMultiplier();
        }
    }

    // =========================
    // SPEZIAL
    // =========================

    public void OnBoardMiss()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            AddThrow(0, DartMultiplier.Single, HitType.Board);
            ResetMultiplier();
        }
    }


    // =========================
    // CORE
    // =========================

    private void AddThrow(int value, DartMultiplier multiplier, HitType hitType)
    {
        var t = new Throw(multiplier, value, hitType);
        gameEngine.AddThrow(t);
    }

    // =========================
    // UI STATE
    // =========================

    private void UpdateMultiplierUI()
    {
        UpdateBull25Availability();

        // Double Button
        if (currentMultiplier == DartMultiplier.Double)
        {
            doubleButton.image.color = YellowSolid; // aktiv → solid yellow
            tripleButton.image.color = OrangeLight;  // inaktiv → light orange
        }
        else if (currentMultiplier == DartMultiplier.Triple)
        {
            doubleButton.image.color = YellowLight;  // inaktiv → light yellow
            tripleButton.image.color = OrangeSolid;  // aktiv → solid orange
        }
        else
        {
            // Beide inaktiv
            doubleButton.image.color = YellowLight;
            tripleButton.image.color = OrangeLight;
        }
    }

    private void UpdateBull25Availability()
    {
        if (bull25Button == null)
            return;

        bool tripleActive = currentMultiplier == DartMultiplier.Triple;
        bull25Button.interactable = !tripleActive;

        // Visual feedback without overriding themed colors.
        var cg = bull25CanvasGroup != null ? bull25CanvasGroup : bull25Button.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.interactable = !tripleActive;
            cg.blocksRaycasts = !tripleActive;
            cg.alpha = tripleActive ? 0.45f : 1f;
        }
    }
}