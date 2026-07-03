using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Database;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "VineSpriteScriptable", menuName = "Scriptable/VineSpriteScriptable")]
    public sealed class VineSpriteScriptable : ScriptableObject
    {
        public List<VineSpriteData> vineSpriteDatas;

        public List<string> GetAllIds(Region region, Site site)
        {
            var spriteData = vineSpriteDatas?.FirstOrDefault(data => data.region == region && data.site == site);
            if (spriteData != null) return spriteData.vineSprites.Select(sprite => sprite.id).ToList();
            Debug.LogError($"Fatal Error: Vine sprite data could not be found for region {region} and site {site}");
            return null;
        }
    }
}
