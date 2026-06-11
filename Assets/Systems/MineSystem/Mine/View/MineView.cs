using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.Mine.View
{
    public class MineView : MonoBehaviour
    {
        public Grid grid;
        public List<Tilemap> tilemaps;

        public Tilemap backgroundTileMap;
        public Tilemap specialBackdropTileMap;
        public Tilemap vineTileMap;
        public Tilemap artifactTileMap;
        public Tilemap resourceTileMap; // can be merged to artifact
        public Tilemap wallPlaceableTileMap;
        public Tilemap cellPlaceableTileMap;
        public Tilemap wallTileMap;
        public Tilemap unrevealedTileMap;
    }
}
