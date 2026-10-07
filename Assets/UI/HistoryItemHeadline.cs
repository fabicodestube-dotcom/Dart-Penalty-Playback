using TMPro;
using UnityEngine;
using Newtonsoft.Json;

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

    public void Initialize(GameSummary summary)
    {
        var finishedAt = summary.FinishedAt;

        textDate.text = finishedAt.HasValue
            ? finishedAt.Value.ToString("dd.MM.yyyy HH:mm")
            : summary.LastActivityAt.ToString("dd.MM.yyyy HH:mm");

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

        if (summary.GameMode == GameMode.X01)
        {
            textSettings.text = "X01 (" + GetSettingsString(summary) + ")";
        }
        else if (summary.GameMode == GameMode.Cricket)
        {
            textSettings.text = "Cricket (" + GetSettingsString(summary) + ")";
        }
        else if (summary.GameMode == GameMode.ATC)
        {
            textSettings.text = "ATC (" + GetSettingsString(summary) + ")";
        }

        RenderPenalties(summary);
    }

    private string GetSettingsString(GameSummary summary)
    {
        var settings = GetSettings(summary);
        return settings?.GetString() ?? "";
    }

    private void RenderPenalties(GameSummary summary)
    {
        var settings = GetSettings(summary);

        if (settings == null)
        {
            Debug.LogWarning(" No settings found for game ID " + summary.Id);
            return;
        }

        SetupPenaltyCosts(settings);
    }

    private GameSettings GetSettings(GameSummary summary)
    {
        if (!string.IsNullOrEmpty(summary.SettingsJson))
        {
            try
            {
                GameSettings settings = summary.GameMode switch
                {
                    GameMode.X01 => JsonConvert.DeserializeObject<X01GameSettings>(summary.SettingsJson),
                    GameMode.Cricket => JsonConvert.DeserializeObject<CricketGameSettings>(summary.SettingsJson),
                    GameMode.ATC => JsonConvert.DeserializeObject<ATCGameSettings>(summary.SettingsJson),
                    _ => null
                };
                if (settings != null)
                    return settings;
            }
            catch
            {
            }
        }

        return summary.GameMode switch
        {
            GameMode.X01 => new X01GameSettings
            {
                pointTarget = 501,
                checkinType = CheckinType.StraightIn,
                checkoutType = CheckoutType.Double,
                setsAndLegsMode = SetsAndLegs.BestOf,
                setCount = 1,
                legCount = 1,
                soundEnabled = true,
                Penalties = new PenaltySettings()
            },
            GameMode.Cricket => new CricketGameSettings
            {
                pointsEnabled = true,
                cutThroatEnabled = false,
                setsAndLegsMode = SetsAndLegs.BestOf,
                setCount = 1,
                legCount = 1,
                soundEnabled = true,
                Penalties = new PenaltySettings()
            },
            GameMode.ATC => new ATCGameSettings
            {
                targetType = ATCTargetType.Singles,
                order = ATCOrder.Ascending,
                setsAndLegsMode = SetsAndLegs.BestOf,
                setCount = 1,
                legCount = 1,
                soundEnabled = true,
                Penalties = new PenaltySettings()
            },
            _ => null
        };
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
