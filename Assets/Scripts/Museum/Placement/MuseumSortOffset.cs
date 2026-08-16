using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Marks a SpriteRenderer to draw at (its object's museum-sort order + offset)
    /// instead of the object's base order. Used so exhibit artifacts render in FRONT
    /// of the exhibit body (offset +1) while still Y-sorting among themselves.
    /// Read by <see cref="MuseumSortingSystem"/> when it assigns sorting orders.
    /// </summary>
    public class MuseumSortOffset : MonoBehaviour
    {
        [Tooltip("Added to the owning object's sorting order for this renderer.")]
        public int offset;
    }
}
