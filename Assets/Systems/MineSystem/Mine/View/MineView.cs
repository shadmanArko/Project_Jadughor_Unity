using UnityEngine;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.Mine.View
{
    public class MineView : MonoBehaviour
    {
        public Grid grid;

        public Tilemap backgroundTileMap;
        public Tilemap specialBackdropTileMap;
        public Tilemap vineTileMap;
        public Tilemap artifactTileMap;
        public Tilemap resourceTileMap;
        public Tilemap wallTileMap;
        public Tilemap wallShadowTileMap;
        public Tilemap cellCrackTilemap;
        public Tilemap unrevealedTileMap;
    }
}
