using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Guarantees the Scene view shows the correct sub-image while scrubbing or previewing a
    /// clip in the Animation window.
    ///
    /// <c>[ExecuteAlways]</c> already gives edit-mode LateUpdate calls, but those are tied to
    /// editor repaints — during animation preview the Animator can write a new frame value
    /// without a LateUpdate landing after it, which leaves the visible sprite one scrub step
    /// stale. This pushes the value through on every editor tick.
    ///
    /// Scoped to animation-preview mode and to the selected hierarchy (which is what the
    /// Animation window is driving), so it costs nothing during normal editing.
    /// </summary>
    [InitializeOnLoad]
    static class AnimationPreviewFrameTicker
    {
        static AnimationPreviewFrameTicker()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (Application.isPlaying) return;
            if (!AnimationMode.InAnimationMode()) return;

            var selected = Selection.activeGameObject;
            if (selected == null) return;

            var root = selected.transform.root.gameObject;

            foreach (var group in root.GetComponentsInChildren<SheetSpriteGroup>(true))
                group.Apply(true);

            foreach (var renderer in root.GetComponentsInChildren<SheetSpriteRenderer>(true))
                renderer.Apply(true);
        }
    }
}
