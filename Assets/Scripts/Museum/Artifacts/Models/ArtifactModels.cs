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

    /// <summary>Wear state of an artifact — folder/file prefix DC / IC / PC.</summary>
    public enum ArtifactCondition
    {
        Decrepit = 0,
        Intact = 1,
        Pristine = 2,
    }

    /// <summary>Rarity of an artifact — file suffix CR / RR / LR.</summary>
    public enum ArtifactRarity
    {
        Common = 0,
        Rare = 1,
        Legendary = 2,
    }

    /// <summary>
    /// One of the nine condition x rarity art variants of an artifact, resolved from
    /// Assets/2D/Common/Artifacts/conditions 100X100/&lt;ArtifactName&gt;/&lt;CC&gt;_&lt;RR&gt;.png
    /// at import time (see ArtifactDatabaseImporter).
    /// </summary>
    [Serializable]
    public class ArtifactConditionIcon
    {
        public ArtifactCondition Condition;
        public ArtifactRarity Rarity;
        public Sprite Icon;

        /// <summary>File-stem code for this pairing, e.g. "PC_LR".</summary>
        public string Code => $"{ArtifactCodes.Of(Condition)}_{ArtifactCodes.Of(Rarity)}";
    }

    /// <summary>Two-letter codes used by the condition sprite filenames.</summary>
    public static class ArtifactCodes
    {
        public static string Of(ArtifactCondition c) => c switch
        {
            ArtifactCondition.Decrepit => "DC",
            ArtifactCondition.Intact => "IC",
            ArtifactCondition.Pristine => "PC",
            _ => null,
        };

        public static string Of(ArtifactRarity r) => r switch
        {
            ArtifactRarity.Common => "CR",
            ArtifactRarity.Rare => "RR",
            ArtifactRarity.Legendary => "LR",
            _ => null,
        };

        public static readonly ArtifactCondition[] Conditions =
        {
            ArtifactCondition.Decrepit, ArtifactCondition.Intact, ArtifactCondition.Pristine,
        };

        public static readonly ArtifactRarity[] Rarities =
        {
            ArtifactRarity.Common, ArtifactRarity.Rare, ArtifactRarity.Legendary,
        };
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
