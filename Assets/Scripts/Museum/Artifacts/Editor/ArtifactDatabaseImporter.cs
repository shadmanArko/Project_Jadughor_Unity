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
    /// the UI icon by ArtifactName in the UI-icon folder, the isometric sprite by Id
    /// in the isometric folder. Misses are logged and left null. Mirrors
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

        // UI icon: matched by ArtifactName. Isometric/world sprite: matched by Id.
        private static readonly string[] IconFoldersByName = { "Assets/2D/UI/MineUi/Artifacts" };
        private static readonly string[] IsometricFoldersById = { "Assets/2D/Museum/Isometric View Artifacts" };

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
            int missingIcons = 0, missingIso = 0;
            foreach (ArtifactDescriptive d in descriptive)
            {
                if (d?.Id == null) continue;
                funcById.TryGetValue(d.Id, out ArtifactFunctional f);

                Sprite icon = ResolveSprite(d.ArtifactName, IconFoldersByName);
                Sprite iso = ResolveSprite(d.Id, IsometricFoldersById);
                if (icon == null) missingIcons++;
                if (iso == null) missingIso++;

                entries.Add(new MuseumArtifactDatabase.Entry
                {
                    Descriptive = d,
                    Functional = f,
                    Icon = icon,
                    IsometricSprite = iso
                });
            }

            MuseumArtifactDatabase db = LoadOrCreate(AssetPath);
            db.SetArtifacts(entries);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArtifactDatabaseImporter] Imported {entries.Count} artifact(s) → {AssetPath} " +
                      $"({missingIcons} missing UI icon, {missingIso} missing isometric sprite).");
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
