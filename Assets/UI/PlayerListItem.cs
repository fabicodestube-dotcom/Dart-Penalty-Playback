using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class PlayerListItem : MonoBehaviour
{
    public TMP_Text playerName;
    public Image difficultyImage;

    private PlayerList list;
    private Guid id;

    public Guid ID => id;

    public void Initialize(PlayerList list)
    {
        this.list = list;
    }

    public void SetData(BasePlayer player)
    {
        if (player == null) return;

        this.id = player.GetID();

        if (player is DartBot bot)
        {
            playerName.text = bot.GetNameWithDifficulty();
            ApplyDifficultyColor(bot.GetDifficulty());
        }
        else
        {
            playerName.text = player.GetName();
            ApplyDefaultColor();
        }
    }

    private void ApplyDefaultColor()
    {
        if (difficultyImage == null) return;
        difficultyImage.color = Color.clear;
    }

    private void ApplyDifficultyColor(DartBotDifficulty diff)
    {
        if (difficultyImage == null) return;

        switch (diff)
        {
            case DartBotDifficulty.Easy:
                difficultyImage.color = new Color(0.4f, 0.85f, 0.4f); // light green
                break;
            case DartBotDifficulty.Medium:
                difficultyImage.color = Color.yellow;
                break;
            case DartBotDifficulty.Hard:
                difficultyImage.color = new Color(1f, 0.6f, 0f); // orange
                break;
            case DartBotDifficulty.Pro:
                difficultyImage.color = Color.red;
                break;
            default:
                difficultyImage.color = Color.clear;
                break;
        }
    }

    public void Rename()
    {
        list.RenameClicked(id);
        Debug.Log("Rename Player with ID: " + id);
    }

    public void Delete()
    {
        list.DeleteClicked(id);
    }
}