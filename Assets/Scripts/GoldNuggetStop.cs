using UnityEngine;

public class GoldNuggetStop : MonoBehaviour
{
    public float stopDelay = 2.0f; // Time in seconds before stopping
    public float groundCollisionStopDelay = 0.5f; // Shorter delay if it hits the ground

    private Rigidbody rb;
    private bool hasCollidedWithGround = false;
    private float timer;
    private bool stoppingScheduled = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        timer = stopDelay; // Initialize timer with the longer delay
    }

    void Update()
    {
        if (rb == null || rb.IsSleeping() || (rb.isKinematic && rb.velocity.magnitude < 0.01f))
        {
            // Already stopped or no Rigidbody, no need to do anything.
            return;
        }

        if (stoppingScheduled)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                StopMovement();
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if it collided with something considered "ground"
        // For simplicity, any collision will trigger the shorter stop timer
        // A more robust solution would check tags or layers (e.g., if (collision.gameObject.CompareTag("Ground")))
        if (!hasCollidedWithGround && !stoppingScheduled)
        {
            hasCollidedWithGround = true;
            ScheduleStop(groundCollisionStopDelay);
            // Debug.Log(gameObject.name + " collided with ground, scheduling stop in " + groundCollisionStopDelay + "s");
        }
        else if (!stoppingScheduled) // If it didn't hit ground first, use the general stop delay
        {
             ScheduleStop(stopDelay);
             // Debug.Log(gameObject.name + " collided, scheduling stop in " + stopDelay + "s");
        }
    }
    
    void ScheduleStop(float delay)
    {
        if (!stoppingScheduled) 
        {
            timer = delay;
            stoppingScheduled = true;
        }
    }

    void StopMovement()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true; // Make it kinematic to stop it completely
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Debug.Log(gameObject.name + " movement stopped.");
            // Optionally, disable this script after stopping
            // this.enabled = false; 
        }
    }
}
