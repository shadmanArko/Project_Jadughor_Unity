using System.Collections;
using System.Collections.Generic;
using ProjectMuseum.Data;
using ProjectMuseum.Narrative;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace ProjectMuseum.MapSelection
{
    /// <summary>
    /// Digging &amp; Permits map panel — port of Godot's <c>DiggingAndPermitsMenu</c> + <c>SceneChanger</c>.
    ///
    /// The panel is authored in the Map Selection Canvas prefab (Tools ▸ Project Museum ▸ HUD ▸
    /// Build Map Selection Panel). This script only:
    /// - shows / hides it (opened by the right-side Digging button, closed by X or Esc),
    /// - cycles regions with the arrows and fills the site list from <see cref="MapSelectionData"/>
    ///   by cloning the Site Button Template (style the template to restyle every site),
    /// - on a site click: tutorial "SelectedMineSite", save, hand the site to the mine, show
    ///   Loading…, then load the mine scene.
    /// </summary>
    public class MapSelectionPanelController : MonoBehaviour
    {
        [Header("Panel (set by the build tool)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text regionLabel;
        [SerializeField] private Button previousRegionButton;
        [SerializeField] private Button nextRegionButton;
        [SerializeField] private RectTransform siteListContent;
        [Tooltip("Inactive button cloned once per site. Its TMP_Text child gets the site name.")]
        [SerializeField] private Button siteButtonTemplate;
        [Tooltip("Shown when the current region has no sites.")]
        [SerializeField] private GameObject emptyListMessage;
        [SerializeField] private GameObject loadingOverlay;

        [Header("Data")]
        [SerializeField] private MapSelectionData data;
        [Tooltip("Mine assets that receive the chosen region / site.")]
        [SerializeField] private MinePlayerScriptable minePlayer;
        [SerializeField] private ArtifactGenerationConfig mineArtifactConfig;

        [Header("Scene change")]
        [Tooltip("Scene name in Build Settings.")]
        [SerializeField] private string mineSceneName = "Mine";
        [Tooltip("Pause before loading, so the Loading… overlay is seen (Godot: 1 s).")]
        [SerializeField] private float loadDelay = 0.5f;

        [Inject] private MuseumDataModel _model;

        private readonly List<Button> _siteButtons = new();
        private int _regionIndex;
        private bool _loading;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (siteButtonTemplate != null) siteButtonTemplate.gameObject.SetActive(false);
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable() => MuseumActions.OnClickDiggingPermitsButton += Open;
        private void OnDisable() => MuseumActions.OnClickDiggingPermitsButton -= Open;

        private void Update()
        {
            if (!IsOpen || _loading) return;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                Close();
        }

        // ── Open / close (buttons call these) ───────────────────────────

        public void Open()
        {
            if (panelRoot == null || _loading) return;
            panelRoot.SetActive(true);
            ShowRegion(_regionIndex);
        }

        public void Close()
        {
            if (_loading) return;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void NextRegion() => ShowRegion(_regionIndex + 1);

        public void PreviousRegion() => ShowRegion(_regionIndex - 1);

        // ── List ────────────────────────────────────────────────────────

        private void ShowRegion(int index)
        {
            int count = data != null ? data.regions.Count : 0;
            _regionIndex = count == 0 ? 0 : (index % count + count) % count;
            MapRegion region = count == 0 ? null : data.regions[_regionIndex];

            if (regionLabel != null) regionLabel.text = region != null ? region.displayName : "NO REGIONS";
            bool canCycle = count > 1;
            if (previousRegionButton != null) previousRegionButton.interactable = canCycle;
            if (nextRegionButton != null) nextRegionButton.interactable = canCycle;

            foreach (Button b in _siteButtons) if (b != null) Destroy(b.gameObject);
            _siteButtons.Clear();

            int siteCount = region?.sites.Count ?? 0;
            if (emptyListMessage != null) emptyListMessage.SetActive(siteCount == 0);
            if (siteButtonTemplate == null || siteListContent == null) return;

            for (int i = 0; i < siteCount; i++)
            {
                MapSite site = region.sites[i];
                Button button = Instantiate(siteButtonTemplate, siteListContent);
                button.name = $"Site_{site.displayName}";
                button.gameObject.SetActive(true);
                button.interactable = site.available;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = site.available ? site.displayName : $"{site.displayName} (no permit)";

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectSite(region, site));
                _siteButtons.Add(button);
            }
        }

        // ── Go to the mine ──────────────────────────────────────────────

        private void SelectSite(MapRegion region, MapSite site)
        {
            if (_loading || !site.available) return;

            if (!Application.CanStreamedLevelBeLoaded(mineSceneName))
            {
                Debug.LogError($"[MapSelection] Scene '{mineSceneName}' is not in Build Settings. " +
                               "Run Tools ▸ Project Museum ▸ HUD ▸ Build Map Selection Panel (it adds it) " +
                               "or add it in File ▸ Build Profiles.");
                return;
            }

            _loading = true;
            MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke("SelectedMineSite");

            // Save first, like Godot (time included — it lives in the museum data).
            MuseumActions.OnPlayerSavedGame?.Invoke();
            _model?.Save();

            MineSiteSelection.Select(region.region, site.site, minePlayer, mineArtifactConfig);
            if (loadingOverlay != null) loadingOverlay.SetActive(true);
            StartCoroutine(LoadMine());
        }

        private IEnumerator LoadMine()
        {
            yield return new WaitForSecondsRealtime(loadDelay);
            Time.timeScale = 1f; // the museum clock may have paused gameplay
            SceneManager.LoadSceneAsync(mineSceneName);
        }
    }
}
