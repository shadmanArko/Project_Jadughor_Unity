using Core.EventBus;

namespace InputSystem.Events
{
    /// <summary>
    /// Publish this event from anywhere in the game to request haptic feedback
    /// on the currently active gamepad.
    ///
    /// HapticsController listens for this and delegates to HapticsService.
    ///
    /// Example:
    ///   _eventBus.Publish(new HapticRequestEvent(lowFreq: 0.5f, highFreq: 0.8f, duration: 0.3f));
    /// </summary>
    public sealed class HapticRequestEvent : IEvent
    {
        /// <summary>Low-frequency motor intensity (0–1). Controls the heavy rumble.</summary>
        public float LowFrequency  { get; }

        /// <summary>High-frequency motor intensity (0–1). Controls the fine vibration.</summary>
        public float HighFrequency { get; }

        /// <summary>
        /// Optional duration in seconds. After this time, rumble fades out.
        /// Pass null for indefinite rumble (call StopHaptics event to stop).
        /// </summary>
        public float? Duration { get; }

        public HapticRequestEvent(float lowFreq, float highFreq, float? duration = null)
        {
            LowFrequency  = lowFreq;
            HighFrequency = highFreq;
            Duration      = duration;
        }
    }
}
