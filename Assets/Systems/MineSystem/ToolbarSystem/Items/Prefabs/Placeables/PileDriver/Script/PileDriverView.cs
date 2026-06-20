using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public sealed class PileDriverView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer core;
        [SerializeField] private SpriteRenderer extension;
        [SerializeField] private SpriteRenderer head;
        [SerializeField] private Animator coreAnimator;

        public SpriteRenderer Core => core;
        public SpriteRenderer Extension => extension;
        public SpriteRenderer Head => head;
        public Animator CoreAnimator => coreAnimator;
    }
}
