namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Known clip lists for the existing character sheets, so a character can be set up with one
    /// click instead of retyping ranges that are already established.
    ///
    /// Ranges, rates and loop flags are taken from the Godot <c>.tscn</c> clips and the frame
    /// counts were verified against the sheets' alpha channels. See <c>SheetRowMaps.md</c> for the
    /// per-row breakdown and the two annotated discrepancies.
    /// </summary>
    internal static class FrameClipPresets
    {
        internal readonly struct Preset
        {
            public readonly string Label;
            public readonly int Columns;
            public readonly int Rows;
            public readonly string ClipList;
            public readonly string Note;

            public Preset(string label, int columns, int rows, string clipList, string note = null)
            {
                Label = label;
                Columns = columns;
                Rows = rows;
                ClipList = clipList;
                Note = note;
            }

            /// <summary>Grid this preset's frame numbers assume — indices are meaningless on any other grid.</summary>
            public string Grid => $"{Columns}x{Rows}";
        }

        public static readonly Preset[] All =
        {
            new Preset("Guest", 16, 23,
                "walk_forward: 0-7 @ 10 loop\n" +
                "walk_backward: 16-23 @ 10 loop\n" +
                "idle_front_facing: 32-37 @ 10 loop\n" +
                "idle_back_facing: 48-53 @ 10 loop\n" +
                "intrigue_front: 64-66 @ 10 once\n" +
                "intrigue_back: 80-82 @ 10 once\n" +
                "intrigue_blink_front: 96-98 @ 10 once\n" +
                "sit_down_front: 112-116,115-113 @ 10 loop\n" +
                "sit_down_back: 128-132,131-129 @ 10 loop\n" +
                "stand_up_front: 144-150 @ 10 once\n" +
                "stand_up_back: 160-166 @ 10 once\n" +
                "use_front: 176-181 @ 10 once\n" +
                "use_back: 192-197 @ 10 once\n" +
                "consume_front: 208-215 @ 10 loop\n" +
                "consume_back: 224-231 @ 10 loop\n" +
                "sus_behaviour_front: 240-254 @ 10 loop\n" +
                "sus_behaviour_back: 256-270 @ 10 loop\n" +
                "dissapoint_front: 272-287 @ 10 loop\n" +
                "dissapoint_back: 288-303 @ 10 loop\n" +
                "disgusted_front: 304-315 @ 10 loop\n" +
                "disgusted_back: 320-331 @ 10 loop\n" +
                "excited_front: 336-347 @ 10 loop\n" +
                "excited_back: 352-363 @ 10 loop",
                "23 clips. sit_down was ping-pong in Godot; the descending second range replays " +
                "the middle frames backwards for the same motion."),

            new Preset("Player / Alex", 8, 4,
                "walk_forward: 0-7 @ 10 loop\n" +
                "walk_backward: 8-15 @ 10 loop\n" +
                "idle_front_facing: 16-20 @ 5 loop\n" +
                "idle_back_facing: 24-28 @ 5 loop",
                "Male Main Character (2).png and Digging_Buddy_Animation.png share this layout."),

            new Preset("Professor", 8, 4,
                "walk_forward: 0-7 @ 10 loop\n" +
                "walk_backward: 8-15 @ 10 loop\n" +
                "idle_front_facing: 16-21 @ 5 loop\n" +
                "idle_back_facing: 24-29 @ 5 loop",
                "Idles are 16-21 / 24-29 here: the sheet draws 6 idle frames but the Godot clip " +
                "only played 5. Scrub them to confirm the 6th belongs in the cycle."),

            new Preset("Emily", 10, 5,
                "walk_forward: 0-7 @ 10 loop\n" +
                "walk_backward: 10-17 @ 10 loop\n" +
                "idle_front_facing: 20-24 @ 5 loop\n" +
                "idle_back_facing: 30-34 @ 5 loop\n" +
                "row_4_unnamed: 40-49 @ 10 loop",
                "The only 10x5 sheet, so frame numbers step by 10. Row 4 is drawn but had no clip " +
                "in Godot — rename it once you know what it is."),
        };
    }
}
