namespace ProjectMuseum.Builder
{
    /// <summary>What a bottom-bar sub-button does when clicked.</summary>
    public enum BottomBarActionKind
    {
        /// <summary>Opens the builder panel listing a <see cref="BuilderCardType"/>'s objects.</summary>
        OpenBuilderCategory,

        /// <summary>No system behind it yet — the button is built but rendered disabled.</summary>
        NotImplemented
    }

    /// <summary>One sub-button in the second row of the bottom bar.</summary>
    public readonly struct BottomBarSubItem
    {
        public readonly string Label;
        public readonly BottomBarActionKind Kind;
        public readonly BuilderCardType CardType;

        private BottomBarSubItem(string label, BottomBarActionKind kind, BuilderCardType cardType)
        {
            Label = label;
            Kind = kind;
            CardType = cardType;
        }

        /// <summary>A sub-button that opens the builder panel for <paramref name="cardType"/>.</summary>
        public static BottomBarSubItem Opens(string label, BuilderCardType cardType) =>
            new BottomBarSubItem(label, BottomBarActionKind.OpenBuilderCategory, cardType);

        /// <summary>A placeholder sub-button for a system that doesn't exist yet.</summary>
        public static BottomBarSubItem Todo(string label) =>
            new BottomBarSubItem(label, BottomBarActionKind.NotImplemented, default);
    }

    /// <summary>One top-level button in the first row, plus the sub-buttons it reveals.</summary>
    public readonly struct BottomBarCategory
    {
        public readonly string Label;
        public readonly BottomBarSubItem[] Items;

        public BottomBarCategory(string label, params BottomBarSubItem[] items)
        {
            Label = label;
            Items = items;
        }
    }

    /// <summary>
    /// The bottom bar's two-level menu. Row one is <see cref="Categories"/>; clicking
    /// one collapses that row and slides in its <see cref="BottomBarCategory.Items"/>.
    ///
    /// Only the items built with <c>Opens(...)</c> are wired to a real system — they
    /// raise <see cref="BuilderActions.OnBottomPanelBuilderCardToggleClicked"/> and the
    /// existing panel/placement code handles them unchanged. Everything built with
    /// <c>Todo(...)</c> renders greyed out until its system lands, so the bar shows the
    /// finished shape without any button silently doing nothing.
    ///
    /// Editing this table is the only thing needed to add or re-label a button.
    /// </summary>
    public static class BottomBarMenu
    {
        public static readonly BottomBarCategory[] Categories =
        {
            new BottomBarCategory("Administration",
                BottomBarSubItem.Todo("Museum Overview"),
                BottomBarSubItem.Todo("Marketing"),
                BottomBarSubItem.Todo("Finances")),

            new BottomBarCategory("Exhibits",
                BottomBarSubItem.Opens("New Exhibit", BuilderCardType.Exhibit),
                BottomBarSubItem.Todo("Edit Exhibit"),
                BottomBarSubItem.Todo("Clear Exhibit")),

            new BottomBarCategory("Facilities",
                // The JSON has one flat shop list, so this is "Shops" rather than the
                // separate Food and Drinks / Souvenirs buttons of the mock-up.
                BottomBarSubItem.Opens("Shops", BuilderCardType.DecorationShop),
                BottomBarSubItem.Opens("Sanitation", BuilderCardType.Sanitation),
                BottomBarSubItem.Todo("Education"),
                BottomBarSubItem.Todo("Seating"),
                BottomBarSubItem.Todo("Play Areas")),

            new BottomBarCategory("Rooms",
                BottomBarSubItem.Opens("Flooring", BuilderCardType.Flooring),
                BottomBarSubItem.Opens("Wallpaper", BuilderCardType.Wallpaper),
                BottomBarSubItem.Opens("Decorations", BuilderCardType.DecorationOther),
                BottomBarSubItem.Todo("Walls"),
                BottomBarSubItem.Todo("Lighting"),
                BottomBarSubItem.Todo("Security")),

            new BottomBarCategory("Staff",
                BottomBarSubItem.Todo("Staff Overview"),
                BottomBarSubItem.Todo("Hiring"),
                BottomBarSubItem.Todo("Staff Facilities"),
                BottomBarSubItem.Todo("Staff Zoning")),

            new BottomBarCategory("Zoning",
                BottomBarSubItem.Todo("New Zone"),
                BottomBarSubItem.Todo("Edit Zones")),
        };
    }
}
