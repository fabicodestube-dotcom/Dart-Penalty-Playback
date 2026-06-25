using TMPro;
using UnityEngine;

public class HistoryItemHeadline : MonoBehaviour
{
    public TMP_Text textDate;

    public GameObject textGameStatusFinished;
    public GameObject textGameStatusRunning;

    public TMP_Text textSettings;

    [Header("Penalites UI Elements")]
    public TMP_Text textPenaltyWall;
    public TMP_Text textPenaltyCeiling;
    public TMP_Text textPenaltyAllMiss;
    public TMP_Text textPenaltyTripleOnes;
    public TMP_Text textPenaltySchnapszahl;
    public TMP_Text textPenaltyLose;

    public void Initialize (Game g)
    {
        var finishedAt = g.GetFinishedAt();

        textDate.text = finishedAt.HasValue
            ? finishedAt.Value.ToString("dd.MM.yyyy HH:mm")
            : g.GetLastActivityAt().ToString("dd.MM.yyyy HH:mm");

        if (finishedAt.HasValue)
        {
            textGameStatusRunning.SetActive(false);
            textGameStatusFinished.SetActive(true);
        }
        else
        {
            textGameStatusFinished.SetActive(false);
            textGameStatusRunning.SetActive(true);
        }

        if (g is X01Game)
        {
            textSettings.text = "X01 (" + g.GetSettings().GetString() + ")";
        }
        else if (g is CricketGame)
        {
            textSettings.text = "Cricket (" + g.GetSettings().GetString() + ")";
        }
        else if (g is ATCGame)
        {
            textSettings.text = "ATC (" + g.GetSettings().GetString() + ")";
        }

        RenderPenalties(g);
    }

    private void RenderPenalties(Game g)
    {
        var settings = g.GetSettings();

        if (settings == null)
        {
            Debug.LogWarning(" No settings found for game ID " + g.GetID());
            return;
        }

        SetupPenaltyCosts(settings);
    }

    private void SetupPenaltyCosts(GameSettings settings)
    {
        if (settings.Penalties.IsEnabled(PenaltyType.Wall))
        {
            textPenaltyWall.text = settings.Penalties.GetCost(PenaltyType.Wall).ToString("0.##");
        }
        else
        {
            textPenaltyWall.text = "Off";
        }

        if (settings.Penalties.IsEnabled(PenaltyType.Ceiling))
        {
            textPenaltyCeiling.text = settings.Penalties.GetCost(PenaltyType.Ceiling).ToString("0.##");
        }
        else
        {
            textPenaltyCeiling.text = "Off";
        }

        if (settings.Penalties.IsEnabled(PenaltyType.AllMiss))
        {
            textPenaltyAllMiss.text = settings.Penalties.GetCost(PenaltyType.AllMiss).ToString("0.##");
        }
        else
        {
            textPenaltyAllMiss.text = "Off";
        }

        if (settings.Penalties.IsEnabled(PenaltyType.ThreeOnes))
        {
            textPenaltyTripleOnes.text = settings.Penalties.GetCost(PenaltyType.ThreeOnes).ToString("0.##");
        }
        else
        {
            textPenaltyTripleOnes.text = "Off";
        }

        if (settings.Penalties.IsEnabled(PenaltyType.Schnapszahl))
        {
            textPenaltySchnapszahl.text = settings.Penalties.GetCost(PenaltyType.Schnapszahl).ToString("0.##");
        }
        else
        {
            textPenaltySchnapszahl.text = "Off";
        }

        if (settings.Penalties.IsEnabled(PenaltyType.LostGame))
        {
                textPenaltyLose.text = settings.Penalties.GetCost(PenaltyType.LostGame).ToString("0.##");
        }
        else
        {
                textPenaltyLose.text = "Off";
        }
    }
}
