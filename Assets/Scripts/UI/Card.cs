using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Car : MonoBehaviour
{
    [SerializeField] Image image;
    [SerializeField] Image borderImage;
    [SerializeField] Image alignmentImage;
    [SerializeField] Image cardTypeImage;
    [SerializeField] TextMeshProUGUI description;
    [SerializeField] TextMeshProUGUI title;
    private Illustrations illustrations;
    private Colors colors;

    private CardData cardData;

    public void Initialize(CardData data)
    {
        cardData = data;
        if (data == null)
        {
            ClearVisuals();
            return;
        }

        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        if (colors == null) colors = FindFirstObjectByType<Colors>();

        if (title != null) title.text = data.name ?? string.Empty;
        CardTypeEnum cardType = data.GetCardType();
        string cardTypeKey = cardType.ToString();
        if (description != null)
        {
            string typeColorHex = "FFFFFF";
            if (TryGetCardTypeColor(cardType, out Color typeColor))
            {
                typeColorHex = ColorUtility.ToHtmlStringRGB(typeColor);
            }
            string jsonDescription = data.description ?? string.Empty;
            description.text = $"<color=\"#{typeColorHex}\">{cardTypeKey}</color>{jsonDescription}";
        }

        if (image != null)
        {
            image.sprite = ResolveCardImage(data);
        }

        AlignmentEnum alignmentValue = (AlignmentEnum)data.alignment;
        string alignmentKey = alignmentValue.ToString();
        if (alignmentImage != null) alignmentImage.sprite = GetSprite(alignmentKey);

        if (cardTypeImage != null) cardTypeImage.sprite = GetSprite(cardTypeKey);

        if (borderImage != null && TryGetCardTypeColor(cardType, out Color borderColor))
        {
            borderImage.color = borderColor;
        }
    }

    private bool TryGetCardTypeColor(CardTypeEnum cardType, out Color color)
    {
        color = Color.white;
        if (colors == null) return false;

        switch (cardType)
        {
            case CardTypeEnum.Action:
                color = colors.actionCard;
                return true;
            case CardTypeEnum.Army:
                color = colors.armyCard;
                return true;
            case CardTypeEnum.Character:
                color = colors.characterCard;
                return true;
            case CardTypeEnum.Event:
                color = colors.eventCard;
                return true;
            case CardTypeEnum.Encounter:
                color = colors.eventCard;
                return true;
            case CardTypeEnum.Land:
                color = colors.landCard;
                return true;
            case CardTypeEnum.PC:
                color = colors.pcCard;
                return true;
            case CardTypeEnum.Rest:
                color = colors.spellCard;
                return true;
            default:
            {
                return false;
            }
        }
    }

    private Sprite GetSprite(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || illustrations == null) return null;
        return illustrations.GetIllustrationByName(key);
    }

    private Sprite ResolveCardImage(CardData data)
    {
        if (data == null) return null;

        CardTypeEnum cardType = data.GetCardType();
        if (cardType == CardTypeEnum.Action || cardType == CardTypeEnum.Event || cardType == CardTypeEnum.Encounter)
        {
            return GetSprite(data.GetActionRef()) ?? GetSprite(data.name);
        }

        return GetSprite(data.name);
    }

    private void ClearVisuals()
    {
        if (title != null) title.text = string.Empty;
        if (description != null) description.text = string.Empty;

        if (image != null) image.sprite = null;
        if (borderImage != null) borderImage.sprite = null;
        if (alignmentImage != null) alignmentImage.sprite = null;
        if (cardTypeImage != null) cardTypeImage.sprite = null;
    }
}
