using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [CreateAssetMenu(
        fileName = "ArtifactSpriteScriptable",
        menuName = "Scriptable/Artifact Sprite Scriptable")]
    public sealed class ArtifactSpriteScriptable : ScriptableObject
    {
        [SerializeField]
        private List<ArtifactSpriteData> artifactSpriteDatas = new();

        public IReadOnlyList<ArtifactSpriteData> ArtifactSpriteDatas =>
            artifactSpriteDatas;

        public bool TryGetArtifact(
            string definitionId,
            Region region,
            Site site,
            out ArtifactSpriteEntry artifact)
        {
            artifact = null;
            if (string.IsNullOrEmpty(definitionId))
                return false;

            var locationData = artifactSpriteDatas.FirstOrDefault(data =>
                data.region == region && data.site == site);

            artifact = locationData?.artifacts?.FirstOrDefault(entry =>
                entry != null && entry.definitionId == definitionId);

            if (artifact != null)
                return true;

            artifact = artifactSpriteDatas
                .Where(data => data.region == region)
                .SelectMany(data => data.artifacts ?? new List<ArtifactSpriteEntry>())
                .FirstOrDefault(entry =>
                    entry != null && entry.definitionId == definitionId);

            return artifact != null;
        }

        public Sprite GetWorldSprite(
            string definitionId,
            Region region,
            Site site)
        {
            return TryGetArtifact(definitionId, region, site, out var artifact)
                ? artifact.GetWorldSprite()
                : null;
        }

        public Sprite GetInventorySprite(
            string definitionId,
            Region region,
            Site site)
        {
            if (!TryGetArtifact(definitionId, region, site, out var artifact))
                return null;

            return artifact.inventorySprite != null
                ? artifact.inventorySprite
                : artifact.GetWorldSprite();
        }

        public Sprite GetDetailSprite(
            string definitionId,
            Region region,
            Site site)
        {
            if (!TryGetArtifact(definitionId, region, site, out var artifact))
                return null;

            return artifact.detailSprite != null
                ? artifact.detailSprite
                : GetInventorySprite(definitionId, region, site);
        }
    }
}
