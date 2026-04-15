using UnityEngine;
using UnityEngine.UI;

namespace Systems.MainMenuSystem.Views
{
    public class MainMenuView : MonoBehaviour
    {
        [Header("Buttons")] 
        
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button exitButton;

        [Header("Optional UI")] 
        [SerializeField] private GameObject noSaveLabel;

        // ── IMainMenuView: Input Streams ──────────────────────────────────
        public Button NewGameButton => newGameButton;
        public Button ContinueButton => continueButton;
        public Button OptionsButton => optionsButton;
        public Button ExitButton => exitButton;

        // // ── IInitializable ────────────────────────────────────────────────
        // public void Start()
        // {
        //     // Start hidden — Controller will call Show() when ready
        //     Hide();
        //     Debug.Log("[View] Initialized.");
        // }
        //
        // // ── IMainMenuView: Display Commands ───────────────────────────────
        // public void SetContinueInteractable(bool interactable)
        // {
        //     continueButton.interactable = interactable;
        //
        //     if (noSaveLabel != null)
        //         noSaveLabel.SetActive(!interactable);
        // }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}