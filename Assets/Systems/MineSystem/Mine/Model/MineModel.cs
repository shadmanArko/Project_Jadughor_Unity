using System;
using UniRx;
using Zenject;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class MineModel : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;
        
        private ReactiveProperty<MineData> _mineData;
        public IReadOnlyReactiveProperty<MineData> MineData => _mineData;
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            _mineData = new ReactiveProperty<MineData>();
        }

        public void SetMineData(MineData mineData)
        {
            _mineData.Value = mineData;
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}