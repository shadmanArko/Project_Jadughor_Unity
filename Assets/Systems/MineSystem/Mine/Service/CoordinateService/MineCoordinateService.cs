using System;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Service.CoordinateService
{
    public sealed class MineCoordinateService :
        IInitializable,
        IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly RuntimeDataScriptable _playerRuntime;
        private readonly MineCoordinateCanvasView _view;
        private readonly CompositeDisposable _disposables = new();

        public MineCoordinateService(
            MineModel mine,
            MineView mineView,
            RuntimeDataScriptable playerRuntime,
            MineCoordinateCanvasView view)
        {
            _mine = mine;
            _mineView = mineView;
            _playerRuntime = playerRuntime;
            _view = view;
        }

        public void Initialize()
        {
            _view.SetVisible(false);

            _mine.MineData
                .CombineLatest(
                    _playerRuntime.worldPosition,
                    ResolveCoordinate)
                .DistinctUntilChanged()
                .Subscribe(Present)
                .AddTo(_disposables);
        }

        private CoordinatePresentation ResolveCoordinate(
            MineData mineData,
            Vector2 worldPosition)
        {
            var tilemap = _mineView.wallTileMap;
            if (mineData == null || tilemap == null)
                return CoordinatePresentation.Hidden;

            var tileCell = tilemap.WorldToCell(worldPosition);
            var mineCell = mineData.GetCell(tileCell);
            if (mineCell == null)
                return CoordinatePresentation.Hidden;

            return new CoordinatePresentation(
                true,
                mineCell.Position.X,
                -mineCell.Position.Y);
        }

        private void Present(CoordinatePresentation presentation)
        {
            _view.SetVisible(presentation.Visible);
            if (presentation.Visible)
            {
                _view.Present(
                    presentation.X,
                    presentation.Depth);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private readonly struct CoordinatePresentation :
            IEquatable<CoordinatePresentation>
        {
            public static readonly CoordinatePresentation Hidden =
                new(false, 0, 0);

            public readonly bool Visible;
            public readonly int X;
            public readonly int Depth;

            public CoordinatePresentation(
                bool visible,
                int x,
                int depth)
            {
                Visible = visible;
                X = x;
                Depth = depth;
            }

            public bool Equals(CoordinatePresentation other)
            {
                return Visible == other.Visible &&
                       X == other.X &&
                       Depth == other.Depth;
            }

            public override bool Equals(object obj)
            {
                return obj is CoordinatePresentation other &&
                       Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Visible, X, Depth);
            }
        }
    }
}
