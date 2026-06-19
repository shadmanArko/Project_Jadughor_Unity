using System;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public class PlaceableRuntime : MonoBehaviour, IPlaceableRuntime
    {
        private Action<IPlaceableRuntime> _releaseAction;
        public PlaceableSpawnContext Context { get; private set; }

        public virtual void Initialize(PlaceableSpawnContext context)
        {
            Context = context;
            transform.position = context.WorldPosition;
            gameObject.SetActive(true);
        }

        public void SetReleaseAction(Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public virtual void Release()
        {
            _releaseAction?.Invoke(this);
        }
    }
}
