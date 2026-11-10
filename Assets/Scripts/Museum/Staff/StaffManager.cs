using System;
using System.Collections.Generic;
using ProjectMuseum.Data;
using ProjectMuseum.Narrative;
using UnityEngine;
using Zenject;

namespace ProjectMuseum.Staff
{
    /// <summary>
    /// Recruiting, firing and paying museum staff.
    ///
    /// - <see cref="Hire"/>: pays the hire cost, clones the role's template at the entrance tile
    ///   (the inside door cell) and the new hire walks to its home tile.
    /// - <see cref="Fire"/>: the staff member drops their task, walks out through the entrance,
    ///   and is removed.
    /// - Wages: every new game day, each staff member's daily wage is deducted.
    /// - Staff placed in the scene by hand count as already hired (no fee) and register themselves.
    ///
    /// Optional — a hand-placed janitor works without one. Keep it under the SceneContext so new
    /// hires get MuseumDataModel injected. No UI yet: use the context menu or call from code.
    /// </summary>
    [AddComponentMenu("Project Museum/Staff/Staff Manager")]
    public class StaffManager : MonoBehaviour
    {
        [Serializable]
        public class RoleConfig
        {
            public StaffRole role = StaffRole.Janitor;
            [Tooltip("Prefab (or scene object) to clone. Empty = clone the first staff member of " +
                     "this role already in the scene.")]
            public GameObject template;
            [Min(0f)] public float hireCost = 150f;
            [Min(0f)] public float dailyWage = 40f;
        }

        [SerializeField] private List<RoleConfig> roles = new List<RoleConfig> { new RoleConfig() };
        [Tooltip("Museum floor tile new hires appear on and fired staff leave by. " +
                 "Match GuestController's Door Inside Cell.")]
        [SerializeField] private Vector2Int entranceCell = new Vector2Int(5, 0);
        [SerializeField] private bool payDailyWages = true;
        [SerializeField] private Transform staffParent;

        [InjectOptional] private MuseumDataModel _model;
        [InjectOptional] private DiContainer _container;

        private readonly List<StaffAgent> _staff = new List<StaffAgent>();
        private int _lastDayKey = -1;

        public IReadOnlyList<StaffAgent> Staff => _staff;
        public Vector2Int EntranceCell => entranceCell;
        public event Action<StaffAgent> Hired;
        public event Action<StaffAgent> Fired;

        private void OnEnable() => MuseumActions.OnTimeUpdated += OnClockTick;
        private void OnDisable() => MuseumActions.OnTimeUpdated -= OnClockTick;

        // ── Roster ─────────────────────────────────────────────────────────

        /// <summary>Hand-placed staff call this from their Start. No fee.</summary>
        public void Register(StaffAgent agent)
        {
            if (agent != null && !_staff.Contains(agent)) _staff.Add(agent);
        }

        public void Unregister(StaffAgent agent) => _staff.Remove(agent);

        public RoleConfig GetConfig(StaffRole role) => roles.Find(r => r.role == role);

        public int CountOf(StaffRole role)
        {
            var n = 0;
            foreach (var s in _staff) if (s != null && s.Role == role) n++;
            return n;
        }

        // ── Hire / fire ────────────────────────────────────────────────────

        /// <summary>
        /// Recruit one staff member who will live on <paramref name="homeCell"/>.
        /// Returns null if the role isn't configured, there's no template, or funds are short.
        /// </summary>
        public StaffAgent Hire(StaffRole role, Vector2Int homeCell)
        {
            var config = GetConfig(role);
            if (config == null)
            {
                Debug.LogWarning($"[StaffManager] No config for role {role}.", this);
                return null;
            }

            var template = config.template != null ? config.template : FindSceneTemplate(role);
            if (template == null)
            {
                Debug.LogWarning($"[StaffManager] No template for {role} — assign one, or place one in the scene.", this);
                return null;
            }

            if (_model != null && !_model.CanAfford(config.hireCost))
            {
                Debug.Log($"[StaffManager] Can't afford a {role} (${config.hireCost}, funds ${_model.Money.Value}).", this);
                return null;
            }

            var parent = staffParent != null ? staffParent : transform;
            var go = _container != null ? _container.InstantiatePrefab(template, parent) : Instantiate(template, parent);
            go.name = $"{role} (hired)";
            go.SetActive(true);

            var agent = go.GetComponent<StaffAgent>();
            if (agent == null)
            {
                Debug.LogError($"[StaffManager] Template '{template.name}' has no StaffAgent.", this);
                Destroy(go);
                return null;
            }

            _model?.AddMoney(-config.hireCost);
            agent.BeginHired(entranceCell, homeCell, this);
            _staff.Add(agent);
            Hired?.Invoke(agent);
            StaffActions.OnStaffHired?.Invoke(agent);
            Debug.Log($"[StaffManager] Hired {role} for ${config.hireCost}; home {homeCell}.", this);
            return agent;
        }

        /// <summary>The staff member walks out through the entrance, then is destroyed.</summary>
        public void Fire(StaffAgent agent)
        {
            if (agent == null || !_staff.Remove(agent)) return;
            Fired?.Invoke(agent);
            StaffActions.OnStaffFired?.Invoke(agent);
            agent.Dismiss(entranceCell, () => Destroy(agent.gameObject));
        }

        private GameObject FindSceneTemplate(StaffRole role)
        {
            foreach (var s in _staff)
                if (s != null && s.Role == role) return s.gameObject;
            return null;
        }

        // ── Wages ──────────────────────────────────────────────────────────

        private void OnClockTick(int minutes, int hours, int days, int months, int years)
        {
            var key = (years * 4 + months) * 31 + days;
            if (_lastDayKey < 0) { _lastDayKey = key; return; }
            if (key == _lastDayKey) return;
            _lastDayKey = key;

            if (!payDailyWages || _model == null) return;
            var total = 0f;
            foreach (var s in _staff)
            {
                if (s == null) continue;
                var config = GetConfig(s.Role);
                if (config != null) total += config.dailyWage;
            }

            if (total <= 0f) return;
            _model.AddMoney(-total);
            Debug.Log($"[StaffManager] Paid ${total} in wages to {_staff.Count} staff.", this);
        }

        // ── Debug ──────────────────────────────────────────────────────────

        [ContextMenu("Hire Janitor (random home tile)")]
        private void DebugHireJanitor()
        {
            if (_model == null || _model.Tiles.Count == 0) return;
            for (var i = 0; i < 30; i++)
            {
                var tile = _model.Tiles[UnityEngine.Random.Range(0, _model.Tiles.Count)];
                if (!tile.Walkable) continue;
                Hire(StaffRole.Janitor, tile.Cell);
                return;
            }
        }

        [ContextMenu("Fire Last Staff Member")]
        private void DebugFireLast()
        {
            if (_staff.Count > 0) Fire(_staff[_staff.Count - 1]);
        }
    }
}
