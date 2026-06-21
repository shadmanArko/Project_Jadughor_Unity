using System.Collections.Generic;
using Systems.MineSystem.Damage;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface ICellDamageService
    {
        void ApplyCellImpact(
            Vector3Int cellPosition,
            int wallDamage,
            float objectDamage,
            float overlapRadius,
            LayerMask targetLayers,
            HashSet<IDamageable> damaged);
    }
}
