using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ATCInputHandler : InputHandler
{
    public ATCGameEngine atcGameEngine;
 
    [Header("Target Button")]
    public TMP_Text hitButtonLabel;

    [Header("Penalty Buttons")]
    public CanvasGroup oneCanvasGroup;



    // =========================
    // INPUT
    // =========================


    public void Hit()
    {
        if (inputEnabled)
        {
            Debug.Log("Hit");
            TriggerHaptic();
            var t = CreateHitThrow();
            gameEngine.AddThrow(t);
        }
    }

    public void Miss()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            var t = new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);
            gameEngine.AddThrow(t);
        }
    }

    public void One()
    {
        if (inputEnabled)
        {
            TriggerHaptic();
            var game = atcGameEngine.GetGame();
            Guid playerId = game.GetCurrentPlayerId();
            int target = game.GetCurrentTarget(playerId);

            bool isTargetHit = target == 1;

            var t = new Throw(
                DartMultiplier.Single,
                1,
                HitType.Board,
                isTargetHit,
                isTargetHit ? 1 : -1
            );
            gameEngine.AddThrow(t);
        }
    }



    // =========================
    // CORE
    // =========================

    private Throw CreateHitThrow()
    {
        if (gameEngine == null)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        var game = atcGameEngine.GetGame();
        if (game == null)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        Guid playerId = game.GetCurrentPlayerId();
        int target = game.GetCurrentTarget(playerId);
        DartMultiplier required = game.GetCurrentTargetMultiplier(playerId);

        if (target == -1)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        return new Throw(required, target, HitType.Board, true, target);
    }

    public void UpdateHitButtonLabel()
    {
        if (gameEngine == null || hitButtonLabel == null) return;

        var game = atcGameEngine.GetGame();
        if (game == null) return;

        Guid playerId = game.GetCurrentPlayerId();
        int target = game.GetCurrentTarget(playerId);
        DartMultiplier required = game.GetCurrentTargetMultiplier(playerId);

        hitButtonLabel.text = GetTargetLabel(target, required);
    }

    private string GetTargetLabel(int target, DartMultiplier required)
    {
        if (target == -1) return LocalizationText.Get("Finished", "Finished");

        return required switch
        {
            DartMultiplier.Double => $"D{target}",
            DartMultiplier.Triple => $"T{target}",
            _ => target.ToString()
        };
    }
}