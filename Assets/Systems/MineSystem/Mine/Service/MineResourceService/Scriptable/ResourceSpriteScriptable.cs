using System.Collections.Generic;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineResourceService.Scriptable
{
    [CreateAssetMenu(fileName = "ResourceSpriteScriptable", menuName = "Scriptable/ResourceSpriteScriptable")]
    public class ResourceSpriteScriptable : ScriptableObject
    {
        public List<ResourceSpriteData> resourceSpriteDatas;
    }
}