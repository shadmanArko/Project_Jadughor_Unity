using System;
using Systems.MineSystem.Mine.Model;
using UniRx;
using Zenject;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    [Serializable]
    public class MineCellCrackVisualizerService : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;
        
        
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public void UpdateCellWallCrack(Cell cell)
        {
            //TODO: Show cell crack visuals
        }
        

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}