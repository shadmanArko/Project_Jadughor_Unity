using System;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public sealed class PileDriverRuntime :
        MonoBehaviour,
        IPlaceableRuntime
    {
        [SerializeField] private PileDriverView view;

        private MineModel _mine;
        private MineView _mineView;
        private PlayerView _player;
        private PlaceableItemizationService _itemization;
        private Action<IPlaceableRuntime> _releaseAction;
        private PileDriverController _controller;
        private PlaceableDurabilityModel _durability;
        private PlaceableSpawnContext _context;

        public IPlaceableDamageView DamageView => view;

        [Inject]
        public void Construct(
            MineModel mine,
            MineView mineView,
            PlayerView player,
            PlaceableItemizationService itemization)
        {
            _mine = mine;
            _mineView = mineView;
            _player = player;
            _itemization = itemization;
        }

        public void Initialize(PlaceableSpawnContext context)
        {
            DisposeController();
            DisposeDurability();

            var profile = context.Profile as PileDriverActionProfile;
            if (profile == null ||
                profile.Config == null ||
                view == null)
            {
                Debug.LogError(
                    "PileDriver requires a PileDriverActionProfile and view.",
                    this);
                return;
            }

            transform.position = context.WorldPosition;
            _context = context;
            gameObject.SetActive(true);
            _durability = new PlaceableDurabilityModel(
                view,
                profile.MaxHealth,
                Release);
            view.ConfigureItemization(() =>
                _itemization.TryConvert(
                    _context,
                    transform.position));

            var model = new PileDriverModel(
                _mine,
                context,
                profile.Config);
            _controller = new PileDriverController(
                model,
                view,
                profile.Config,
                _mine,
                _mineView,
                _player);
            _controller.Start(context.PileDriverDirection);
        }

        public void SetReleaseAction(
            Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public void Release()
        {
            DisposeController();
            DisposeDurability();
            _releaseAction?.Invoke(this);
        }

        private void OnDisable()
        {
            DisposeController();
            DisposeDurability();
        }

        private void DisposeController()
        {
            _controller?.Dispose();
            _controller = null;
        }

        private void DisposeDurability()
        {
            _durability?.Dispose();
            _durability = null;
            view?.ClearItemization();
        }
    }
}
