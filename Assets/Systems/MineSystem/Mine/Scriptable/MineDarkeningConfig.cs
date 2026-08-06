using System;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "MineDarkeningConfig",
        menuName = "Mine/Darkening Config")]
    public sealed class MineDarkeningConfig : ScriptableObject
    {
        [Header("Depth ramp")]
        [Tooltip("Darkening begins below this mine cell Y coordinate.")]
        public int fadeStartCellY = -5;

        [Tooltip("Darkening reaches maximum at this cell Y coordinate.")]
        public int maxAlphaCellY = -15;

        [Header("Ambient light")]
        [Tooltip("Global Light 2D intensity at or above fadeStartCellY.")]
        [Range(0f, 2f)] public float surfaceAmbientIntensity = 0.85f;

        [Tooltip("Global Light 2D intensity at or below maxAlphaCellY. " +
                 "This is the main 'how dark is the mine' dial.")]
        [Range(0f, 2f)] public float deepAmbientIntensity = 0.2f;

        [Tooltip("Tints the darkness. Multiplied over every lit sprite, so keep " +
                 "it bright enough that the intensity values above stay in charge " +
                 "of how dark the mine reads.")]
        [ColorUsage(false)]
        public Color ambientColor = new(0.55f, 0.47f, 0.85f);

        [Header("Deprecated")]
        [Tooltip("Unused since the darkening quad was replaced by Global Light 2D. " +
                 "Kept so existing assets do not lose their serialized value.")]
        [Range(0, 255)] public byte maxAlpha = 200;

        private readonly Subject<Unit> _validated = new();

        public IObservable<Unit> ObserveChanged() => _validated;

        private void OnValidate()
        {
            if (maxAlphaCellY >= fadeStartCellY)
                maxAlphaCellY = fadeStartCellY - 1;
            _validated.OnNext(Unit.Default);
        }
    }
}
