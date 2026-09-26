using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Town
{
    /// <summary>
    /// The icon column on the right edge of the museum screen — the port of Godot's
    /// <c>Museum ui.tscn → Right Panel</c> (Town Map on top, Digging and Permits below).
    ///
    /// The canvas and buttons are authored in the Right Side Canvas prefab (made by
    /// Tools ▸ Project Museum ▸ Town ▸ Build Right Side Canvas) — move, restyle or add
    /// buttons there. Each button's OnClick is wired to one of the methods below in the
    /// inspector, so a new button only needs a new public method here.
    /// </summary>
    public class RightSideButtonsController : MonoBehaviour
    {
        [Tooltip("Buttons whose system isn't ported yet. They're shown but not clickable.")]
        [SerializeField] private Button[] notImplementedButtons;

        private void Awake()
        {
            foreach (Button button in notImplementedButtons)
                if (button != null) button.interactable = false;
        }

        /// <summary>Town Map button.</summary>
        public void OpenTownMap() => TownMapController.RequestOpen();

        /// <summary>Digging and Permits button — no Unity port of that menu yet.</summary>
        public void OpenDiggingPermits() => Debug.Log("[RightSideButtons] Digging & Permits is not ported yet.");
    }
}
