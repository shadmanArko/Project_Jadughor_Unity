using UnityEngine;

namespace Systems.MineSystem.InventorySystem.View
{
    public sealed class ItemCollectionVisualizerCanvasView : MonoBehaviour
    {
        [SerializeField] private RectTransform cardParent;

        public RectTransform CardParent =>
            cardParent != null ? cardParent : transform as RectTransform;

        public void AttachCard(ItemCollectableView view)
        {
            if (view == null)
                return;

            view.transform.SetParent(CardParent, false);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ReorderBottomToTop(
            System.Collections.Generic.IReadOnlyList<ItemCollectableView> views)
        {
            for (var i = 0; i < views.Count; i++)
            {
                if (views[i] != null)
                    views[i].transform.SetSiblingIndex(i);
            }
        }
    }
}
