using System.Collections.Generic;
using System.IO;
using ProjectMuseum.Narrative; // JsonHelper
using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// Reads the artifact JSON in Resources/ArtifactCatalogue, merges descriptive +
    /// functional by Id, resolves BOTH the artifact's UI icon and its in-game
    /// isometric sprite, and writes a single <see cref="MuseumArtifactDatabase"/>
    /// asset. Lookups are exact (trimmed, case-insensitive) filename matches:
    /// BOTH the UI icon and the isometric sprite are matched by ArtifactName (the
    /// sprite files in both folders are named by artifact name, with spaces — not by
    /// the space-less Id). Misses are logged and left null. Mirrors
    /// <c>BuilderJsonImporter</c>.
    ///
    /// Run from <c>Tools ▸ Project Museum ▸ Import Artifact Data</c>.
    /// </summary>
    public static class ArtifactDatabaseImporter
    {
        private const string CatalogueFolder = "Assets/Resources/ArtifactCatalogue";
        private const string DescriptiveJson = CatalogueFolder + "/RawArtifactDescriptiveDataEnglish.json";
        private const string FunctionalJson = CatalogueFolder + "/RawArtifactFunctionalData.json";

        private const string OutputFolder = "Assets/GameData";
        private const string AssetPath = OutputFolder + "/MuseumArtifactDatabase.asset";

        // Both matched by ArtifactName (files in both folders are named with spaces).
        private static readonly string[] IconFoldersByName = { "Assets/2D/UI/MineUi/Artifacts" };
        private static readonly string[] IsometricFoldersByName = { "Assets/2D/Museum/Isometric View Artifacts" };

        // One subfolder per artifact, named by ArtifactName, holding DC/IC/PC x CR/RR/LR.
        private const string ConditionsFolder = "Assets/2D/Common/Artifacts/conditions 100X100";

        // Artifact names whose condition folder is spelled differently from the JSON.
        // Keyed by normalised ArtifactName -> normalised folder name.
        private static readonly Dictionary<string, string> ConditionFolderAliases = new Dictionary<string, string>
        {
            // JSON typo: "Humam" for "Human". The art folder has it right.
            { "classicalnativeamericanclayhumamfigurine", "classicalnativeamericanclayhumanfigurine" },
        };

        // Normalised folder name -> asset path, built once per import.
        private static Dictionary<string, string> _conditionFolders;

        [MenuItem("Tools/Project Museum/Import Artifact Data")]
        public static void Import()
        {
            EnsureFolder(OutputFolder);

            ArtifactDescriptive[] descriptive = Read<ArtifactDescriptive>(DescriptiveJson);
            ArtifactFunctional[] functional = Read<ArtifactFunctional>(FunctionalJson);

            var funcById = new Dictionary<string, ArtifactFunctional>();
            foreach (ArtifactFunctional f in functional)
                if (f?.Id != null) funcById[f.Id] = f;

            var entries = new List<MuseumArtifactDatabase.Entry>(descriptive.Length);
            int missingIcons = 0, missingIso = 0, missingConditions = 0;
            _conditionFolders = null;
            foreach (ArtifactDescriptive d in descriptive)
            {
                if (d?.Id == null) continue;
                funcById.TryGetValue(d.Id, out ArtifactFunctional f);

                Sprite icon = ResolveSprite(d.ArtifactName, IconFoldersByName);
                Sprite iso = ResolveSprite(d.ArtifactName, IsometricFoldersByName);
                if (icon == null) missingIcons++;
                if (iso == null) missingIso++;

                List<ArtifactConditionIcon> conditions = ResolveConditionIcons(d.ArtifactName, out int missing);
                missingConditions += missing;

                entries.Add(new MuseumArtifactDatabase.Entry
                {
                    Id = d.Id,
                    Descriptive = d,
                    Functional = f,
                    Icon = icon,
                    IsometricSprite = iso,
                    ConditionIcons = conditions
                });
            }

            MuseumArtifactDatabase db = LoadOrCreate(AssetPath);
            db.SetArtifacts(entries);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArtifactDatabaseImporter] Imported {entries.Count} artifact(s) → {AssetPath} " +
                      $"({missingIcons} missing UI icon, {missingIso} missing isometric sprite, " +
                      $"{missingConditions} missing condition variant(s) of " +
                      $"{entries.Count * ArtifactCodes.Conditions.Length * ArtifactCodes.Rarities.Length}).");

            WarnAboutOrphanConditionFolders(entries);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static T[] Read<T>(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ArtifactDatabaseImporter] Missing JSON: {path}");
                return System.Array.Empty<T>();
            }
            return JsonHelper.FromJsonArray<T>(File.ReadAllText(path));
        }

        private static Sprite ResolveSprite(string wantedRaw, string[] folders)
        {
            if (string.IsNullOrEmpty(wantedRaw)) return null;
            string wanted = wantedRaw.Trim();

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets($"{wanted} t:Sprite", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.Equals(Path.GetFileNameWithoutExtension(p).Trim(), wanted,
                            System.StringComparison.OrdinalIgnoreCase)) continue;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (sprite != null) return sprite;
                }
            }
            return null;
        }

        /// <summary>
        /// Builds all nine condition x rarity variants for an artifact. The list is always
        /// nine long and in a fixed order (DC/IC/PC x CR/RR/LR) so the inspector shows the
        /// full matrix; a variant with no source PNG keeps a null Icon and is counted in
        /// <paramref name="missing"/>.
        /// </summary>
        private static List<ArtifactConditionIcon> ResolveConditionIcons(string artifactName, out int missing)
        {
            missing = 0;
            var variants = new List<ArtifactConditionIcon>(
                ArtifactCodes.Conditions.Length * ArtifactCodes.Rarities.Length);

            string folder = ConditionFolderFor(artifactName);
            foreach (ArtifactCondition condition in ArtifactCodes.Conditions)
            foreach (ArtifactRarity rarity in ArtifactCodes.Rarities)
            {
                var variant = new ArtifactConditionIcon { Condition = condition, Rarity = rarity };
                if (folder != null)
                    variant.Icon = LoadSprite($"{folder}/{ArtifactCodes.Of(condition)}_{ArtifactCodes.Of(rarity)}.png");
                if (variant.Icon == null) missing++;
                variants.Add(variant);
            }

            if (folder == null)
                Debug.LogWarning($"[ArtifactDatabaseImporter] No condition folder for '{artifactName}' " +
                                 $"under {ConditionsFolder}.");
            return variants;
        }

        /// <summary>
        /// Asset path of an artifact's condition folder, or null. Matching ignores case,
        /// spaces and punctuation, and tolerates the stray ".png" suffix some of these
        /// folders were created with.
        /// </summary>
        private static string ConditionFolderFor(string artifactName)
        {
            if (string.IsNullOrEmpty(artifactName)) return null;
            BuildConditionFolderIndex();

            string key = Normalise(artifactName);
            if (ConditionFolderAliases.TryGetValue(key, out string alias)) key = alias;
            return _conditionFolders.TryGetValue(key, out string path) ? path : null;
        }

        private static void BuildConditionFolderIndex()
        {
            if (_conditionFolders != null) return;
            _conditionFolders = new Dictionary<string, string>();

            if (!AssetDatabase.IsValidFolder(ConditionsFolder))
            {
                Debug.LogError($"[ArtifactDatabaseImporter] Missing condition art folder: {ConditionsFolder}");
                return;
            }

            foreach (string dir in AssetDatabase.GetSubFolders(ConditionsFolder))
                _conditionFolders[Normalise(Path.GetFileName(dir))] = dir;
        }

        /// <summary>Lowercase alphanumerics only, minus a trailing "png" from the misnamed folders.</summary>
        private static string Normalise(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            string s = sb.ToString();
            return s.EndsWith("png") ? s.Substring(0, s.Length - 3) : s;
        }

        /// <summary>
        /// Loads the sprite at an exact path. Falls back to the sub-asset representations so
        /// it works whether the texture is imported as Sprite Single or still as Multiple.
        /// </summary>
        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (sub is Sprite s) return s;
            return null;
        }

        /// <summary>Condition art that no artifact claims - usually a misnamed folder.</summary>
        private static void WarnAboutOrphanConditionFolders(List<MuseumArtifactDatabase.Entry> entries)
        {
            BuildConditionFolderIndex();
            var claimed = new HashSet<string>();
            foreach (MuseumArtifactDatabase.Entry e in entries)
            {
                string folder = ConditionFolderFor(e.Descriptive?.ArtifactName);
                if (folder != null) claimed.Add(folder);
            }

            var orphans = new List<string>();
            foreach (string folder in _conditionFolders.Values)
                if (!claimed.Contains(folder)) orphans.Add(Path.GetFileName(folder));

            if (orphans.Count > 0)
                Debug.LogWarning($"[ArtifactDatabaseImporter] {orphans.Count} condition folder(s) match no " +
                                 $"artifact and were skipped: {string.Join(", ", orphans)}");
        }

        private static MuseumArtifactDatabase LoadOrCreate(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MuseumArtifactDatabase>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MuseumArtifactDatabase>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
