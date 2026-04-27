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
        private const int SlotsX = 3;
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
            var offsetX = slotUnitX / 2;
            var offsetY = slotUnitY / 2;

            var candidatePositions = new List<GridPosition>(SlotsX * SlotsY);
            for (var i = 0; i < SlotsX; i++)
            {
                for (var j = 0; j < SlotsY; j++)
                {
                    var x = i * slotUnitX + offsetX;
                    var y = j * slotUnitY + offsetY;
                    var pos = new GridPosition(x, y);
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
            noOfBackdrops = Math.Min(noOfBackdrops,
                Math.Min(candidatePositions.Count, sourcePool.Count));

            var placed = new List<SpecialBackdropData>(noOfBackdrops);

            for (var i = 0; i < noOfBackdrops; i++)
            {
                // Take from the front of each shuffled list (no duplicates possible)
                var sourceId = sourcePool[i];
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