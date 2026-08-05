using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.UI
{
    /// <summary>
    /// Left-to-right flow layout that wraps to new rows — for variable-width items
    /// like tag chips, which a GridLayoutGroup (fixed cell size) or a
    /// HorizontalLayoutGroup (single row, no wrap) can't handle. Each child is placed
    /// at its own PREFERRED width/height, wrapping when it would overflow this rect's
    /// width; the group's preferred height grows to fit all rows.
    ///
    /// Put this on the tags container; give each chip a layout that reports a
    /// preferred width (e.g. a HorizontalLayoutGroup around its label). Pair with a
    /// ContentSizeFitter (Vertical: Preferred) on this container if it should grow
    /// vertically to fit the wrapped rows.
    /// </summary>
    public class FlowLayoutGroup : LayoutGroup
    {
        [SerializeField] private float spacingX = 4f;
        [SerializeField] private float spacingY = 4f;

        private float _preferredHeight;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            // Min width = the widest single child (so wrapping is always possible);
            // preferred width = same (the parent decides the actual width).
            float widest = 0f;
            for (int i = 0; i < rectChildren.Count; i++)
                widest = Mathf.Max(widest, LayoutUtility.GetPreferredSize(rectChildren[i], 0));

            float pad = padding.horizontal;
            SetLayoutInputForAxis(pad + widest, pad + widest, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            // Width is known by now (set in the horizontal pass), so we can measure
            // how the children wrap and report the resulting preferred height.
            Place(apply: false);
            SetLayoutInputForAxis(_preferredHeight, _preferredHeight, -1, 1);
        }

        public override void SetLayoutHorizontal() => Place(apply: true);
        public override void SetLayoutVertical() => Place(apply: true);

        private void Place(bool apply)
        {
            float available = rectTransform.rect.width - padding.horizontal;
            float x = padding.left;
            float y = padding.top;
            float rowHeight = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                float w = LayoutUtility.GetPreferredSize(child, 0);
                float h = LayoutUtility.GetPreferredSize(child, 1);

                // Wrap to a new row if this child would overflow (but never on an empty row).
                if (x > padding.left && x - padding.left + w > available)
                {
                    x = padding.left;
                    y += rowHeight + spacingY;
                    rowHeight = 0f;
                }

                if (apply)
                {
                    SetChildAlongAxis(child, 0, x, w);
                    SetChildAlongAxis(child, 1, y, h);
                }

                x += w + spacingX;
                rowHeight = Mathf.Max(rowHeight, h);
            }

            _preferredHeight = y + rowHeight + padding.bottom;
        }
    }
}
