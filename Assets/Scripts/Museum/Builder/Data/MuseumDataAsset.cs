using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// ScriptableObject wrapper around the live <see cref="MuseumData"/> so the
    /// whole museum state is inspectable in the editor and injectable via Zenject.
    ///
    /// IMPORTANT: the asset is a WORKING COPY, not the persistence layer — real
    /// persistence is the JSON written by <c>MuseumDataModel</c> (in the editor,
    /// play-mode changes stick to the asset; in a build they never do).
    /// </summary>
    [CreateAssetMenu(fileName = "MuseumData", menuName = "Config/Museum/Museum Data")]
    public class MuseumDataAsset : ScriptableObject
    {
        [Header("Live state (inspect during play mode)")]
        public MuseumData Data = new MuseumData();

        [Header("New game defaults")]
        [Tooltip("Chunk size in cells — must match ExpansionManager's chunk size.")]
        public Vector2Int ChunkSize = new Vector2Int(20, 18);
        [Tooltip("Floor tile name recorded for the starting tiles of a new game. " +
                 "LEAVE EMPTY to keep whatever the scene already shows — floor " +
                 "records then only fill in as the player actually paints tiles.")]
        public string DefaultTileVariationName = "";
        [Tooltip("Money a brand-new museum starts with.")]
        public float StartingMoney = 1000f;

        /// <summary>Reset the working copy to a blank museum (used on New Game).</summary>
        public void ResetToNewGame()
        {
            Data = new MuseumData
            {
                Info = new MuseumInfo { Name = "My Museum", Money = StartingMoney }
            };
        }
    }
}
