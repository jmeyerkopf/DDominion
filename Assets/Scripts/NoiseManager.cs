using UnityEngine;

public static class NoiseManager
{
    public static Vector3 lastNoisePosition;
    public static float lastNoiseRadius;
    public static float lastNoiseTime;
    public static float noiseDuration = 2.0f; // How long noise info persists

    public static void MakeNoise(Vector3 position, float radius)
    {
        lastNoisePosition = position;
        lastNoiseRadius = radius;
        lastNoiseTime = Time.time;
        Debug.Log("NoiseManager: Noise made at " + position + " with radius " + radius + " at time " + lastNoiseTime);
    }

    public static bool GetLatestNoise(out Vector3 position, out float radius)
    {
        if (Time.time < lastNoiseTime + noiseDuration)
        {
            position = lastNoisePosition;
            radius = lastNoiseRadius;
            // Debug.Log("NoiseManager: Getting noise. Position: " + position + ", Radius: " + radius + ", Time Left: " + (lastNoiseTime + noiseDuration - Time.time));
            return true;
        }
        else
        {
            position = Vector3.zero; 
            radius = 0f;
            return false;
        }
    }
}
