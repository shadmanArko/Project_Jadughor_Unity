using System.Collections;
using DG.Tweening;
using ProjectMuseum.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Town
{
    /// <summary>
    /// Turns the town map on and off and decides what it says — the port of Godot's
    /// <c>TownController</c> + <c>TownUi</c>.
    ///
    /// Everything it drives is authored in the Town prefab (made by Tools ▸ Project Museum ▸
    /// Town ▸ Build Town) and edited there; this script only toggles objects:
    /// - Open: map panel on, town camera on (it renders only while the map is up), view reset.
    /// - Close: both off. Triggered by the X button, Esc, the toggle key, or gamepad B.
    /// - Rules: which popup text shows for which event, for how long, and when the
    ///   digging-buddy pointer shows / hides.
    ///
    /// The museum camera is untouched: the full-screen backdrop is UI, so the existing
    /// "pointer over UI" checks already stop world zoom, clicks and placement underneath.
    /// </summary>
    public class TownMapController : MonoBehaviour
    {
        [Header("References (set by the Build Town tool)")]
        [Tooltip("The UI panel holding backdrop, map image, header and close button.")]
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private TownMapView mapView;
        [Tooltip("Popup root, hidden when no message is showing.")]
        [SerializeField] private GameObject popup;
        [SerializeField] private TMP_Text popupText;
        [Tooltip("The pointer sprite next to the digging buddy's house, in the town world.")]
        [SerializeField] private Transform diggingBuddyIndicator;

        [Header("Rules — what to say when")]
        [Tooltip("Shown when a living house with nobody home is clicked. Empty = no popup.")]
        [SerializeField] private string emptyHouseMessage = "Looks like no one is home.";
        [Tooltip("Seconds the empty-house popup stays up (Godot: 1).")]
        [SerializeField] private float emptyHouseMessageDuration = 1f;
        [Tooltip("After an empty house is clicked, point at the digging buddy's house.")]
        [SerializeField] private bool pointToBuddyAfterEmptyHouse = true;
        [Tooltip("Shown when the digging buddy's house is clicked. Empty = no popup.")]
        [SerializeField] private string buddyFoundMessage = "";
        [SerializeField] private float buddyFoundMessageDuration = 1.5f;

        [Header("Pointer animation")]
        [Tooltip("How far the pointer bobs away from the house, in world units.")]
        [SerializeField] private float indicatorBob = 0.04f;
        [SerializeField] private float indicatorBobDuration = 0.6f;

        [Header("Input")]
        [Tooltip("Keyboard shortcut that opens / closes the map. None = off.")]
        [SerializeField] private Key toggleKey = Key.M;
        [Tooltip("Gamepad View/Select opens / closes the map; B always closes it.")]
        [SerializeField] private bool gamepadToggle = true;

        private bool _open;
        private bool _buddyFound;
        private Vector3 _indicatorHome;
        private Coroutine _popupRoutine;

        public bool IsOpen => _open;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (diggingBuddyIndicator != null) _indicatorHome = diggingBuddyIndicator.localPosition;

            // The prefab is left visible so it can be edited; the game always starts closed.
            ApplyOpen(false);
            HidePopup();
            SetIndicator(false);
        }

        private void OnEnable()
        {
            MuseumActions.OnTownMapButtonClicked += Open;
            MuseumActions.OnClickCloseTownUi += Close;
            MuseumActions.OnPlayerClickedAnEmptyHouse += OnEmptyHouseClicked;
            MuseumActions.OnPlayerPerformedTutorialRequiringAction += OnTutorialAction;
        }

        private void OnDisable()
        {
            MuseumActions.OnTownMapButtonClicked -= Open;
            MuseumActions.OnClickCloseTownUi -= Close;
            MuseumActions.OnPlayerClickedAnEmptyHouse -= OnEmptyHouseClicked;
            MuseumActions.OnPlayerPerformedTutorialRequiringAction -= OnTutorialAction;
            if (diggingBuddyIndicator != null) diggingBuddyIndicator.DOKill();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool toggle = (kb != null && toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame) ||
                          (gamepadToggle && pad != null && pad.selectButton.wasPressedThisFrame);

            if (!_open)
            {
                if (toggle) RequestOpen();
                return;
            }

            if (toggle ||
                (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                RequestClose();
        }

        // ── Open / close ────────────────────────────────────────────────

        /// <summary>
        /// What the Town Map button (or hotkey) does: open the map and tell the tutorial,
        /// like Godot's <c>MuseumUi.TownMapButtonOnPressed</c>.
        /// </summary>
        public static void RequestOpen()
        {
            MuseumActions.OnTownMapButtonClicked?.Invoke();
            MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke("ClickedTownMap");
        }

        /// <summary>Hooked to the X button. Raises the event so other systems hear it too.</summary>
        public void RequestClose() => MuseumActions.OnClickCloseTownUi?.Invoke();

        private void Open()
        {
            if (_open) return;
            ApplyOpen(true);
            if (mapView != null) mapView.ResetView();
        }

        private void Close()
        {
            if (!_open) return;
            HidePopup();
            ApplyOpen(false);
        }

        private void ApplyOpen(bool open)
        {
            _open = open;
            if (mapPanel != null) mapPanel.SetActive(open);
            if (mapView != null && mapView.TownCamera != null) mapView.TownCamera.enabled = open;
        }

        // ── Rules ───────────────────────────────────────────────────────

        private void OnEmptyHouseClicked()
        {
            if (!_open) return;
            ShowPopup(emptyHouseMessage, emptyHouseMessageDuration);
            if (pointToBuddyAfterEmptyHouse && !_buddyFound) SetIndicator(true);
        }

        private void OnTutorialAction(string action)
        {
            if (action != "FoundDiggingBuddy") return;
            _buddyFound = true;
            SetIndicator(false);
            if (_open) ShowPopup(buddyFoundMessage, buddyFoundMessageDuration);
        }

        // ── Toggles ─────────────────────────────────────────────────────

        private void ShowPopup(string message, float duration)
        {
            if (popup == null || string.IsNullOrEmpty(message)) return;
            if (_popupRoutine != null) StopCoroutine(_popupRoutine);
            if (popupText != null) popupText.text = message;
            popup.SetActive(true);
            _popupRoutine = StartCoroutine(HideAfter(duration));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            HidePopup();
        }

        private void HidePopup()
        {
            if (_popupRoutine != null) StopCoroutine(_popupRoutine);
            _popupRoutine = null;
            if (popup != null) popup.SetActive(false);
        }

        private void SetIndicator(bool on)
        {
            if (diggingBuddyIndicator == null) return;

            diggingBuddyIndicator.DOKill();
            diggingBuddyIndicator.localPosition = _indicatorHome;
            diggingBuddyIndicator.gameObject.SetActive(on);
            if (!on) return;

            // Godot's "indicate" animation: ping-pong away from the house.
            diggingBuddyIndicator.DOLocalMoveX(_indicatorHome.x - indicatorBob, indicatorBobDuration)
                                 .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
    }
}
