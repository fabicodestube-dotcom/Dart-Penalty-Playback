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
        var settings = game.GetSettingsAsATC();
        bool hasStarted = game.HasPlayerStarted(playerId);

        if (target == -1)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        switch (settings.targetType)
        {
            case ATCTargetType.Singles:
                return new Throw(DartMultiplier.Single, target, HitType.Board, true, target);
            case ATCTargetType.Doubles:
                return new Throw(DartMultiplier.Double, target, HitType.Board, true, target);
            case ATCTargetType.Triples:
                return new Throw(DartMultiplier.Triple, target, HitType.Board, true, target);
            default:
                return new Throw(DartMultiplier.Single, target, HitType.Board, true, target);
        }
    }

    public void UpdateHitButtonLabel()
    {
        if (gameEngine == null || hitButtonLabel == null) return;

        var game = atcGameEngine.GetGame();
        if (game == null) return;

        Guid playerId = game.GetCurrentPlayerId();
        int target = game.GetCurrentTarget(playerId);
        var settings = game.GetSettingsAsATC();

        string label = GetTargetLabel(target, settings.targetType);
        hitButtonLabel.text = label;
    }

    private string GetTargetLabel(int target, ATCTargetType targetType)
    {
        if (target == -1) return "Finished";

        bool hasStarted = false;
        if (gameEngine != null && atcGameEngine.GetGame() != null)
        {
            Guid playerId = atcGameEngine.GetGame().GetCurrentPlayerId();
            hasStarted = atcGameEngine.GetGame().HasPlayerStarted(playerId);
        }

        switch (targetType)
        {
            case ATCTargetType.Singles:
                return target.ToString();
            case ATCTargetType.Doubles:
                return $"D{target}";
            case ATCTargetType.Triples:
                return $"T{target}";
            default:
                return target.ToString();
        }
    }
}