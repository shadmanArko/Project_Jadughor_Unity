using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.Config
{
    [CreateAssetMenu(fileName = "MineTransitionConfig", menuName = "Config/Mine Transition Config")]
    public sealed class MineTransitionConfig : ScriptableObject
    {
        [Tooltip("Camp world position where the player begins entering the mine.")]
        public Vector2 campWalkTarget = new(-0.1f, 0.3f);
        [Tooltip("Grid cell above the mine entrance for climbing in or out.")]
        public Vector2Int mineEntryStartCell = new(0, 1);
        [Tooltip("Grid cell where the player lands after entering the mine.")]
        public Vector2Int mineLandingCell = new(0, -1);
        [Tooltip("Seconds spent walking from camp to the mine.")]
        [Min(0f)] public float campWalkDuration = 1.5f;
        [Tooltip("Seconds spent panning the camera to the mine.")]
        [Min(0f)] public float cameraPanDuration = 2f;
        [Tooltip("Seconds spent climbing into the mine from camp.")]
        [Min(0f)] public float mineEntryDuration = 1.5f;
        [Tooltip("Seconds spent climbing above the mine entrance before sleeping or leaving.")]
        [Min(0f)] public float mineExitDuration = 1.5f;
        [Tooltip("Seconds spent climbing back into the mine after sleeping.")]
        [Min(0f)] public float overnightReturnDuration = 1.5f;
        [Tooltip("Seconds spent descending to the tile below the entrance.")]
        [Min(0f)] public float backToMineDuration = 0.6f;
        [Tooltip("Easing used for scripted player movement in mine transitions.")]
        public Ease playerMovementEase = Ease.Linear;
    }
}
