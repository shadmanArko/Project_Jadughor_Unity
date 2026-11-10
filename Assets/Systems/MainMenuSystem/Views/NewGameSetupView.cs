using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MainMenuSystem.Views
{
    /// <summary>
    /// Port of Godot's <c>NewGameSetupUi.tscn</c>: name field, gender dropdown,
    /// "Show Tutorial" switch, Start button, the "Please Enter Your Name." warning
    /// and the full-screen loading panel.
    ///
    /// Pure view — no logic. <see cref="Controllers.NewGameSetupController"/> drives it.
    /// Built at runtime by <see cref="NewGameSetupBuilder"/> unless a prefab is
    /// assigned on the MainMenuInstaller, in which case its references are used as-is.
    /// </summary>
    public class NewGameSetupView : MonoBehaviour
    {
        [Header("Setup Panel")]
        [SerializeField] private GameObject setupPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Dropdown genderDropdown;
        [SerializeField] private Toggle tutorialToggle;
        [SerializeField] private Button startButton;

        [Header("Warning (empty name)")]
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private Button warningOkayButton;

        [Header("Loading Panel")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text loadingLabel;
        [SerializeField] private Image loadingFill;
        [SerializeField] private TMP_Text loadingPercent;

        public TMP_InputField NameInput => nameInput;
        public TMP_Dropdown GenderDropdown => genderDropdown;
        public Toggle TutorialToggle => tutorialToggle;
        public Button StartButton => startButton;
        public Button WarningOkayButton => warningOkayButton;

        public string PlayerName => nameInput.text.Trim();
        public string Gender => genderDropdown.options[genderDropdown.value].text;
        public bool TutorialEnabled => tutorialToggle.isOn;

        /// <summary>Used by the builder to wire the generated hierarchy.</summary>
        public void Bind(GameObject setup, TMP_InputField input, TMP_Dropdown gender, Toggle tutorial,
            Button start, GameObject warning, Button warningOkay, GameObject loading, TMP_Text loadingText,
            Image fill, TMP_Text percent)
        {
            loadingPercent = percent;
            setupPanel = setup;
            nameInput = input;
            genderDropdown = gender;
            tutorialToggle = tutorial;
            startButton = start;
            warningPanel = warning;
            warningOkayButton = warningOkay;
            loadingPanel = loading;
            loadingLabel = loadingText;
            loadingFill = fill;
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        public void SetWarningVisible(bool visible) => warningPanel.SetActive(visible);

        public void SetLoadingVisible(bool visible)
        {
            loadingPanel.SetActive(visible);
            setupPanel.SetActive(!visible);
        }

        public void SetLoadingText(string text) => loadingLabel.text = text;

        /// <param name="progress01">0..1</param>
        public void SetLoadingProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);
            loadingFill.fillAmount = progress01;
            if (loadingPercent != null) loadingPercent.text = $"{Mathf.RoundToInt(progress01 * 100)}%";
        }
    }
}
