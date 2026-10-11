namespace Systems.MineSystem.Mine.Signal
{
    public readonly struct CaveRevealedSignal
    {
        public readonly string CaveId;

        public CaveRevealedSignal(string caveId)
        {
            CaveId = caveId;
        }
    }
}
