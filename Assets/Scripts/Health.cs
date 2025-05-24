using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100.0f;
    private float currentHealth;

    // Gold Drop Fields
    public GameObject goldNuggetPrefab; // Assign GoldNugget prefab in Inspector (especially for Scout)
    public int goldDropAmount = 1;      // Number of gold nuggets to drop

    private float currentDamageReduction = 0f;
    private HeroControllerBase heroBase; // Reference to the hero base script

    void Start()
    {
        currentHealth = maxHealth;
        heroBase = GetComponent<HeroControllerBase>(); // Get the component
    }

    public void SetDamageReduction(float reductionPercent)
    {
        currentDamageReduction = Mathf.Clamp01(reductionPercent);
        Debug.Log(gameObject.name + " damage reduction set to " + (currentDamageReduction * 100) + "%");
    }

    public bool TakeDamage(float amount) // Changed to return bool
    {
        float actualDamage = amount * (1.0f - currentDamageReduction);
        currentHealth -= actualDamage;
        Debug.Log(gameObject.name + " took " + actualDamage + " (raw: " + amount + ") damage. Reduction: " + (currentDamageReduction * 100) + "%. Current health: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die(); // Call Die, which now also handles SetDefeated
            return true; // Damage was lethal
        }
        return false; // Damage was not lethal
    }

    void Die()
    {
        Debug.Log(gameObject.name + " has been defeated!");

        // If this Health component is on a hero, tell its controller it's defeated
        if (heroBase != null && !heroBase.isDefeated)
        {
            heroBase.SetDefeated();
        }

        // Gold drop logic specifically for "Scout"
        if (gameObject.CompareTag("Scout"))
        {
            if (goldNuggetPrefab != null)
            {
                for (int i = 0; i < goldDropAmount; i++)
                {
                    // Instantiate slightly above the scout's position with a small random offset
                    Vector3 dropPosition = transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.1f;
                    Instantiate(goldNuggetPrefab, dropPosition, Quaternion.identity);
                }
                Debug.Log(gameObject.name + " dropped " + goldDropAmount + " gold nugget(s).");
            }
            else
            {
                Debug.LogWarning("Health.Die: goldNuggetPrefab is not assigned on " + gameObject.name);
            }
        }
        
        gameObject.SetActive(false); // Deactivate the GameObject
        
        // The "GAME OVER - Hero Defeated" log is now effectively handled by GameManager
        // if (gameObject.CompareTag("Hero")) { Debug.Log("GAME OVER - Hero Defeated"); } 
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        Debug.Log(gameObject.name + " healed for " + amount + ". Current health: " + currentHealth);
    }

    public void IncreaseMaxHealth(float amount)
    {
        if (amount <= 0) return; // Do nothing if amount is not positive

        maxHealth += amount;
        // currentHealth += amount; // Optional: Also increase current health by the same amount
                                 // The KnightController will call Heal separately as per plan.
        Debug.Log(gameObject.name + " max health increased by " + amount + ". New max health: " + maxHealth);
    }
}
