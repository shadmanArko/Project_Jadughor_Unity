using System;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
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
        private Action<IPlaceableRuntime> _releaseAction;
        private PileDriverController _controller;

        [Inject]
        public void Construct(
            MineModel mine,
            MineView mineView,
            PlayerView player)
        {
            _mine = mine;
            _mineView = mineView;
            _player = player;
        }

        public void Initialize(PlaceableSpawnContext context)
        {
            DisposeController();

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
            gameObject.SetActive(true);

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
            _releaseAction?.Invoke(this);
        }

        private void OnDisable()
        {
            DisposeController();
        }

        private void DisposeController()
        {
            _controller?.Dispose();
            _controller = null;
        }
    }
}
