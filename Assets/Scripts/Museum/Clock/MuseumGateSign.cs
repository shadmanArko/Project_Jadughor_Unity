using System.Collections;
using ProjectMuseum.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Clock
{
    /// <summary>
    /// The OPEN / CLOSE sign next to the clock — port of Godot's <c>GateOpenCloseSign</c> plus
    /// the gate CheckButton in <c>MuseumUi</c>. Clicking flips the museum gate and plays the
    /// spin: 6 frames at 0.1 s from the matching row of "sign board spin.png".
    ///
    /// Raises <see cref="MuseumActions.OnClickMuseumGateToggle"/>; GuestController listens and
    /// stops guests entering while closed. Unlike Godot, the starting state is announced in
    /// Start so listeners agree with the sign from the first frame.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MuseumGateSign : MonoBehaviour
    {
        [SerializeField] private bool startOpen = true;
        [Tooltip("OPEN → CLOSE spin (frame 0 = OPEN, last = CLOSE).")]
        [SerializeField] private Sprite[] closingFrames;
        [Tooltip("CLOSE → OPEN spin (frame 0 = CLOSE, last = OPEN).")]
        [SerializeField] private Sprite[] openingFrames;
        [SerializeField] private float frameDuration = 0.1f;

        private Image _image;
        private bool _open;
        private Coroutine _spin;

        public bool IsOpen => _open;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _open = startOpen;
            ShowRestingFrame();
        }

        private void Start()
        {
            MuseumActions.OnClickMuseumGateToggle?.Invoke(_open);
        }

        /// <summary>Hooked to the sign's Button.</summary>
        public void Toggle()
        {
            _open = !_open;
            MuseumActions.OnClickMuseumGateToggle?.Invoke(_open);

            if (_spin != null) StopCoroutine(_spin);
            _spin = StartCoroutine(Spin(_open ? openingFrames : closingFrames));
        }

        private IEnumerator Spin(Sprite[] frames)
        {
            // Godot skips frame 0 (it is the resting frame already on screen).
            for (int i = 1; i < frames.Length; i++)
            {
                if (frames[i] != null) _image.sprite = frames[i];
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            _spin = null;
        }

        private void ShowRestingFrame()
        {
            // Resting OPEN = first closing frame; resting CLOSE = first opening frame.
            Sprite[] frames = _open ? closingFrames : openingFrames;
            if (frames != null && frames.Length > 0 && frames[0] != null) _image.sprite = frames[0];
        }
    }
}
