using System.Collections.Generic;
using System.IO;
using ProjectMuseum.Narrative; // JsonHelper
using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// Reads the placeable-object JSON (copied from the Godot project into
    /// <c>Assets/GameData/Source/</c>) and writes/refreshes a single
    /// <see cref="BuilderDatabase"/> asset. For each entry it resolves the icon
    /// texture by exact filename in the matching <c>Assets/2D/Museum/&lt;Category&gt;</c>
    /// folder and logs anything it can't find.
    ///
    /// Run from <c>Tools ▸ Project Museum ▸ Import Builder JSON</c>. Flooring has no
    /// JSON here — those cards come from the live tileset at runtime.
    /// </summary>
    public static class BuilderJsonImporter
    {
        private const string SourceFolder = "Assets/GameData/Source";
        private const string OutputFolder = "Assets/GameData";
        private const string AssetPath = OutputFolder + "/BuilderDatabase.asset";
        private const string SpriteRoot = "Assets/2D/Museum";

        [MenuItem("Tools/Project Museum/Import Builder JSON")]
        public static void Import()
        {
            EnsureFolder(OutputFolder);
            BuilderDatabase db = LoadOrCreate(AssetPath);

            int exhibits = 0, shops = 0, others = 0, sanit = 0, walls = 0;

            // Exhibit
            var exList = new List<BuilderDatabase.ExhibitEntry>();
            foreach (var v in Read<ExhibitVariation>("exhibitVariations.json"))
                exList.Add(new BuilderDatabase.ExhibitEntry
                { Data = v, Icon = ResolveIcon("Exhibits", v.VariationName) });
            db.SetExhibits(exList); exhibits = exList.Count;

            // DecorationShop
            var shopList = new List<BuilderDatabase.DecorationShopEntry>();
            foreach (var v in Read<DecorationShopVariation>("decorationShopVariations.json"))
                shopList.Add(new BuilderDatabase.DecorationShopEntry
                { Data = v, Icon = ResolveIcon("DecorationShops", v.VariationName) });
            db.SetDecorationShops(shopList); shops = shopList.Count;

            // DecorationOther
            var otherList = new List<BuilderDatabase.DecorationOtherEntry>();
            foreach (var v in Read<DecorationOtherVariation>("decorationOtherVariations.json"))
                otherList.Add(new BuilderDatabase.DecorationOtherEntry
                { Data = v, Icon = ResolveIcon("DecorationOthers", v.VariationName) });
            db.SetDecorationOthers(otherList); others = otherList.Count;

            // Sanitation (name field is SanitationId)
            var sanitList = new List<BuilderDatabase.SanitationEntry>();
            foreach (var v in Read<SanitationVariation>("sanitationVariations.json"))
                sanitList.Add(new BuilderDatabase.SanitationEntry
                { Data = v, Icon = ResolveIcon("Sanitations", v.SanitationId) });
            db.SetSanitations(sanitList); sanit = sanitList.Count;

            // Wallpaper
            var wallList = new List<BuilderDatabase.WallpaperEntry>();
            foreach (var v in Read<WallpaperVariation>("wallpaperVariations.json"))
                wallList.Add(new BuilderDatabase.WallpaperEntry
                { Data = v, Icon = ResolveIcon("Wallpapers", v.VariationName) });
            db.SetWallpapers(wallList); walls = wallList.Count;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuilderJsonImporter] Imported → {AssetPath}: " +
                      $"{exhibits} exhibit(s), {shops} shop(s), {others} other(s), " +
                      $"{sanit} sanitation(s), {walls} wallpaper(s).");
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static T[] Read<T>(string fileName)
        {
            string path = Path.Combine(SourceFolder, fileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[BuilderJsonImporter] Source JSON not found: {path}");
                return System.Array.Empty<T>();
            }
            return JsonHelper.FromJsonArray<T>(File.ReadAllText(path));
        }

        /// <summary>Find a Texture2D whose filename exactly (trimmed, ci) matches name.</summary>
        private static Texture2D ResolveIcon(string categoryFolder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string folder = $"{SpriteRoot}/{categoryFolder}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[BuilderJsonImporter] Sprite folder missing: {folder}");
                return null;
            }

            string wanted = name.Trim();
            string[] guids = AssetDatabase.FindAssets($"{wanted} t:Texture2D", new[] { folder });
            var matches = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path).Trim();
                if (string.Equals(file, wanted, System.StringComparison.OrdinalIgnoreCase))
                    matches.Add(path);
            }

            if (matches.Count == 0)
            {
                Debug.LogWarning($"[BuilderJsonImporter] No icon '{wanted}' in {folder} — card will use a placeholder.");
                return null;
            }
            if (matches.Count > 1)
                Debug.LogWarning($"[BuilderJsonImporter] Multiple icons named '{wanted}' in {folder}; using {matches[0]}.");

            return AssetDatabase.LoadAssetAtPath<Texture2D>(matches[0]);
        }

        private static BuilderDatabase LoadOrCreate(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BuilderDatabase>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BuilderDatabase>();
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
