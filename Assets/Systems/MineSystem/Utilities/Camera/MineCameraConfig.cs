using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.Utilities.Camera
{
    [CreateAssetMenu(fileName = "MineCameraConfig", menuName = "Config/Mine Camera Config")]
    public sealed class MineCameraConfig : ScriptableObject
    {
        [Min(0.01f)] public float orthographicSize = 2f;
        [Min(0f)] public float confinerDamping;
        [Min(0f)] public float defaultPanDuration = 2f;
        public Ease panEase = Ease.InOutSine;
    }
}
