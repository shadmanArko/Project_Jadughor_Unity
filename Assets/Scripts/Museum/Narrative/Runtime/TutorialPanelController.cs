using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// The on-screen tutorial hint panel. Slides in with the current step's text
    /// and slides out when the tutorial ends. Listens to
    /// <see cref="MuseumActions.OnTutorialUpdated"/> / <see cref="MuseumActions.OnTutorialEnded"/>.
    ///
    /// Unity port of the Godot <c>TutorialController</c> (AnimationPlayer slides
    /// become DOTween anchored-position tweens). Put this on the "TutorialPanel"
    /// object and assign its body text + the RectTransform to slide.
    /// </summary>
    public class TutorialPanelController : MonoBehaviour
    {
        [Header("UI references")]
        [SerializeField] private GameObject root;
        [Tooltip("The panel that slides in/out (its RectTransform is tweened).")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text tutorialBody;

        [Header("Slide animation")]
        [Tooltip("Offset from the authored position that counts as 'hidden'. " +
                 "e.g. (-600,0) hides the panel by sliding it off the left edge.")]
        [SerializeField] private Vector2 hiddenOffset = new Vector2(-700f, 0f);
        [SerializeField] private float slideDuration = 0.5f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;
        [Tooltip("Delay before a new step's hint slides in (matches the Godot 1s wait).")]
        [SerializeField] private float showDelay = 0.6f;

        private Vector2 _shownPos;
        private Vector2 _hiddenPos;
        private CancellationTokenSource _showCts;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (panel != null)
            {
                _shownPos = panel.anchoredPosition;
                _hiddenPos = _shownPos + hiddenOffset;
                panel.anchoredPosition = _hiddenPos;
            }
            root.SetActive(false);
        }

        private void OnEnable()
        {
            MuseumActions.OnTutorialUpdated += OnTutorialUpdated;
            MuseumActions.OnTutorialEnded += OnTutorialEnded;
        }

        private void OnDisable()
        {
            MuseumActions.OnTutorialUpdated -= OnTutorialUpdated;
            MuseumActions.OnTutorialEnded -= OnTutorialEnded;
            CancelPending();
        }

        private void OnTutorialUpdated(string text)
        {
            CancelPending();
            _showCts = new CancellationTokenSource();
            ShowStep(text, _showCts.Token).Forget();
        }

        private async UniTaskVoid ShowStep(string text, CancellationToken ct)
        {
            try
            {
                if (showDelay > 0f)
                    await UniTask.Delay((int)(showDelay * 1000), DelayType.UnscaledDeltaTime,
                                        PlayerLoopTiming.Update, ct);

                root.SetActive(true);
                if (tutorialBody != null) tutorialBody.text = text;

                if (panel != null)
                {
                    panel.DOKill();
                    panel.DOAnchorPos(_shownPos, slideDuration).SetEase(slideEase).SetUpdate(true);
                }
            }
            catch (System.OperationCanceledException) { /* superseded by a newer step */ }
        }

        private void OnTutorialEnded()
        {
            CancelPending();
            if (panel == null) { root.SetActive(false); return; }

            panel.DOKill();
            panel.DOAnchorPos(_hiddenPos, slideDuration)
                 .SetEase(slideEase)
                 .SetUpdate(true)
                 .OnComplete(() => root.SetActive(false));
        }

        private void CancelPending()
        {
            if (_showCts == null) return;
            _showCts.Cancel();
            _showCts.Dispose();
            _showCts = null;
        }
    }
}
