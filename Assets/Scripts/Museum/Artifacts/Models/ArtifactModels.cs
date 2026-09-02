using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Text data for an artifact — from RawArtifactDescriptiveDataEnglish.json.
    /// [Serializable]/PascalCase to match the JSON keys (JsonUtility).
    /// </summary>
    [Serializable]
    public class ArtifactDescriptive
    {
        // Merge key for the JSON only — surfaced on MuseumArtifactDatabase.Entry.Id,
        // so it stays hidden here to avoid showing the same id three times.
        [HideInInspector] public string Id;
        public string ArtifactName;
        public string Description;
    }

    /// <summary>
    /// Functional data for an artifact — from RawArtifactFunctionalData.json.
    /// Provides the tags shown on cards (Era, Region, Object, ObjectSize, Materials).
    /// The JSON's image-location strings are intentionally NOT kept — they were stale
    /// Godot res:// paths; the actual sprites are resolved into the database at import
    /// time (Entry.Icon / Entry.IsometricSprite) instead.
    /// </summary>
    [Serializable]
    public class ArtifactFunctional
    {
        // See ArtifactDescriptive.Id — hidden for the same reason.
        [HideInInspector] public string Id;
        public string Era;
        public string Region;
        public string Object;
        public string[] Materials;
        public string ObjectClass;
        public string ObjectSize;

        /// <summary>Tags for the card, in Godot order: Era, Region, Object, ObjectSize, then each Material.</summary>
        public List<string> BuildTags()
        {
            var tags = new List<string>(5 + (Materials?.Length ?? 0));
            if (!string.IsNullOrEmpty(Era)) tags.Add(Era);
            if (!string.IsNullOrEmpty(Region)) tags.Add(Region);
            if (!string.IsNullOrEmpty(Object)) tags.Add(Object);
            if (!string.IsNullOrEmpty(ObjectSize)) tags.Add(ObjectSize);
            if (Materials != null)
                foreach (string m in Materials)
                    if (!string.IsNullOrEmpty(m)) tags.Add(m);
            return tags;
        }
    }
}
