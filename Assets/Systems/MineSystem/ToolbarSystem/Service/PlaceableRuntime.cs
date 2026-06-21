using System;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using Systems.MineSystem.ToolbarSystem.Interface;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public class PlaceableRuntime : MonoBehaviour, IPlaceableRuntime
    {
        private Action<IPlaceableRuntime> _releaseAction;
        private PlaceableItemizationService _itemization;
        private PlaceableDurabilityModel _durability;
        private IPlaceableDamageView _damageView;

        public PlaceableSpawnContext Context { get; private set; }
        public IPlaceableDamageView DamageView => _damageView;

        [Inject]
        public void Construct(PlaceableItemizationService itemization)
        {
            _itemization = itemization;
        }

        public virtual void Initialize(PlaceableSpawnContext context)
        {
            DisposeDurability();
            Context = context;
            _damageView = FindDamageView();
            transform.position = context.WorldPosition;
            gameObject.SetActive(true);

            if (_damageView == null)
            {
                Debug.LogError(
                    $"Placeable '{context.PlaceableId}' requires an IPlaceableDamageView.",
                    this);
                return;
            }

            _durability = new PlaceableDurabilityModel(
                _damageView,
                context.Profile.MaxHealth,
                Release);
            if (_damageView is View.PlaceableDamageView view)
            {
                view.ConfigureItemization(() =>
                    _itemization.TryConvert(
                        Context,
                        transform.position));
            }
        }

        public void SetReleaseAction(Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public virtual void Release()
        {
            DisposeDurability();
            _releaseAction?.Invoke(this);
        }

        protected virtual void OnDisable()
        {
            DisposeDurability();
        }

        private IPlaceableDamageView FindDamageView()
        {
            foreach (var behaviour in
                     GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IPlaceableDamageView view)
                    return view;
            }

            return null;
        }

        private void DisposeDurability()
        {
            _durability?.Dispose();
            _durability = null;
            if (_damageView is View.PlaceableDamageView view)
                view.ClearItemization();
        }
    }
}
