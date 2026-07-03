using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    public interface IArtifactCatalog
    {
        IReadOnlyList<ArtifactDefinition> Definitions { get; }
        bool TryGetDefinition(string definitionId, out ArtifactDefinition definition);
        bool TryGetDescription(string definitionId, out ArtifactDescription description);
        ArtifactDefinition GetDefinition(string definitionId);
        ArtifactDescription GetDescription(string definitionId);
    }
}
