using UnityEngine;

public abstract class HeroControllerBase : MonoBehaviour
{
    public bool isDefeated = false; // This will be the single source of truth for defeat status.
    public abstract void SetDefeated(); // To be implemented by each hero type
    
    // Common method to apply knockback, can be called by Dark Lord abilities or other sources
    // Each hero type will implement how knockback affects them (e.g., using CharacterController)
    public abstract void ApplyKnockback(Vector3 force, float duration);

    // Common method for Reveal effect, if applicable to all heroes
    public abstract void ApplyRevealEffect(float duration);

    // Common method for Scry effect
    public abstract void ApplyScryEffect(float duration);
}
