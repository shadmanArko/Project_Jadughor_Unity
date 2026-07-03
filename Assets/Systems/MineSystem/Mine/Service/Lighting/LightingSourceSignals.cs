using Systems.MineSystem.Mine.Service.Lighting;

namespace Systems.MineSystem.Mine.Signal
{
    public readonly struct LightingSourceRegisteredSignal
    {
        public readonly LightingSourceReporter Source;

        public LightingSourceRegisteredSignal(LightingSourceReporter source)
        {
            Source = source;
        }
    }

    public readonly struct LightingSourceActivationChangedSignal
    {
        public readonly LightingSourceReporter Source;
        public readonly bool IsActive;

        public LightingSourceActivationChangedSignal(
            LightingSourceReporter source,
            bool isActive)
        {
            Source = source;
            IsActive = isActive;
        }
    }

    public readonly struct LightingSourceUnregisteredSignal
    {
        public readonly LightingSourceReporter Source;

        public LightingSourceUnregisteredSignal(LightingSourceReporter source)
        {
            Source = source;
        }
    }
}
