using System;
using UnityEngine;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"{X},{Y}";
        public Vector3Int ToVector3Int() => new(X, Y, 0);

        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);
        public static bool operator ==(GridPosition a, Vector3Int b) => a.X == b.x && a.Y == b.y;
        public static bool operator !=(GridPosition a, Vector3Int b) => !(a == b);
    }
}
