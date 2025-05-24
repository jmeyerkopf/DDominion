public enum PotionEffectType
{
    HealSelf,       // Restores health
    SpeedBoost,     // Increases movement speed temporarily
    DamageBuff,     // Increases attack damage temporarily
    MinorExplosion, // A small damaging explosion (failed potion outcome)
    SmokeCloud,     // Creates a temporary smoke cloud (failed/utility)
    NullEffect      // No actual effect (failed potion outcome)
}
