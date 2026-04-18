using System;

namespace Systems.Utilities.EventBus
{
    public static class GlobalEventBus
    {
        private static readonly EventBus _instance = new();

        public static void Fire<TSignal>(TSignal signal) where TSignal : struct
            => _instance.Fire(signal);

        public static void Fire<TSignal>() where TSignal : struct
            => _instance.Fire<TSignal>();

        public static IObservable<TSignal> OnSignal<TSignal>() where TSignal : struct
            => _instance.OnSignal<TSignal>();

        /// <summary>Only call this on full application quit.</summary>
        public static void Shutdown() => _instance.Dispose();
    }
}