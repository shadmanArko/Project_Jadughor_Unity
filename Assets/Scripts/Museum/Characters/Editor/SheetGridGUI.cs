using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Shared drawing for the sheet grid picker and the magnified frame preview, used by both
    /// the renderer and the group inspectors.
    /// </summary>
    internal static class SheetGridGUI
    {
        static readonly Color GridLine = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color Highlight = new Color(0.35f, 0.85f, 1f, 1f);
        static readonly Color HighlightFill = new Color(0.35f, 0.85f, 1f, 0.15f);
        static readonly Color Backdrop = new Color(0.16f, 0.16f, 0.16f, 1f);

        /// <summary>UV rect of one grid cell. Frame 0 is top-left; GUI UVs start bottom-left.</summary>
        public static Rect CellUv(int columns, int rows, int frame)
        {
            frame = Mathf.Clamp(frame, 0, columns * rows - 1);
            var column = frame % columns;
            var row = frame / columns;

            return new Rect(
                column / (float)columns,
                1f - (row + 1) / (float)rows,
                1f / columns,
                1f / rows);
        }

        /// <summary>Magnified view of the active frame, so you can see the pose you selected.</summary>
        public static void DrawFramePreview(Texture2D sheet, int columns, int rows, int frame, float height = 96f)
        {
            var cellAspect = (sheet.width / (float)columns) / Mathf.Max(1f, sheet.height / (float)rows);
            var rect = GUILayoutUtility.GetRect(height * cellAspect, height, GUILayout.ExpandWidth(false));

            EditorGUI.DrawRect(rect, Backdrop);
            GUI.DrawTextureWithTexCoords(rect, sheet, CellUv(columns, rows, frame), true);
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Highlight * 0.6f);
        }

        /// <summary>
        /// Draws the whole sheet with grid lines and the active cell highlighted. Returns the
        /// frame index, changed if the user clicked a different cell.
        /// </summary>
        public static int DrawSheetGrid(Texture2D sheet, int columns, int rows, int frame)
        {
            var available = EditorGUIUtility.currentViewWidth - 40f;
            var width = Mathf.Min(available, sheet.width * 3f);
            var height = width * (sheet.height / (float)sheet.width);

            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            rect.width = width;
            rect.height = height;

            EditorGUI.DrawRect(rect, Backdrop);
            GUI.DrawTexture(rect, sheet, ScaleMode.StretchToFill, true);

            var cellWidth = rect.width / columns;
            var cellHeight = rect.height / rows;

            for (var c = 1; c < columns; c++)
                EditorGUI.DrawRect(new Rect(rect.x + c * cellWidth, rect.y, 1f, rect.height), GridLine);
            for (var r = 1; r < rows; r++)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + r * cellHeight, rect.width, 1f), GridLine);

            var clamped = Mathf.Clamp(frame, 0, columns * rows - 1);
            var cellRect = new Rect(
                rect.x + clamped % columns * cellWidth,
                rect.y + clamped / columns * cellHeight,
                cellWidth, cellHeight);
            Handles.DrawSolidRectangleWithOutline(cellRect, HighlightFill, Highlight);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                var column = Mathf.Clamp((int)((evt.mousePosition.x - rect.x) / cellWidth), 0, columns - 1);
                var row = Mathf.Clamp((int)((evt.mousePosition.y - rect.y) / cellHeight), 0, rows - 1);
                evt.Use();
                return row * columns + column;
            }

            return frame;
        }
    }
}
