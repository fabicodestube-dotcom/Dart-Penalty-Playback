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
    public TMP_Text textTotelPenaltiesCount;
    public TMP_Text textTotalPenaltyCostSum;

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

        if (textTotelPenaltiesCount != null)
            textTotelPenaltiesCount.text = CountActivePenalties(summary, settings).ToString();

        if (textTotalPenaltyCostSum != null)
            textTotalPenaltyCostSum.text = SumImposedPenaltyCosts(summary, settings).ToString("0.##");
    }

    private static readonly PenaltyType[] CountedPenaltyTypes =
    {
        PenaltyType.Wall,
        PenaltyType.Ceiling,
        PenaltyType.AllMiss,
        PenaltyType.ThreeOnes,
        PenaltyType.Schnapszahl,
        PenaltyType.LostGame
    };

    /// <summary>
    /// Wie viele Strafregeln im Spiel gegriffen haben: Legacy-Typen mit
    /// Buchungen (alte Spiele) + aktivierte Snapshot-Customs.
    /// Legacy-Settings zählen nicht mehr (System stillgelegt); neue Spiele
    /// buchen dort 0 und zeigen nur Customs.
    /// </summary>
    private static int CountActivePenalties(GameSummary summary, GameSettings settings)
    {
        int count = 0;

        if (summary?.PlayerEntries != null)
        {
            foreach (var type in CountedPenaltyTypes)
            {
                int total = 0;
                foreach (var entry in summary.PlayerEntries)
                {
                    if (entry == null)
                        continue;
                    total += type switch
                    {
                        PenaltyType.Wall => entry.WallCount,
                        PenaltyType.Ceiling => entry.CeilingCount,
                        PenaltyType.AllMiss => entry.AllMissCount,
                        PenaltyType.ThreeOnes => entry.ThreeOnesCount,
                        PenaltyType.Schnapszahl => entry.TripleDigitCount,
                        PenaltyType.LostGame => entry.LostGameCount,
                        _ => 0
                    };
                    if (total > 0)
                        break;
                }

                if (total > 0)
                    count++;
            }
        }

        var snapshot = summary?.CustomPenaltySnapshot;
        if (snapshot != null)
        {
            foreach (var def in snapshot)
            {
                if (def != null && def.enabled)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Summe der verhängten Strafen über alle Spieler (eine Wahrheit mit
    /// dem Detail-Popup: PenaltyDisplay.PlayerImposedTotal pro Spieler).
    /// Z.B. Wall 3× à 10 + Ceiling 1× à 20 = 50.
    /// </summary>
    private static float SumImposedPenaltyCosts(GameSummary summary, GameSettings settings)
    {
        float total = 0f;

        var entries = summary?.PlayerEntries;
        if (entries == null)
            return total;

        foreach (var entry in entries)
            total += PenaltyDisplay.PlayerImposedTotal(summary, entry, settings);

        return total;
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

}
