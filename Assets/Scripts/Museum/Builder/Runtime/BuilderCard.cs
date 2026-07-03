using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// One clickable object card in the builder panel. Put this on the card prefab
    /// with an icon <see cref="Image"/>, a name label, an optional price label and a
    /// <see cref="Button"/>. Clicking raises <see cref="BuilderActions.OnClickBuilderCard"/>.
    /// </summary>
    public class BuilderCard : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel; // optional
        [SerializeField] private Button button;

        private BuilderCardType _type;
        private string _cardName;

        /// <summary>Configure the card from its data (placeholder used if icon is null).</summary>
        public void Setup(BuilderCardData data, Sprite placeholder)
        {
            _type = data.Type;
            _cardName = data.CardName;

            if (icon != null)
            {
                Sprite sprite = data.Icon != null ? data.Icon : placeholder;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            if (nameLabel != null) nameLabel.text = data.CardName;
            if (priceLabel != null) priceLabel.text = data.PriceText;

            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            BuilderActions.OnClickBuilderCard?.Invoke(_type, _cardName);
        }
    }
}
