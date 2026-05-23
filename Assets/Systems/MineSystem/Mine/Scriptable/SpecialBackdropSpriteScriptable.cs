using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Database;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "SpecialBackdropSprite", menuName = "Scriptable/SpecialBackdropSpriteScriptable")]
    public class SpecialBackdropSpriteScriptable : ScriptableObject
    {
        public List<SpecialBackdropSpriteData> specialBackdropSpriteDatas;

        public List<string> GetAllIds(Region region, Site site)
        {
            var spriteData = specialBackdropSpriteDatas.FirstOrDefault(data => data.region == region && data.site == site);
            if (spriteData != null) return spriteData.specialBackdropSprites.Select(sprite => sprite.id).ToList();
            Debug.LogError($"Fatal Error: Sprite Data could not be found for region {region} and site {site}");
            return null;
        }
    }
}