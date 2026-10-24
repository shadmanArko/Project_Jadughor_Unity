using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Config
{
    [CreateAssetMenu(fileName = "FadeTransitionConfig", menuName = "Config/Mine Fade Transition")]
    public sealed class FadeTransitionConfig : ScriptableObject
    {
        [Tooltip("Seconds for the screen to become opaque.")]
        [Min(0f)] public float fadeInSeconds = 0.6f;
        [Tooltip("Seconds to show the next day while the screen is opaque.")]
        [Min(0f)] public float dayCardSeconds = 1.2f;
        [Tooltip("Seconds for the screen to become transparent again.")]
        [Min(0f)] public float fadeOutSeconds = 0.6f;
    }
}
