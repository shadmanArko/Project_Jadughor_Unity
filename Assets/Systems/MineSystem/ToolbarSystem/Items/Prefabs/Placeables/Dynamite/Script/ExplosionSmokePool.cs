using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class ExplosionSmokePool :
        MonoMemoryPool<ExplosionSmokeView>
    {
        protected override void OnDespawned(ExplosionSmokeView item)
        {
            item.ResetView();
            base.OnDespawned(item);
        }
    }
}
