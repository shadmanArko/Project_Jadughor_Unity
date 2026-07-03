using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Auto-spawns the six category buttons in the Bottom Bar. Each button raises
    /// <see cref="BuilderActions.OnBottomPanelBuilderCardToggleClicked"/> for its
    /// category — the <c>BuilderPanelController</c> handles opening/closing the panel.
    /// Pure emitter, so no subscriptions to clean up.
    /// </summary>
    public class BottomBarBuilderController : MonoBehaviour
    {
        [Tooltip("Button prefab spawned once per category. If it has a TMP_Text in " +
                 "its children, it's set to the category's display name.")]
        [SerializeField] private Button categoryButtonPrefab;
        [Tooltip("Where the buttons are parented. Defaults to this object's transform.")]
        [SerializeField] private Transform buttonParent;

        // Display names + order for the six category buttons.
        private static readonly (BuilderCardType type, string label)[] Categories =
        {
            (BuilderCardType.Exhibit,         "Exhibits"),
            (BuilderCardType.DecorationShop,  "Shops"),
            (BuilderCardType.DecorationOther, "Decorations"),
            (BuilderCardType.Sanitation,      "Sanitation"),
            (BuilderCardType.Flooring,        "Flooring"),
            (BuilderCardType.Wallpaper,       "Wallpaper"),
        };

        private void Start()
        {
            if (categoryButtonPrefab == null)
            {
                Debug.LogError("[BottomBarBuilderController] Category Button Prefab not assigned.", this);
                return;
            }
            if (buttonParent == null) buttonParent = transform;

            foreach (var (type, label) in Categories)
            {
                BuilderCardType captured = type; // avoid closure capture of the loop var
                Button btn = Instantiate(categoryButtonPrefab, buttonParent);
                btn.name = $"BuilderCategory_{label}";

                var text = btn.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = label;

                btn.onClick.AddListener(() =>
                    BuilderActions.OnBottomPanelBuilderCardToggleClicked?.Invoke(captured));
            }
        }
    }
}
