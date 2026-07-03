namespace Systems.MineSystem.PauseSystem.Interface
{
    public interface IPausable
    {
        bool IsAffectedByPause { get; set; }
        void OnPause();
        void OnUnpause();
    }
}
