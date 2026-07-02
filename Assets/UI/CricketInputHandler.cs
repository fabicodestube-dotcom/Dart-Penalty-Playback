using UnityEngine;
using UnityEngine.UI;

public class CricketInputHandler : InputHandler
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
    private static readonly Color YellowLight  = new Color(200f / 255f, 180f / 255f, 70f  / 255f);
    private static readonly Color YellowSolid  = new Color(180f / 255f, 150f / 255f, 20f  / 255f);

    // Orange
    private static readonly Color OrangeLight  = new Color(220f / 255f, 120f / 255f, 40f  / 255f);
    private static readonly Color OrangeSolid  = new Color(200f / 255f, 90f  / 255f, 0f   / 255f);

    // =========================
    // INSPECTOR FRIENDLY INPUTS
    // =========================
    // These are meant to be wired directly in the Unity Inspector (Buttons / InputAction events).

    public void Input15() => OnNumberPressed(15);
    public void Input16() => OnNumberPressed(16);
    public void Input17() => OnNumberPressed(17);
    public void Input18() => OnNumberPressed(18);
    public void Input19() => OnNumberPressed(19);
    public void Input20() => OnNumberPressed(20);
    public void Input25() => OnNumberPressed(25);
    public void Input1() => OnNumberPressed(1);

    // 0 == miss on board (HitType.Board with value 0)
    public void Input0() => OnBoardMiss();

    public void Wall() => OnWall();
    public void Ceiling() => OnCeiling();
    public void UndoInput() => Undo();


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

    public void OnNumberPressed(int value)
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            var multiplierToUse = currentMultiplier;

            // No triple bull
            if (value == 25 && multiplierToUse == DartMultiplier.Triple)
                return;

            AddThrow(value, multiplierToUse, HitType.Board);
            ResetMultiplier();
        }
    }

    public void OnBoardMiss()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            AddThrow(0, DartMultiplier.Single, HitType.Board);
            ResetMultiplier();
        }
    }

    private void AddThrow(int value, DartMultiplier multiplier, HitType hitType)
    {
        if (gameEngine == null) return;

        if (inputEnabled)
        {
            if (value == 0)
            {
                gameEngine.AddThrow(new Throw(DartMultiplier.Single, value, hitType));
            }
            else
            {
                gameEngine.AddThrow(new Throw(multiplier, value, hitType));
            }
        }
    }

    private void UpdateMultiplierUI()
    {
        UpdateBull25Availability();

        if (currentMultiplier == DartMultiplier.Double)
        {
            doubleButton.image.color = YellowSolid;
            tripleButton.image.color = OrangeLight;
        }
        else if (currentMultiplier == DartMultiplier.Triple)
        {
            doubleButton.image.color = YellowLight;
            tripleButton.image.color = OrangeSolid;
        }
        else
        {
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

        var cg = bull25CanvasGroup != null ? bull25CanvasGroup : bull25Button.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.interactable = !tripleActive;
            cg.blocksRaycasts = !tripleActive;
            cg.alpha = tripleActive ? 0.45f : 1f;
        }
    }
}

