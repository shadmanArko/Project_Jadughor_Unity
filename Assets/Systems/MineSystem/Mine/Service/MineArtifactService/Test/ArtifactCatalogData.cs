using System;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [Serializable]
    public sealed class ArtifactDefinition
    {
        public string Id;
        public string Era;
        public string Region;
        public string Object;
        public string[] Materials;
        public string ObjectClass;
        public string ObjectSize;
        public string LargeImageLocation;
        public string SmallImageLocation;
    }

    [Serializable]
    public sealed class ArtifactDescription
    {
        public string Id;
        public string ArtifactName;
        public string Description;
    }
}
