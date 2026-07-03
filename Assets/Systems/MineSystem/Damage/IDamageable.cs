namespace Systems.MineSystem.Damage
{
    /// <summary>Receives gameplay damage without assuming its source.</summary>
    public interface IDamageable
    {
        void ApplyDamage(float amount);
    }
}
