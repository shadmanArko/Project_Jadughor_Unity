using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "SpecialBackdropSprite", menuName = "Scriptable/SpecialBackdropSpriteScriptable")]
    public class SpecialBackdropSpriteScriptable : ScriptableObject
    {
        public List<SpecialBackdropSprite> specialBackdropSprites;

        public List<string> GetAllIds()
        {
            return specialBackdropSprites.Select(sprite => sprite.id).ToList();
        }
    }
}