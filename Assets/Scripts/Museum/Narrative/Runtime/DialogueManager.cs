using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Drives the Dialogue Panel: types out a <see cref="StoryScene"/> line by line,
    /// shows the speaker portrait and (when present) the full-screen cutscene art,
    /// and chains into a tutorial or ends the scene when the last line is read.
    ///
    /// Unity port of the Godot <c>DialogueSystem</c>. HTTP fetching is dropped in
    /// favour of a <see cref="StoryDatabase"/> asset; typing uses UniTask, and the
    /// box slides with DOTween.
    ///
    /// Wire this onto the "Dialogue Panel" object and assign the references below.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private StoryDatabase storyDatabase;
        [Tooltip("Resources sub-folder holding '{Speaker} {Emotion}.png' portraits.")]
        [SerializeField] private string portraitsResourceFolder = "Portraits";
        [Tooltip("Resources sub-folder holding '{IllustrationName}.png' cutscene art.")]
        [SerializeField] private string illustrationsResourceFolder = "Illustrations";

        [Header("UI references")]
        [Tooltip("Root that is shown/hidden. Usually this same Dialogue Panel object.")]
        [SerializeField] private GameObject root;
        [Tooltip("The box that slides in/out (its RectTransform is tweened).")]
        [SerializeField] private RectTransform dialogueBox;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextDialogueButton;
        [SerializeField] private Image characterPortrait;
        [Tooltip("Optional full-screen cutscene illustration shown above the box.")]
        [SerializeField] private Image cutsceneArt;

        [Header("Typewriter delays (seconds)")]
        [SerializeField] private float delayBetweenLetters = 0.03f;
        [SerializeField] private float delayForFullStop = 0.4f;
        [SerializeField] private float delayForComma = 0.2f;
        [Tooltip("How long a [PAUSE] tag waits.")]
        [SerializeField] private float delayForPause = 0.6f;

        [Header("Slide animation")]
        [Tooltip("Offset from the authored position that counts as 'hidden'. " +
                 "e.g. (0,-400) hides the box by sliding it down off-screen.")]
        [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -600f);
        [SerializeField] private float slideDuration = 0.6f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        private StoryScene _storyScene;
        private int _storyEntryCount;
        private int _currentStorySceneNumber;

        private CancellationTokenSource _typingCts;
        private bool _finishedCurrentDialogue;
        private bool _isTyping;

        private string _playerName = "Player";
        private Vector2 _shownPos;   // authored position = fully visible
        private Vector2 _hiddenPos;  // shownPos + hiddenOffset

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (dialogueBox != null)
            {
                _shownPos = dialogueBox.anchoredPosition;
                _hiddenPos = _shownPos + hiddenOffset;
                dialogueBox.anchoredPosition = _hiddenPos;
            }
            root.SetActive(false);
        }

        private void OnEnable()
        {
            MuseumActions.PlayStoryScene += LoadStoryScene;
            MuseumActions.OnPlayerInfoUpdated += OnPlayerInfoUpdated;
            if (nextDialogueButton != null)
                nextDialogueButton.onClick.AddListener(OnNextDialoguePressed);
        }

        private void OnDisable()
        {
            MuseumActions.PlayStoryScene -= LoadStoryScene;
            MuseumActions.OnPlayerInfoUpdated -= OnPlayerInfoUpdated;
            if (nextDialogueButton != null)
                nextDialogueButton.onClick.RemoveListener(OnNextDialoguePressed);
            CancelTyping();
        }

        private void Start()
        {
            _playerName = PlayerInfoProvider.Current?.Name ?? "Player";
        }

        private void OnPlayerInfoUpdated(PlayerInfo info)
        {
            if (info != null) _playerName = info.Name;
        }

        // ── Scene loading ───────────────────────────────────────────────

        private void LoadStoryScene(int storySceneNumber)
        {
            if (storyDatabase == null)
            {
                Debug.LogError("[DialogueManager] StoryDatabase not assigned.", this);
                return;
            }

            _currentStorySceneNumber = storySceneNumber;
            _storyScene = storyDatabase.GetByScene(storySceneNumber);
            if (_storyScene == null || _storyScene.StorySceneEntries == null ||
                _storyScene.StorySceneEntries.Count == 0)
            {
                Debug.LogWarning($"[DialogueManager] Story scene {storySceneNumber} is empty.");
                return;
            }

            _storyEntryCount = 0;
            root.SetActive(true);
            SlideIn();
            ShowCurrentEntry();
        }

        private void ShowCurrentEntry()
        {
            root.SetActive(true);
            LoadAndSetPortrait();
            LoadAndSetCutsceneArt();

            StorySceneEntry entry = _storyScene.StorySceneEntries[_storyEntryCount];
            MuseumActions.StorySceneEntryStarted?.Invoke(entry.EntryNo);

            CancelTyping();
            _typingCts = new CancellationTokenSource();
            TypeDialogue(_storyEntryCount, _typingCts.Token).Forget();
        }

        // ── Next button ─────────────────────────────────────────────────

        private void OnNextDialoguePressed()
        {
            // First press while still typing: finish the line instantly, stay put.
            if (_isTyping)
            {
                CancelTyping();
                CompleteDialogueInstantly(_storyEntryCount);
                return;
            }

            StorySceneEntry entry = _storyScene.StorySceneEntries[_storyEntryCount];
            MuseumActions.StorySceneEntryEnded?.Invoke(entry.EntryNo);

            if (_storyEntryCount < _storyScene.StorySceneEntries.Count - 1)
            {
                _storyEntryCount++;
                ShowCurrentEntry();
            }
            else
            {
                HandleSceneEnd();
            }
        }

        // ── Scene end ───────────────────────────────────────────────────

        private async void HandleSceneEnd()
        {
            // Bookkeeping that used to hit the player HTTP API.
            PlayerInfo info = PlayerInfoProvider.Current;
            if (info != null)
            {
                info.CompletedStoryScene = _currentStorySceneNumber;
                MuseumActions.OnPlayerInfoUpdated?.Invoke(info);
            }

            await SlideOut();

            if (cutsceneArt != null) cutsceneArt.gameObject.SetActive(false);
            root.SetActive(false);

            if (_storyScene.HasTutorial)
                MuseumActions.PlayTutorial?.Invoke(_storyScene.TutorialNumber);
            else
                MuseumActions.StorySceneEnded?.Invoke(_currentStorySceneNumber);
        }

        // ── Portrait / cutscene art ─────────────────────────────────────

        private void LoadAndSetPortrait()
        {
            if (characterPortrait == null) return;
            StorySceneEntry entry = _storyScene.StorySceneEntries[_storyEntryCount];
            string fileName = $"{entry.Speaker} {entry.SpeakerEmotion}";
            Sprite sprite = LoadSprite($"{portraitsResourceFolder}/{fileName}");
            characterPortrait.sprite = sprite;
            characterPortrait.enabled = sprite != null;
        }

        private void LoadAndSetCutsceneArt()
        {
            if (cutsceneArt == null) return;
            StorySceneEntry entry = _storyScene.StorySceneEntries[_storyEntryCount];

            if (!entry.HasCutscene)
            {
                cutsceneArt.gameObject.SetActive(false);
                return;
            }

            cutsceneArt.gameObject.SetActive(true);

            if (!entry.HasCutsceneArt || string.IsNullOrEmpty(entry.IllustrationName))
            {
                cutsceneArt.sprite = null;
                cutsceneArt.enabled = false;
                return;
            }

            Sprite sprite = LoadSprite($"{illustrationsResourceFolder}/{entry.IllustrationName}");
            cutsceneArt.sprite = sprite;
            cutsceneArt.enabled = sprite != null;
        }

        /// <summary>
        /// Loads a sprite from Resources, tolerating both "Single" and "Multiple"
        /// sprite-mode textures (Portraits are Single, Illustrations are Multiple).
        /// </summary>
        private static Sprite LoadSprite(string resourcePath)
        {
            Sprite direct = Resources.Load<Sprite>(resourcePath);
            if (direct != null) return direct;

            Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
            if (all != null && all.Length > 0) return all[0];

            Debug.LogWarning($"[DialogueManager] Sprite not found in Resources: {resourcePath}");
            return null;
        }

        // ── Typewriter ──────────────────────────────────────────────────

        private async UniTaskVoid TypeDialogue(int entry, CancellationToken ct)
        {
            _isTyping = true;
            _finishedCurrentDialogue = false;
            SetNextButtonInteractable(true);

            string dialogue = _storyScene.StorySceneEntries[entry].Dialogue ?? string.Empty;
            dialogueText.text = string.Empty;

            bool skip = false;
            string tag = string.Empty;

            try
            {
                foreach (char letter in dialogue)
                {
                    float delay = delayBetweenLetters;

                    if (letter == ',') delay = delayForComma;
                    else if (letter == '.' || letter == '?') delay = delayForFullStop;
                    else if (letter == '[') { skip = true; tag = string.Empty; continue; }
                    else if (letter == ']')
                    {
                        skip = false;
                        if (tag == "PAUSE")
                            await UniTask.Delay((int)(delayForPause * 1000), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                        else if (tag == "PLAYERNAME")
                            dialogueText.text += _playerName;
                        continue;
                    }

                    if (skip) { tag += letter; continue; }

                    dialogueText.text += letter;
                    await UniTask.Delay((int)(delay * 1000), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                }

                _finishedCurrentDialogue = true;
            }
            catch (System.OperationCanceledException)
            {
                // Skipped by the Next button — OnNextDialoguePressed fills the rest in.
            }
            finally
            {
                _isTyping = false;
            }
        }

        /// <summary>Render the full line at once (used when the player skips typing).</summary>
        private void CompleteDialogueInstantly(int entry)
        {
            string dialogue = _storyScene.StorySceneEntries[entry].Dialogue ?? string.Empty;
            var sb = new System.Text.StringBuilder();

            bool skip = false;
            string tag = string.Empty;
            foreach (char letter in dialogue)
            {
                if (letter == '[') { skip = true; tag = string.Empty; continue; }
                if (letter == ']')
                {
                    skip = false;
                    if (tag == "PLAYERNAME") sb.Append(_playerName);
                    continue;
                }
                if (skip) { tag += letter; continue; }
                sb.Append(letter);
            }

            dialogueText.text = sb.ToString();
            _finishedCurrentDialogue = true;
            _isTyping = false;
        }

        private void CancelTyping()
        {
            if (_typingCts == null) return;
            _typingCts.Cancel();
            _typingCts.Dispose();
            _typingCts = null;
        }

        // ── Slide ───────────────────────────────────────────────────────

        private void SlideIn()
        {
            if (dialogueBox == null) { SetNextButtonInteractable(true); return; }
            SetNextButtonInteractable(false);
            dialogueBox.DOKill();
            dialogueBox.DOAnchorPos(_shownPos, slideDuration)
                       .SetEase(slideEase)
                       .SetUpdate(true) // run on unscaled time (works while paused)
                       .OnComplete(() => SetNextButtonInteractable(true));
        }

        private UniTask SlideOut()
        {
            if (dialogueBox == null) return UniTask.CompletedTask;
            SetNextButtonInteractable(false);
            dialogueBox.DOKill();

            // Drive a completion source from OnComplete so we don't depend on the
            // optional UniTask⇄DOTween integration define (UNITASK_DOTWEEN_SUPPORT).
            var tcs = new UniTaskCompletionSource();
            dialogueBox.DOAnchorPos(_hiddenPos, slideDuration)
                       .SetEase(slideEase)
                       .SetUpdate(true)
                       .OnComplete(() => tcs.TrySetResult());
            return tcs.Task;
        }

        private void SetNextButtonInteractable(bool value)
        {
            if (nextDialogueButton != null) nextDialogueButton.interactable = value;
        }
    }
}
