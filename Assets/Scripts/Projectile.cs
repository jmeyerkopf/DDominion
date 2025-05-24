using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f; // Time before self-destructing if no hit
    public float damagePayload = 10f;
    public string targetTag = "Minion"; // General tag, specific tags are checked in OnTriggerEnter

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the collided object has one of the target tags
        // For flexibility, we check specific minion tags directly.
        // The 'targetTag' field could be used for other types of targets if needed.
        if (other.CompareTag("Scout") || other.CompareTag("Tank") || other.CompareTag("Priest"))
        {
            Health healthComponent = other.GetComponent<Health>();
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(damagePayload);
                Debug.Log(gameObject.name + " hit " + other.name + " for " + damagePayload + " damage.");
            }
            Destroy(gameObject); // Destroy projectile on hit
        }
        // Optional: Could also destroy on collision with environment if needed
        // else if (other.gameObject.layer == LayerMask.NameToLayer("Environment")) // Assuming an Environment layer
        // {
        //     Debug.Log(gameObject.name + " hit environment object " + other.name);
        //     Destroy(gameObject);
        // }
    }
}
