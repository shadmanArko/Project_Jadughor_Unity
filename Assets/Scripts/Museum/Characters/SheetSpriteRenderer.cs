using UnityEngine;

namespace ProjectMuseum.Characters
{
    /// <summary>
    /// Unity equivalent of Godot's <c>Sprite2D</c> grid animation: assign one sheet texture,
    /// tell it how many columns/rows it has, and drive the integer <see cref="Frame"/> from
    /// an <see cref="AnimationClip"/>. Frame indices are row-major (frame 0 = top-left), so
    /// the numbers used in the Godot scenes port over as-is.
    ///
    /// Runs with <c>[ExecuteAlways]</c> so scrubbing a clip in the Animation window shows the
    /// real sub-image in the Scene view — the whole point being that you can eyeball an
    /// animation while authoring it instead of entering play mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Project Museum/Sheet Sprite Renderer")]
    public class SheetSpriteRenderer : MonoBehaviour
    {
        [Header("Sheet")]
        [Tooltip("Source sheet. Any import mode works — the grid is computed from pixel size, " +
                 "sub-sprite slices are ignored.")]
        [SerializeField] Texture2D _sheet;

        [Tooltip("Columns in the sheet (Godot: hframes).")]
        [Min(1)] [SerializeField] int _hFrames = 8;

        [Tooltip("Rows in the sheet (Godot: vframes).")]
        [Min(1)] [SerializeField] int _vFrames = 4;

        [Header("Frame")]
        [Tooltip("Animate this. Row-major index into the grid, clamped to the sheet. " +
                 "Key it with Constant/stepped tangents so frames snap instead of blending.")]
        public int Frame;

        [Tooltip("Added to Frame before display. Lets one clip drive several layers that sit " +
                 "at different offsets inside their own sheets.")]
        [SerializeField] int _frameOffset;

        [Header("Placement")]
        [Tooltip("Pivot inside each cell, normalized. (0.5, 0) = bottom-centre, the usual " +
                 "choice for isometric characters so feet sit on the ground.")]
        [SerializeField] Vector2 _pivot = new Vector2(0.5f, 0f);

        [Tooltip("Must match the sheet's own Pixels Per Unit or the sprite renders at the wrong scale.")]
        [Min(0.0001f)] [SerializeField] float _pixelsPerUnit = 32f;

        [Tooltip("Mirrors horizontally (Godot used Sprite2D.scale.x = -1 for this).")]
        [SerializeField] bool _flipX;

        SpriteRenderer _renderer;
        Sprite[] _frames;
        int _appliedFrame = int.MinValue;
        bool _appliedFlipX;

        /// <summary>Total cells in the grid. Valid <see cref="Frame"/> values are 0..FrameCount-1.</summary>
        public int FrameCount => _hFrames * _vFrames;

        public int Columns => _hFrames;
        public int Rows => _vFrames;

        public SpriteRenderer Renderer
        {
            get
            {
                if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
                return _renderer;
            }
        }

        /// <summary>
        /// Swap the sheet at runtime. This is how guest customisation works: every hair /
        /// skin / cloth variant shares one grid layout, so only the texture changes and the
        /// frame indices keep meaning the same pose.
        /// </summary>
        public Texture2D Sheet
        {
            get => _sheet;
            set
            {
                if (_sheet == value) return;
                _sheet = value;
                Invalidate();
            }
        }

        public bool FlipX
        {
            get => _flipX;
            set
            {
                _flipX = value;
                Renderer.flipX = value;
                _appliedFlipX = value;
            }
        }

        /// <summary>Sets the frame and pushes it to the renderer immediately.</summary>
        public void SetFrame(int frame)
        {
            Frame = frame;
            Apply();
        }

        /// <summary>Drops the cached slice list; call after changing grid or pivot in code.</summary>
        public void Invalidate()
        {
            _frames = null;
            _appliedFrame = int.MinValue;
            Apply();
        }

        void OnEnable()
        {
            _appliedFrame = int.MinValue;
            Apply();
        }

        void OnValidate()
        {
            _frames = null;
            _appliedFrame = int.MinValue;
            Apply();
        }

        // The Animator writes public fields directly — no property setter runs — so the
        // frame is pushed to the SpriteRenderer here, after animation has evaluated.
        void LateUpdate() => Apply();

        /// <summary>
        /// Pushes <see cref="Frame"/> to the <see cref="SpriteRenderer"/>. Cheap to call every
        /// frame: it early-outs unless the index actually changed, so a held pose costs one
        /// int compare and never touches the renderer.
        /// </summary>
        public void Apply(bool force = false)
        {
            var target = Frame + _frameOffset;

            if (_flipX != _appliedFlipX)
            {
                Renderer.flipX = _flipX;
                _appliedFlipX = _flipX;
            }

            if (!force && target == _appliedFrame && _frames != null) return;

            if (_sheet == null)
            {
                Renderer.sprite = null;
                _appliedFrame = int.MinValue;
                return;
            }

            if (_frames == null)
                _frames = SheetFrameCache.Get(_sheet, _hFrames, _vFrames, _pivot, _pixelsPerUnit);

            if (_frames == null || _frames.Length == 0) return;

            // Clamp rather than wrap: an out-of-range key is a mistake worth seeing as a
            // stuck pose, not silently disguised as a different valid frame.
            var index = Mathf.Clamp(target, 0, _frames.Length - 1);
            Renderer.sprite = _frames[index];
            _appliedFrame = target;
        }

        /// <summary>Grid position of a frame index, for editor tooling and debugging.</summary>
        public Vector2Int FrameToCell(int frame)
        {
            if (_hFrames < 1) return Vector2Int.zero;
            return new Vector2Int(frame % _hFrames, frame / _hFrames);
        }

        public int CellToFrame(int column, int row) => row * _hFrames + column;
    }
}
