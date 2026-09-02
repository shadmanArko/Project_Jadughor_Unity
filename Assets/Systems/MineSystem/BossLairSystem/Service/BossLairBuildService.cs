using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using UnityEngine;
using Random = System.Random;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Builds the arena for a selected boss: resolves where it sits, instantiates
    /// the prefab, paints the sealed shell and backdrop, and scatters decor.
    /// </summary>
    /// <remarks>
    /// Runs during mine generation, once, and only when a boss gate was placed.
    /// The arena is left dormant until the player enters.
    /// </remarks>
    public sealed class BossLairBuildService
    {
        private readonly BossLairPlacementService _placement;
        private readonly BossLairCameraService _camera;
        private readonly BossLairShellGenerationService _shell;
        private readonly BossLairDecorService _decor;
        private readonly BossLairFactory _factory;
        private readonly BossLairBossFactory _bossFactory;
        private readonly BossLairModel _model;
        private readonly BossLairConfig _config;
        private readonly Random _random = new();

        public BossLairBuildService(
            BossLairPlacementService placement,
            BossLairCameraService camera,
            BossLairShellGenerationService shell,
            BossLairDecorService decor,
            BossLairFactory factory,
            BossLairBossFactory bossFactory,
            BossLairModel model,
            BossLairConfig config)
        {
            _placement = placement;
            _camera = camera;
            _shell = shell;
            _decor = decor;
            _factory = factory;
            _bossFactory = bossFactory;
            _model = model;
            _config = config;
        }

        public bool Build(BossProfileScriptable profile)
        {
            Teardown();

            var lairConfig = profile?.ProceduralLairConfig;
            if (lairConfig == null)
            {
                Debug.LogError(
                    $"[BossLair] {profile?.DisplayName ?? "Boss"} has no " +
                    "procedural lair config, so no arena was built.");
                return false;
            }
            if (!lairConfig.Validate(out var configError))
            {
                Debug.LogError($"[BossLair] {configError}");
                return false;
            }

            var placement = _placement.Resolve(
                lairConfig, ResolveEffectiveGap(lairConfig));
            _model.SetPlacement(placement);

            var view = _factory.Create(placement);
            _shell.Generate(view, lairConfig, placement);
            _decor.Decorate(view, placement, ResolveDecorSeed());

            if (profile.BossConfig is HedgehogBossConfigScriptable hedgehogConfig)
                _bossFactory.Create(view, hedgehogConfig);

            WarnIfArenaIsSmallerThanFrame(placement, lairConfig);
            Debug.Log(
                $"[BossLair] Built {profile.DisplayName} arena " +
                $"{placement.WidthInCells}x{placement.HeightInCells} at cells " +
                $"x {placement.InteriorCells.xMin}..{placement.InteriorCells.xMax - 1}, " +
                $"y {placement.InteriorCells.yMin}..{placement.TopCellY}.");
            return true;
        }

        public void Teardown()
        {
            _bossFactory.Destroy();
            _shell.Clear(_factory.Active);
            _factory.Destroy();
        }

        /// <summary>
        /// The configured gap, raised if the camera window would otherwise reach
        /// the mine. When the arena is smaller than the window the camera holds a
        /// fixed shot whose frame overhangs the arena, and only the gap keeps the
        /// mine off screen.
        /// </summary>
        private int ResolveEffectiveGap(BossProceduralLairConfig lairConfig)
        {
            var windowCells = _camera.ResolveWindowHeightInCells(
                lairConfig.LairAssetsPPU, _placement.CellWorldSize);
            var required = _placement.ResolveRequiredGapInCells(
                lairConfig, windowCells);
            var configured = lairConfig.GapBelowMineInCells;
            if (required <= configured)
                return configured;

            Debug.Log(
                $"[BossLair] Gap raised from {configured} to {required} cells so " +
                $"the mine stays outside the camera frame " +
                $"({windowCells:0.#} cells tall at {lairConfig.LairAssetsPPU} PPU).");
            return required;
        }

        private void WarnIfArenaIsSmallerThanFrame(
            BossLairPlacement placement,
            BossProceduralLairConfig lairConfig)
        {
            if (_camera.CanConfine(placement, lairConfig.LairAssetsPPU))
                return;
            var window = _camera.ResolveWorldWindow(lairConfig.LairAssetsPPU);
            var size = placement.InteriorWorldSize;
            Debug.Log(
                $"[BossLair] Arena ({size.x:0.##} x {size.y:0.##} world units) is " +
                $"not larger than the camera frame ({window.x:0.##} x " +
                $"{window.y:0.##}) at {lairConfig.LairAssetsPPU} PPU, so the " +
                "camera will hold a fixed shot showing the whole arena. Raise " +
                "lairAssetsPPU to zoom in further, or enlarge the arena to make " +
                "the camera follow the player.");
        }

        private int ResolveDecorSeed() =>
            _config.UseFixedDecorSeed ? _config.FixedDecorSeed : _random.Next();
    }
}
