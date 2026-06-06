using System;
using UniRx;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    [Serializable]
    public class MinePlayerData
    {
        public ReactiveProperty<float> health;
        public ReactiveProperty<float> maxHealth;
        
        public ReactiveProperty<float> stamina;
        public ReactiveProperty<float> maxStamina;

        public ReactiveProperty<int> pickAxeStrength = new ReactiveProperty<int>(20);
        public ReactiveProperty<float> collectablePullRadius =
            new ReactiveProperty<float>(2.5f);
    }
}
