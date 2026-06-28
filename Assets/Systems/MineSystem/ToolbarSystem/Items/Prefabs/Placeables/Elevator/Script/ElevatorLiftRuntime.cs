using System;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorLiftRuntime : MonoBehaviour, IPlaceableRuntime
    {
        [SerializeField] private ElevatorView view;

        private ElevatorNetworkService _network;
        private PlaceableItemizationService _itemization;
        private PlaceableRuntimeRegistry _registry;
        private PlaceableDurabilityModel _durability;
        private Action<IPlaceableRuntime> _releaseAction;
        private PlaceableSpawnContext _context;

        public IPlaceableDamageView DamageView => view;
        public Vector3Int CellPosition { get; private set; }

        [Inject]
        public void Construct(
            ElevatorNetworkService network,
            PlaceableItemizationService itemization,
            PlaceableRuntimeRegistry registry)
        {
            _network = network;
            _itemization = itemization;
            _registry = registry;
        }

        public void Initialize(PlaceableSpawnContext context)
        {
            DisposeDurability();
            _context = context;
            CellPosition = context.CellPosition;
            transform.position = context.WorldPosition;
            gameObject.SetActive(true);

            var profile = context.Profile as ElevatorActionProfile;
            view = EnsureView();
            view.Configure(ElevatorPlaceableKind.Lift, profile?.Config);
            view.ConfigureItemization(() =>
            {
                if (!_itemization.TryConvert(_context, transform.position))
                    return false;

                return true;
            });

            _durability = new PlaceableDurabilityModel(
                view,
                context.Profile.MaxHealth,
                Release);
            _network.RegisterLift(this, profile?.Config);
        }

        public void SetCurrentCell(Vector3Int cell)
        {
            var previous = CellPosition;
            CellPosition = cell;
            _registry.RegisterCell(this, cell);
            _network.NotifyLiftCellChanged(this, previous, cell);
        }

        public void SetReleaseAction(Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public void Release()
        {
            _network.UnregisterLift(this);
            DisposeDurability();
            _releaseAction?.Invoke(this);
        }

        private ElevatorView EnsureView()
        {
            if (view != null)
                return view;

            view = GetComponent<ElevatorView>();
            if (view != null)
                return view;

            return gameObject.AddComponent<ElevatorView>();
        }

        private void OnDisable()
        {
            DisposeDurability();
        }

        private void DisposeDurability()
        {
            _durability?.Dispose();
            _durability = null;
            view?.ClearItemization();
        }
    }
}
