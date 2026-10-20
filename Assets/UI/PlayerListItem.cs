
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerListItem : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text playerName;
    public GameObject playerImage;
    public GameObject botImage;

    private PlayerList list;
    private Guid id;

    public Guid ID => id;

    public void Initialize(PlayerList list)
    {
        this.list = list;
    }

    public void SetData(BasePlayer player)
    {
        if (player == null)
            return;

        id = player.GetID();

        if (playerName != null)
        {
            if (player is DartBot bot)
                playerName.text = bot.GetNameWithDifficulty();
            else
                playerName.text = player.GetName();
        }

        bool isBot = player is DartBot;

        if (playerImage != null)
            playerImage.SetActive(!isBot);

        if (botImage != null)
            botImage.SetActive(isBot);

        if (isBot)
            ApplyDifficultyColor(((DartBot)player).GetDifficulty());
    }

    private void ApplyDifficultyColor(DartBotDifficulty diff)
    {
        if (botImage == null)
            return;

        Image image = botImage.GetComponent<Image>();

        if (image == null)
            return;

        switch (diff)
        {
            case DartBotDifficulty.Easy:
                image.color = new Color(0.4f, 0.85f, 0.4f);
                break;

            case DartBotDifficulty.Medium:
                image.color = Color.yellow;
                break;

            case DartBotDifficulty.Hard:
                image.color = new Color(1f, 0.6f, 0f);
                break;

            case DartBotDifficulty.Pro:
                image.color = Color.red;
                break;

            default:
                image.color = Color.clear;
                break;
        }
    }

    public void Rename()
    {
        list.RenameClicked(id);
    }

    public void Delete()
    {
        list.DeleteClicked(id);
    }
}
