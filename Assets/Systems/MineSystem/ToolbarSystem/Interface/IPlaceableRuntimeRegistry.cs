using System;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableRuntimeRegistry
    {
        event Action<Vector3Int, IPlaceableRuntime> RuntimeRegistered;
        event Action<Vector3Int, IPlaceableRuntime> RuntimeUnregistered;

        bool Contains<T>(Vector3Int cellPosition)
            where T : class, IPlaceableRuntime;
    }
}
