using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "MineDarkeningConfig",
        menuName = "Mine/Darkening Config")]
    public sealed class MineDarkeningConfig : ScriptableObject
    {
        [Tooltip("Darkening begins below this mine cell Y coordinate.")]
        public int fadeStartCellY = -5;

        [Tooltip("Darkening reaches maximum alpha at this cell Y coordinate.")]
        public int maxAlphaCellY = -15;

        [Range(0, 255)] public byte maxAlpha = 170;

        private void OnValidate()
        {
            if (maxAlphaCellY >= fadeStartCellY)
                maxAlphaCellY = fadeStartCellY - 1;
        }
    }
}
