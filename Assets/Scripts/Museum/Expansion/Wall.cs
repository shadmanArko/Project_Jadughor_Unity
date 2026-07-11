using UnityEngine;

/// <summary>
/// A single wall segment. Holds the three sprite variants for its edge (the
/// start piece, the repeated middle piece, and the end/corner piece) and shows
/// the right one depending on where it sits along the wall line.
///
/// One prefab per edge type (back-left, back-right, front-left, front-right);
/// the <see cref="ExpansionManager"/> spawns a row of these and calls
/// <see cref="SetPiece"/> on each.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Wall : MonoBehaviour
{
    public enum Piece { First, Middle, Corner }

    [SerializeField] private Sprite firstSprite;
    [SerializeField] private Sprite middleSprite;
    [SerializeField] private Sprite cornerSprite;

    private SpriteRenderer _sr;

    /// <summary>Show the sprite for this segment's role in the line.</summary>
    public void SetPiece(Piece piece)
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        Sprite s = piece switch
        {
            Piece.First  => firstSprite,
            Piece.Corner => cornerSprite,
            _            => middleSprite,
        };
        if (s != null) _sr.sprite = s;
    }
}
