using UnityEngine;

namespace Systems.MineSystem.NotificationSystem.Config
{
    [CreateAssetMenu(fileName = "NotificationConfig",
        menuName = "Mine/Notification Config")]
    public sealed class NotificationConfig : ScriptableObject
    {
        [Min(0f)] public float visibleDuration = 3f;
        [Min(0f)] public float fadeOutDuration = 0.5f;

        private void OnValidate()
        {
            visibleDuration = Mathf.Max(0f, visibleDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        }
    }
}
