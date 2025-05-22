using UnityEngine;

public static class MinionAlertManager
{
    public static Vector3 lastAlertPosition;
    public static float lastAlertTime;
    public static float alertDuration = 5.0f; // How long alert info persists
    public static float alertRadius = 10.0f;  // How far the alert reaches other minions

    public static void RaiseAlert(Vector3 position)
    {
        lastAlertPosition = position;
        lastAlertTime = Time.time;
        Debug.Log("MinionAlertManager: Alert raised at " + position + " with radius " + alertRadius + " at time " + lastAlertTime);
    }

    public static bool GetActiveAlert(out Vector3 position)
    {
        if (Time.time < lastAlertTime + alertDuration)
        {
            position = lastAlertPosition;
            // Debug.Log("MinionAlertManager: Getting active alert. Position: " + position + ", Time Left: " + (lastAlertTime + alertDuration - Time.time));
            return true;
        }
        else
        {
            position = Vector3.zero;
            return false;
        }
    }
}
