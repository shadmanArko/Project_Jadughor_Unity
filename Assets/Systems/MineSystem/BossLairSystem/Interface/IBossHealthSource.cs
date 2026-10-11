using UniRx;

namespace Systems.MineSystem.BossLairSystem.Interface
{
    /// <summary>
    /// Supplies the health data shown by the shared boss-lair UI.
    /// Boss implementations retain ownership of their health; the UI only
    /// observes these two reactive values.
    /// </summary>
    public interface IBossHealthSource
    {
        IReadOnlyReactiveProperty<int> CurrentHealth { get; }
        IReadOnlyReactiveProperty<int> MaximumHealth { get; }
    }
}
