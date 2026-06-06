using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    public sealed class ArtifactCatalog : IArtifactCatalog, IInitializable
    {
        private const string DefaultFunctionalPath =
            "ArtifactCatalog/RawArtifactFunctionalData";

        private const string DefaultDescriptivePath =
            "ArtifactCatalog/RawArtifactDescriptiveDataEnglish";

        [Serializable]
        private sealed class DefinitionArray
        {
            public ArtifactDefinition[] Items;
        }

        [Serializable]
        private sealed class DescriptionArray
        {
            public ArtifactDescription[] Items;
        }

        private readonly ArtifactCatalogConfig _config;
        private readonly Dictionary<string, ArtifactDefinition> _definitions = new();
        private readonly Dictionary<string, ArtifactDescription> _descriptions = new();
        private readonly List<ArtifactDefinition> _definitionList = new();

        public IReadOnlyList<ArtifactDefinition> Definitions => _definitionList;

        public ArtifactCatalog(ArtifactCatalogConfig config)
        {
            _config = config;
        }

        public void Initialize()
        {
            LoadDefinitions();
            LoadDescriptions();
            ValidateRelationships();
        }

        public bool TryGetDefinition(string definitionId, out ArtifactDefinition definition)
        {
            return _definitions.TryGetValue(definitionId, out definition);
        }

        public bool TryGetDescription(string definitionId, out ArtifactDescription description)
        {
            return _descriptions.TryGetValue(definitionId, out description);
        }

        public ArtifactDefinition GetDefinition(string definitionId)
        {
            if (TryGetDefinition(definitionId, out var definition))
                return definition;

            throw new KeyNotFoundException($"Artifact definition '{definitionId}' was not found.");
        }

        public ArtifactDescription GetDescription(string definitionId)
        {
            if (TryGetDescription(definitionId, out var description))
                return description;

            throw new KeyNotFoundException($"Artifact description '{definitionId}' was not found.");
        }

        private void LoadDefinitions()
        {
            var asset = _config.FunctionalData ??
                        Resources.Load<TextAsset>(DefaultFunctionalPath);
            var wrapper = ParseDefinitions(asset);
            if (wrapper?.Items == null)
                return;

            foreach (var definition in wrapper.Items)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    Debug.LogError("Artifact catalog contains a functional record without an Id.");
                    continue;
                }

                if (_definitions.ContainsKey(definition.Id))
                {
                    Debug.LogError($"Artifact catalog contains duplicate functional Id '{definition.Id}'.");
                    continue;
                }

                _definitions.Add(definition.Id, definition);
                _definitionList.Add(definition);
            }
        }

        private void LoadDescriptions()
        {
            var asset = _config.DescriptiveData ??
                        Resources.Load<TextAsset>(DefaultDescriptivePath);
            var wrapper = ParseDescriptions(asset);
            if (wrapper?.Items == null)
                return;

            foreach (var description in wrapper.Items)
            {
                if (description == null || string.IsNullOrWhiteSpace(description.Id))
                {
                    Debug.LogError("Artifact catalog contains a descriptive record without an Id.");
                    continue;
                }

                if (_descriptions.ContainsKey(description.Id))
                {
                    Debug.LogError($"Artifact catalog contains duplicate descriptive Id '{description.Id}'.");
                    continue;
                }

                _descriptions.Add(description.Id, description);
            }
        }

        private void ValidateRelationships()
        {
            foreach (var definitionId in _definitions.Keys)
            {
                if (!_descriptions.ContainsKey(definitionId))
                    Debug.LogWarning($"Artifact '{definitionId}' has no localized description.");
            }

            foreach (var descriptionId in _descriptions.Keys)
            {
                if (!_definitions.ContainsKey(descriptionId))
                    Debug.LogWarning($"Artifact description '{descriptionId}' has no functional definition.");
            }
        }

        private static DefinitionArray ParseDefinitions(TextAsset asset)
        {
            if (asset == null)
                throw new InvalidOperationException("Artifact functional data TextAsset is not assigned.");

            return JsonUtility.FromJson<DefinitionArray>(WrapArray(asset.text));
        }

        private static DescriptionArray ParseDescriptions(TextAsset asset)
        {
            if (asset == null)
                throw new InvalidOperationException("Artifact descriptive data TextAsset is not assigned.");

            return JsonUtility.FromJson<DescriptionArray>(WrapArray(asset.text));
        }

        private static string WrapArray(string json)
        {
            return "{\"Items\":" + json + "}";
        }
    }
}
