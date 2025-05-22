using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100.0f;
    private float currentHealth;

    // Gold Drop Fields
    public GameObject goldNuggetPrefab; // Assign GoldNugget prefab in Inspector (especially for Scout)
    public int goldDropAmount = 1;      // Number of gold nuggets to drop

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log(gameObject.name + " took " + amount + " damage. Current health: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " has been defeated!");

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
        
        gameObject.SetActive(false); 
        
        if (gameObject.CompareTag("Hero")) { Debug.Log("GAME OVER - Hero Defeated"); }
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
}
