using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Systems.MineSystem.EnemySystem.Service
{
    /// <summary>
    /// Opt-in tracing for hard-to-reproduce enemy AI stalls (stuck fall states,
    /// reposition loops, relocation churn).
    ///
    /// Every method is <see cref="ConditionalAttribute"/> on
    /// <c>ENEMY_DIAGNOSTICS</c>, so without that define the calls — and their
    /// argument evaluation, including string interpolation — are removed by the
    /// compiler. That keeps this free on the FixedUpdate path in normal builds.
    ///
    /// Enable via Project Settings > Player > Scripting Define Symbols, and
    /// remove it again before shipping.
    /// </summary>
    public static class EnemyDiagnosticsLog
    {
        private const string Define = "ENEMY_DIAGNOSTICS";
        private const string Prefix = "[EnemyDiag]";

        [Conditional(Define)]
        public static void Log(Guid enemyId, string message) =>
            Debug.Log($"{Prefix} {Short(enemyId)} {message}");

        [Conditional(Define)]
        public static void Warn(Guid enemyId, string message) =>
            Debug.LogWarning($"{Prefix} {Short(enemyId)} {message}");

        private static string Short(Guid enemyId) =>
            enemyId.ToString("N").Substring(0, 8);
    }
}
