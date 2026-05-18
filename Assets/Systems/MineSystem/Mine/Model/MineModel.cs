using System;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class MineModel : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;
        private MinePlayerScriptable _playerScriptable;
        
        private ReactiveProperty<MineData> _mineData = new();
        public IReadOnlyReactiveProperty<MineData> MineData => _mineData;

        public MineModel(MinePlayerScriptable playerScriptable)
        {
            _playerScriptable = playerScriptable;
        }
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public void SetMineData(MineData mineData)
        {
            _mineData.Value = mineData;
        }

        public void HitWall(Vector3Int cellPos, Action<Vector3Int> onBreak = null)
        {
            var cell = _mineData.Value.GetCell(cellPos);
            
            if (cell == null)
            {
                Debug.LogWarning($"Cell not available: {cellPos}");
                return;
            }

            cell.HitPoint -= _playerScriptable.playerData.pickAxeStrength.Value;
            if (cell.HitPoint > 0) return;
            cell.HitPoint = 0;
            cell.IsBroken = true;
            //TODO: make resource, artifact null after spawning those as items
            onBreak?.Invoke(cellPos);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}