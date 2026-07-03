using Systems.MineSystem.MinePlayerSystem.Model;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public interface IPlayerDamageService
    {
        bool ApplyDamage(
            float amount,
            PlayerDamageKind kind = PlayerDamageKind.Standard);
    }
}
