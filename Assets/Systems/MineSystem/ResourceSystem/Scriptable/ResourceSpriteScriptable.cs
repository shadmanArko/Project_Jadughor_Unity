using System.Collections.Generic;
using Systems.MineSystem.ResourceSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ResourceSystem.Scriptable
{
    [CreateAssetMenu(fileName = "ResourceSpriteScriptable", menuName = "Scriptable/ResourceSpriteScriptable")]
    public class ResourceSpriteScriptable : ScriptableObject
    {
        public List<ResourceSpriteData> resourceSpriteDatas;
    }
}