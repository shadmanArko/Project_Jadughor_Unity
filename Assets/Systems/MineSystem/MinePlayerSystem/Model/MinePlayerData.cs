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
    }
}