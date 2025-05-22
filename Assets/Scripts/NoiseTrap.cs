using UnityEngine;

public class NoiseTrap : MonoBehaviour
{
    public float noiseRadius = 10.0f; // Radius of noise generated
    public bool triggered = false;
    // Optional: public GameObject visualCue; // e.g., a particle effect or sprite to show when triggered

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return; // Only trigger once

        if (other.CompareTag("Hero"))
        {
            triggered = true;
            Debug.Log("Noise Trap triggered by Hero at: " + transform.position);
            NoiseManager.MakeNoise(transform.position, noiseRadius);

            // Optional: Activate a visual cue
            // if (visualCue != null) visualCue.SetActive(true);

            // For now, let's just disable the visual part of the trap,
            // but keep the trigger object active for a short while if needed, or destroy.
            // Assuming "NoiseTrapVisuals" is the child with the renderer.
            Transform visualChild = transform.Find("NoiseTrapVisuals");
            if (visualChild != null)
            {
                visualChild.gameObject.SetActive(false);
            }
            
            // Disable the collider so it can't be triggered again.
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            // Optional: Destroy after some time
            // Destroy(gameObject, 2f); 
        }
    }
}
