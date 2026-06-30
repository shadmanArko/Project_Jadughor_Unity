using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Narrative.EditorTools
{
    /// <summary>
    /// One-shot importer: reads the source JSON (copied from the Godot project into
    /// <c>Assets/GameData/Source/</c>) and writes/refreshes the
    /// <see cref="StoryDatabase"/> and <see cref="TutorialDatabase"/> assets.
    ///
    /// Run it from <c>Tools ▸ Project Museum ▸ Import Narrative JSON</c>. Re-run any
    /// time you update the JSON; after that you can edit the assets directly and the
    /// JSON is no longer needed at runtime.
    /// </summary>
    public static class NarrativeJsonImporter
    {
        private const string SourceFolder = "Assets/GameData/Source";
        private const string OutputFolder = "Assets/GameData";
        private const string StoryJson = "StoryScene.json";
        private const string TutorialJson = "Tutorials.json";
        private const string StoryAssetPath = OutputFolder + "/StoryDatabase.asset";
        private const string TutorialAssetPath = OutputFolder + "/TutorialDatabase.asset";

        [MenuItem("Tools/Project Museum/Import Narrative JSON")]
        public static void Import()
        {
            EnsureFolder(OutputFolder);

            int stories = ImportStories();
            int tutorials = ImportTutorials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NarrativeJsonImporter] Imported {stories} story scene(s) → {StoryAssetPath} " +
                      $"and {tutorials} tutorial(s) → {TutorialAssetPath}.");
        }

        private static int ImportStories()
        {
            string json = ReadSource(StoryJson);
            if (json == null) return 0;

            StoryScene[] scenes = JsonHelper.FromJsonArray<StoryScene>(json);

            StoryDatabase db = LoadOrCreate<StoryDatabase>(StoryAssetPath);
            db.SetScenes(new List<StoryScene>(scenes));
            EditorUtility.SetDirty(db);
            return scenes.Length;
        }

        private static int ImportTutorials()
        {
            string json = ReadSource(TutorialJson);
            if (json == null) return 0;

            Tutorial[] tutorials = JsonHelper.FromJsonArray<Tutorial>(json);

            TutorialDatabase db = LoadOrCreate<TutorialDatabase>(TutorialAssetPath);
            db.SetTutorials(new List<Tutorial>(tutorials));
            EditorUtility.SetDirty(db);
            return tutorials.Length;
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static string ReadSource(string fileName)
        {
            string path = Path.Combine(SourceFolder, fileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[NarrativeJsonImporter] Source JSON not found: {path}");
                return null;
            }
            return File.ReadAllText(path);
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
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
