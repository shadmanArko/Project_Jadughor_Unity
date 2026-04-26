using System;

namespace Systems.MineSystem.Mine.Enum
{
    [Flags]
    public enum BrokenEdges
    {
        None               = 0,
        Top                = 1 << 0,
        Right              = 1 << 1,
        Bottom             = 1 << 2,
        Left               = 1 << 3,
        TopLeftCorner      = 1 << 4,
        BottomLeftCorner   = 1 << 5,
        TopRightCorner     = 1 << 6,
        BottomRightCorner  = 1 << 7,
    }
}
