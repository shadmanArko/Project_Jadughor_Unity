using System;
using Systems.MineSystem.ToolbarSystem.Enum;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [Serializable]
    public sealed class DirectionalAnimationSet
    {
        public string up;
        public string down;
        public string left;
        public string right;

        public string Get(CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.Up => up,
                CardinalDirection.Down => down,
                CardinalDirection.Left => left,
                CardinalDirection.Right => right,
                _ => string.Empty
            };
        }
    }
}
