using UnityEngine;

public class ClueObject : MonoBehaviour
{
    public float lifetime = 10.0f; // Default lifetime, can be overridden by HeroController

    void Start()
    {
        // Destroy the clue GameObject after 'lifetime' seconds
        Destroy(gameObject, lifetime);
    }

    // Optional: Add any visual effects or other logic for the clue here if needed later
    // For example, fading out over time.
    void Update()
    {
        // Example: Fade out effect (requires a Renderer and material that supports transparency)
        /*
        if (GetComponent<Renderer>() != null && GetComponent<Renderer>().material.HasProperty("_Color"))
        {
            float remainingLifetime = lifetime - (Time.time - creationTime); // creationTime would need to be stored in Start
            if (remainingLifetime < 0) remainingLifetime = 0;
            Color color = GetComponent<Renderer>().material.color;
            color.a = remainingLifetime / lifetime; // Assuming initial lifetime was set correctly
            GetComponent<Renderer>().material.color = color;
        }
        */
    }
}
