using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.Config
{
    [CreateAssetMenu(fileName = "MineTransitionConfig", menuName = "Config/Mine Transition Config")]
    public sealed class MineTransitionConfig : ScriptableObject
    {
        public Vector2 campWalkTarget = new(-0.1f, 0.3f);
        public Vector2Int mineEntryStartCell = new(0, 1);
        public Vector2Int mineLandingCell = new(0, -1);
        [Min(0f)] public float campWalkDuration = 1.5f;
        [Min(0f)] public float cameraPanDuration = 2f;
        [Min(0f)] public float mineEntryDuration = 1.5f;
        public Ease playerMovementEase = Ease.Linear;
    }
}
