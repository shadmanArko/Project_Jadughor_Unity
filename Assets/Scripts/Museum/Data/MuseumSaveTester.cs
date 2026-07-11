using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Data
{
    /// <summary>
    /// TEST HELPER — put on a GameObject in the Museum scene to exercise the full
    /// loop: pick a builder card → place objects / paint floors / apply wallpaper →
    /// **Save Museum** → change things → **Load Museum** → everything restores.
    ///
    /// Use the component's right-click context menu (works in Play mode), or the
    /// optional hotkeys. Remove from production scenes.
    ///
    /// What Load restores: placed objects respawn (MuseumObjectPlacementSystem),
    /// painted floors repaint (MuseumFloorSync), wallpapers reapply
    /// (MuseumWallpaperSystem) — all via BuilderActions.OnMuseumDataReloaded.
    /// </summary>
    public class MuseumSaveTester : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;

        [Header("Optional hotkeys (Play mode)")]
        [SerializeField] private bool enableHotkeys = true;
        [SerializeField] private Key saveKey = Key.F5;
        [SerializeField] private Key loadKey = Key.F9;

        [ContextMenu("Save Museum")]
        public void SaveMuseum()
        {
            _model.Save();
        }

        [ContextMenu("Load Museum")]
        public void LoadMuseum()
        {
            _model.ReloadFromDisk();
        }

        [ContextMenu("New Game (reset museum)")]
        public void NewGame()
        {
            _model.NewGame();
        }

        [ContextMenu("Delete Save File")]
        public void DeleteSaveFile()
        {
            _model.DeleteSaveFile();
        }

        [ContextMenu("Log Museum State")]
        public void LogState()
        {
            Debug.Log($"[MuseumSaveTester] Money ${_model.Money.Value} | " +
                      $"objects {_model.PlacedObjects.Count} | tiles {_model.Tiles.Count} | " +
                      $"walls {_model.Walls.Count} | chunks {_model.DevelopedChunkCount.Value}\n" +
                      $"Save file: {MuseumDataModel.SavePath}");
        }

        private void Update()
        {
            if (!enableHotkeys) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb[saveKey].wasPressedThisFrame) SaveMuseum();
            if (kb[loadKey].wasPressedThisFrame) LoadMuseum();
        }
    }
}
