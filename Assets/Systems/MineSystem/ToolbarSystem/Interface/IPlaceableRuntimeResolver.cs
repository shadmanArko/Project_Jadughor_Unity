using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableRuntimeResolver
    {
        bool TryResolve(
            Vector3Int cellPosition,
            out IPlaceableRuntime runtime);
    }
}
