using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// Forces every texture under the artifact condition folder (and its subfolders) to
    /// Sprite ▸ Single, 32 pixels per unit, Point (no filter) — the import settings the
    /// rest of the pixel-art artifacts use. These were imported as Sprite ▸ Multiple with
    /// 100 PPU, which produced a single auto-sliced sub-sprite per file at the wrong scale.
    ///
    /// Run from <c>Tools ▸ Project Museum ▸ Fix Condition Sprite Import Settings</c>.
    /// With a folder selected in the Project window it processes that folder instead.
    /// </summary>
    public static class ConditionSpriteImportFixer
    {
        private const string DefaultFolder = "Assets/2D/Common/Artifacts/conditions 100X100";
        private const float PixelsPerUnit = 32f;

        [MenuItem("Tools/Project Museum/Fix Condition Sprite Import Settings")]
        public static void Fix()
        {
            string folder = SelectedFolder() ?? DefaultFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError($"[ConditionSpriteImportFixer] Not a folder: {folder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var changed = new List<string>(guids.Length);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                    bool dirty = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
                    { importer.spritePixelsPerUnit = PixelsPerUnit; dirty = true; }
                    if (importer.filterMode != FilterMode.Point)
                    { importer.filterMode = FilterMode.Point; dirty = true; }

                    if (!dirty) continue;
                    // WriteImportSettingsIfDirty, not SaveAndReimport: inside a
                    // StartAssetEditing batch the reimport is deferred to StopAssetEditing,
                    // which is what keeps 400+ textures from reimporting one at a time.
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    changed.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ConditionSpriteImportFixer] {changed.Count} of {guids.Length} texture(s) updated " +
                      $"under \"{folder}\" (Single, {PixelsPerUnit} PPU, Point).");
        }

        private static string SelectedFolder()
        {
            foreach (Object o in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path)) return path;
            }
            return null;
        }
    }
}
