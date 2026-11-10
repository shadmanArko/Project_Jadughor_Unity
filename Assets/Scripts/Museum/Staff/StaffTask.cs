using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Staff
{
    /// <summary>
    /// One unit of staff work: walk to <see cref="StandCell"/>, face <see cref="FaceCell"/>,
    /// play an animation for <see cref="Duration"/> seconds, then <see cref="OnCompleted"/>.
    /// Roles build these (sweep this trash, clean that exhibit); <see cref="StaffAgent"/> runs
    /// them. Eating is a task too, so every role gets it for free.
    /// </summary>
    public sealed class StaffTask
    {
        public string Name;
        public Vector2Int StandCell;
        /// <summary>Cell to turn toward on arrival. Null keeps the walking direction.</summary>
        public Vector2Int? FaceCell;
        public string FrontState;
        public string BackState;
        public float Duration = 2f;

        /// <summary>
        /// Exclusive claim (e.g. "trash:&lt;id&gt;") so two staff never take the same job.
        /// Null = no claim needed.
        /// </summary>
        public string ClaimKey;

        /// <summary>Re-checked on arrival; false aborts (the trash was already swept, the exhibit sold).</summary>
        public Func<bool> IsStillValid;
        public Action OnCompleted;
        public Action OnAborted;
    }

    /// <summary>
    /// Who is doing what. Static because staff of different roles may compete for the same
    /// job later (a guard and a janitor both reacting to a spill). Cleared on entering Play mode,
    /// so it also behaves with domain reload disabled.
    /// </summary>
    public static class StaffTaskBoard
    {
        private static readonly Dictionary<string, StaffAgent> Claims = new Dictionary<string, StaffAgent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Claims.Clear();

        public static bool IsClaimed(string key, StaffAgent except = null) =>
            !string.IsNullOrEmpty(key) && Claims.TryGetValue(key, out var owner) && owner != null && owner != except;

        public static bool TryClaim(string key, StaffAgent owner)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (IsClaimed(key, owner)) return false;
            Claims[key] = owner;
            return true;
        }

        public static void Release(string key, StaffAgent owner)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (Claims.TryGetValue(key, out var current) && current == owner) Claims.Remove(key);
        }
    }
}
