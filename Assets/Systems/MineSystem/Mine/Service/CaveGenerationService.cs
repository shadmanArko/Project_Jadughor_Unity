using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Random = System.Random;


namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class CaveGenerationService
    {
        // ── Allowed cave dimensions (width × height) ──────────────────────────
        private static readonly (int W, int H)[] CavePresets =
        {
            (3, 4), (4, 3),
            (3, 5), (5, 3),
            (6, 3), (7, 3),
            (4, 5), (5, 4),
            (6, 4), (7, 4),
        };

        private readonly Random _rand = new();

        // ─────────────────────────────────────────────────────────────────────
        //  Boss Cave
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Places a single boss cave near the bottom-centre of the mine,
        /// with a randomly chosen preset size, positional jitter, deformation
        /// and corrosion applied.
        /// </summary>
        public async UniTask GenerateBossCave(MineGenerationConfig config, MineData mineData)
        {
            await UniTask.SwitchToThreadPool();

            var (w, h) = PickPreset(); // random preset
            var mineW = config.mineSizeX;
            var mineH = config.mineSizeY;

            // Centre-bottom anchor with ±2 cell horizontal jitter
            var jitterX = _rand.Next(-2, 3);
            var centreX = mineW / 2 + jitterX;

            var xMin = Math.Clamp(centreX - w / 2, 2, mineW - w - 2);
            var xMax = xMin + w;
            var yMax = mineH - 3; // near the floor, 2 cells from border
            var yMin = Math.Clamp(yMax - h, 2, mineH - 3);

            var stalagmites = Math.Clamp(config.stalagmiteCount, 0, w);
            var stalactites = Math.Clamp(config.stalactiteCount, 0, w);

            var cave = CarveAndDeformCave(xMin, xMax, yMin, yMax,
                stalagmites, stalactites, mineData);
            mineData.Caves.Add(cave);

            Debug.Log($"[BossCave] Size:{w}x{h}  L:{cave.LeftBound} R:{cave.RightBound} " +
                      $"T:{cave.TopBound} B:{cave.BottomBound}");

            await UniTask.SwitchToMainThread();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Regular Caves
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Scatters a number of caves (up to <see cref="MineGenerationConfig.numberOfMaxCaves"/>)
        /// across the mine using a slot grid, each with a random preset size,
        /// positional jitter, deformation and corrosion.
        /// </summary>
        public async UniTask GenerateCave(MineGenerationConfig config, MineData mineData)
        {
            await UniTask.SwitchToThreadPool();

            var mineW = config.mineSizeX;
            var mineH = config.mineSizeY;
            var noOfCaves = _rand.Next(config.numberOfMaxCaves / 2, config.numberOfMaxCaves + 1);

            // ── Build slot grid ───────────────────────────────────────────────
            const int slotsX = 3;
            const int slotsY = 3;

            var slotW = mineW / slotsX;
            var slotH = mineH / slotsY;

            var slots = new List<(int cx, int cy)>();
            for (var si = 0; si < slotsX; si++)
            {
                for (var sj = 0; sj < slotsY; sj++)
                {
                    if (si == 1 && sj == 2) continue; // keep centre-bottom clear (boss area)

                    var cx = slotW * si + slotW / 2;
                    var cy = slotH * sj + slotH / 2;
                    slots.Add((cx, cy));
                }
            }

            // Shuffle slots so cave assignment is non-deterministic
            Shuffle(slots);

            // ── Place caves ───────────────────────────────────────────────────
            for (var i = 0; i < noOfCaves && i < slots.Count; i++)
            {
                var (w, h) = PickPreset();
                var (cx, cy) = slots[i];

                // Add per-slot jitter (up to ±slotW/4 in each axis)
                var jitterX = _rand.Next(-(slotW / 4), slotW / 4 + 1);
                var jitterY = _rand.Next(-(slotH / 4), slotH / 4 + 1);

                var xMin = Math.Clamp(cx + jitterX - w / 2, 2, mineW - w - 2);
                var xMax = xMin + w;
                var yMin = Math.Clamp(cy + jitterY - h / 2, 2, mineH - h - 2);
                var yMax = yMin + h;

                // Ensure we stay at least 2 cells from every border
                xMin = Math.Clamp(xMin, 2, mineW - 3);
                xMax = Math.Clamp(xMax, xMin + 1, mineW - 2);
                yMin = Math.Clamp(yMin, 2, mineH - 3);
                yMax = Math.Clamp(yMax, yMin + 1, mineH - 2);

                var actualW = xMax - xMin;
                var stalagmites = actualW > 2 ? _rand.Next(1, actualW - 1) : 0;
                var stalactites = actualW > 2 ? _rand.Next(1, actualW - 1) : 0;

                var cave = CarveAndDeformCave(xMin, xMax, yMin, yMax,
                    stalagmites, stalactites, mineData);
                mineData.Caves.Add(cave);

                Debug.Log($"[Cave {i}] Size:{w}x{h}  L:{cave.LeftBound} R:{cave.RightBound} " +
                          $"T:{cave.TopBound} B:{cave.BottomBound}");
            }

            await UniTask.SwitchToMainThread();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core carve + deform + corrode
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Breaks cells inside [xMin,xMax) × [yMin,yMax), then applies
        /// irregular edge deformation and random interior corrosion.
        /// Returns the resulting <see cref="Cave"/> object.
        /// </summary>
        private Cave CarveAndDeformCave(
            int xMin, int xMax,
            int yMin, int yMax,
            int noOfStalagmites, int noOfStalactites,
            MineData mineData)
        {
            var cave = new Cave
            {
                Id = Guid.NewGuid().ToString(),
                LeftBound = xMin,
                RightBound = xMax,
                TopBound = yMin,
                BottomBound = yMax,
                CellIds = new List<string>(),
                StalagmiteCellIds = new List<string>(),
                StalactiteCellIds = new List<string>(),
            };

            var width = xMax - xMin;
            var height = yMax - yMin;

            // ── 1. Build a boolean mask (true = hollow) with deformation ───────
            var mask = BuildDeformedMask(width, height);

            // ── 2. Apply additional corrosion ──────────────────────────────────
            ApplyCorrosion(mask, width, height);

            // ── 3. Carve cells from MineData according to mask ─────────────────
            for (var x = xMin; x < xMax; x++)
            {
                for (var y = yMin; y < yMax; y++)
                {
                    if (!mask[x - xMin, y - yMin]) continue;

                    var cell = GetCell(mineData, x, y);
                    if (cell == null) continue;

                    cell.IsBroken = true;
                    cell.IsRevealed = true;
                    cell.IsBreakable = false;
                    cave.CellIds.Add(cell.Id);
                }
            }

            // ── 4. Stalagmites (bottom row pillars inside the cave) ────────────
            PlaceFormations(cave, mineData, xMin, xMax, yMax - 1, noOfStalagmites,
                isStalagmite: true);

            // ── 5. Stalactites (top row hangings inside the cave) ──────────────
            PlaceFormations(cave, mineData, xMin, xMax, yMin, noOfStalactites,
                isStalagmite: false);

            return cave;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Deformation helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a hollow rectangular mask then erodes its edges by a random
        /// number of cells to produce an irregular, organic cave boundary.
        /// </summary>
        private bool[,] BuildDeformedMask(int width, int height)
        {
            var mask = new bool[width, height];

            // Fill everything as hollow to start
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                mask[x, y] = true;

            // Deform each edge: randomly push the wall inward by 0-1 cells
            // Top & Bottom edges
            for (var x = 0; x < width; x++)
            {
                var topErode = _rand.NextDouble() < 0.35 ? 1 : 0;
                var bottomErode = _rand.NextDouble() < 0.35 ? 1 : 0;
                for (var e = 0; e < topErode; e++) mask[x, e] = false;
                for (var e = 0; e < bottomErode; e++) mask[x, height - 1 - e] = false;
            }

            // Left & Right edges
            for (var y = 0; y < height; y++)
            {
                var leftErode = _rand.NextDouble() < 0.35 ? 1 : 0;
                var rightErode = _rand.NextDouble() < 0.35 ? 1 : 0;
                for (var e = 0; e < leftErode; e++) mask[e, y] = false;
                for (var e = 0; e < rightErode; e++) mask[width - 1 - e, y] = false;
            }

            return mask;
        }

        /// <summary>
        /// Randomly punches small holes in the interior of the cave mask to
        /// simulate corrosion / worn rock faces.
        /// </summary>
        private void ApplyCorrosion(bool[,] mask, int width, int height)
        {
            // ~10-20 % of interior cells become solid again (corrosion pockets)
            var interior = (width - 2) * (height - 2);
            if (interior <= 0) return;

            var pockets = _rand.Next(interior / 10, Math.Max(interior / 5, 1) + 1);
            for (var p = 0; p < pockets; p++)
            {
                var px = _rand.Next(1, width - 1);
                var py = _rand.Next(1, height - 1);
                mask[px, py] = false; // re-solidify cell
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Formation helpers (stalactites / stalagmites)
        // ─────────────────────────────────────────────────────────────────────
        private void PlaceFormations(
            Cave cave, MineData mineData,
            int xMin, int xMax, int targetY,
            int count, bool isStalagmite)
        {
            var candidates = new List<int>();
            for (var x = xMin + 1; x < xMax - 1; x++)
                candidates.Add(x);

            Shuffle(candidates);
            count = Math.Min(count, candidates.Count);

            for (var i = 0; i < count; i++)
            {
                var cell = GetCell(mineData, candidates[i], targetY);
                if (cell == null) continue;

                // Re-solidify the formation cell
                cell.IsBroken = false;
                cell.IsRevealed = true;
                cell.IsBreakable = true;

                if (isStalagmite) cave.StalagmiteCellIds.Add(cell.Id);
                else cave.StalactiteCellIds.Add(cell.Id);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Utility
        // ─────────────────────────────────────────────────────────────────────
        private static Cell GetCell(MineData mineData, int x, int y)
            => mineData.Cells.FirstOrDefault(c => c.Position.X == (x - mineData.GridWidth / 2) && c.Position.Y == -y);

        private (int W, int H) PickPreset()
            => CavePresets[_rand.Next(CavePresets.Length)];

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