using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Flat, UI-ready description of one builder card. The panel consumes only this —
    /// it unifies the JSON-backed categories with the model-less Flooring case and
    /// hides each category's differing field names behind three simple values.
    /// </summary>
    public struct BuilderCardData
    {
        public BuilderCardType Type;
        public string CardName;   // identity sent back on click (VariationName / SanitationId / tile name)
        public string PriceText;  // pre-formatted for display ("" if none)
        public Sprite Icon;       // first-frame sprite, or null → panel uses a placeholder

        public BuilderCardData(BuilderCardType type, string cardName, string priceText, Sprite icon)
        {
            Type = type;
            CardName = cardName;
            PriceText = priceText;
            Icon = icon;
        }
    }
}
