using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// All artifacts as one editable asset — descriptive + functional data paired
    /// with a UI icon <see cref="Sprite"/> resolved at import time (icons live under
    /// Assets/2D, not Resources, so they can't be loaded by path at runtime).
    /// Populated by <c>Tools ▸ Project Museum ▸ Import Artifact Data</c>.
    /// Mirrors <c>BuilderDatabase</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "MuseumArtifactDatabase", menuName = "Project Museum/Artifact Database")]
    public class MuseumArtifactDatabase : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public ArtifactDescriptive Descriptive;
            public ArtifactFunctional Functional;
            [Tooltip("UI card/slot icon — from Assets/2D/UI/MineUi/Artifacts (by ArtifactName).")]
            public Sprite Icon;
            [Tooltip("In-game isometric sprite shown on the physical exhibit — from " +
                     "Assets/2D/Museum/Isometric View Artifacts (by Id). Used later " +
                     "when placed artifacts render in the museum.")]
            public Sprite IsometricSprite;

            public string Id => Descriptive != null ? Descriptive.Id : null;
            public string Name => Descriptive != null ? Descriptive.ArtifactName : Id;
            public List<string> Tags => Functional != null ? Functional.BuildTags() : new List<string>();
        }

        [SerializeField] private List<Entry> artifacts = new List<Entry>();

        private Dictionary<string, Entry> _byId;

        public IReadOnlyList<Entry> Artifacts => artifacts;

        public Entry GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null || _byId.Count != artifacts.Count)
            {
                _byId = new Dictionary<string, Entry>(artifacts.Count);
                foreach (Entry e in artifacts)
                    if (e?.Id != null) _byId[e.Id] = e;
            }
            return _byId.TryGetValue(id, out Entry entry) ? entry : null;
        }

#if UNITY_EDITOR
        public void SetArtifacts(List<Entry> v) { artifacts = v; _byId = null; }
#endif
    }
}
