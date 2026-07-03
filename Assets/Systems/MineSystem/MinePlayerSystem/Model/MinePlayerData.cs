using System;
using UniRx;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    [Serializable]
    public class MinePlayerData
    {
        public ReactiveProperty<float> health = new();
        public ReactiveProperty<float> maxHealth = new();
        
        public ReactiveProperty<float> stamina = new();
        public ReactiveProperty<float> maxStamina = new();

        public ReactiveProperty<float> moveSpeed = new();
        public ReactiveProperty<float> climbSpeed = new();
        public ReactiveProperty<float> miningSpeed = new();
        public ReactiveProperty<float> attackSpeed = new();

        public ReactiveProperty<int> pickAxeStrength = new(20);
        public ReactiveProperty<float> collectablePullRadius = new(2.5f);
        public ReactiveProperty<int> unlockedInventorySlots = new(12);
    }
}
