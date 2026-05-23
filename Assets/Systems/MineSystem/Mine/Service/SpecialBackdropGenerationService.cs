using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using UnityEngine;
using Random = System.Random;

namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class SpecialBackdropGenerationService
    {
        // ── Slot grid layout ──────────────────────────────────────────────────
        private const int SlotsX = 2;
        private const int SlotsY = 4;

        private readonly Random _rand = new();
        
        /// <summary>
        /// Randomly places <paramref name="noOfBackdrops"/> special backdrops
        /// across the mine using a slot grid for even distribution.
        /// Each backdrop is assigned a unique slot position and a randomly
        /// chosen source ID from <paramref name="availableSourceIds"/>.
        /// </summary>
        /// <param name="config">Mine generation config (grid dimensions).</param>
        /// <param name="mineData">Mine data to populate.</param>
        /// <param name="availableSourceIds">
        ///     Pool of backdrop asset source IDs to pick from.
        /// </param>
        /// <param name="noOfBackdrops">
        ///     How many backdrops to place (defaults to 2).
        /// </param>
        public async UniTask GenerateSpecialBackdrops(
            MineGenerationConfig config,
            MineData mineData,
            List<string> availableSourceIds,
            int noOfBackdrops = 2)
        {
            await UniTask.SwitchToThreadPool();

            var mineW = config.mineSizeX;
            var mineH = config.mineSizeY;

            // ── 1. Build candidate slot-centre positions ───────────────────────
            var slotUnitX = mineW / SlotsX;
            var slotUnitY = mineH / SlotsY;

            var candidatePositions = new List<GridPosition>(SlotsX * SlotsY);
            for (var i = 0; i < SlotsX; i++)
            {
                for (var j = 0; j < SlotsY; j++)
                {
                    // Calculate min and max grid indices for the chunk, keeping a padding
                    // of a few tiles to avoid spawning too close to the borders of the chunk or mine
                    var minX = i * slotUnitX + 3;
                    var maxX = (i + 1) * slotUnitX - 4;
                    var minY = j * slotUnitY + 3;
                    var maxY = (j + 1) * slotUnitY - 4;

                    var x = _rand.Next(minX, maxX + 1);
                    var y = _rand.Next(minY, maxY + 1);
                    
                    var pos = new GridPosition(x - mineW / 2, -y);
                    if (!candidatePositions.Contains(pos))
                        candidatePositions.Add(pos);
                }
            }

            // Shuffle so picks are non-deterministic
            Shuffle(candidatePositions);

            // ── 2. Build a mutable pool of source IDs ──────────────────────────
            var sourcePool = new List<string>(availableSourceIds);
            Shuffle(sourcePool);

            // ── 3. Place backdrops ─────────────────────────────────────────────
            if (sourcePool.Count == 0)
            {
                await UniTask.SwitchToMainThread();
                return;
            }

            // We force the number of backdrops to match the number of chunks (8), 
            // ensuring every chunk gets exactly one backdrop.
            noOfBackdrops = candidatePositions.Count;

            var placed = new List<SpecialBackdropData>(noOfBackdrops);

            for (var i = 0; i < noOfBackdrops; i++)
            {
                // Wrap around so we can place more backdrops than we have unique sprites for
                var sourceId = sourcePool[i % sourcePool.Count];
                var position = candidatePositions[i];

                var backdrop = new SpecialBackdropData
                {
                    Id = sourceId,
                    TilePosition = position,
                };

                placed.Add(backdrop);
            }

            // ── 4. Commit to mineData ──────────────────────────────────────────
            mineData.SpecialBackdropDatas = placed;

            // ── 5. Log results ─────────────────────────────────────────────────
            Debug.Log($"[SpecialBackdrops] Placed {placed.Count} backdrop(s):");
            foreach (var b in placed)
                Debug.Log($"  SourceId:{b.Id}  X:{b.TilePosition.X}  Y:{b.TilePosition.Y}");

            await UniTask.SwitchToMainThread();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _rand.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}