using UnityEngine;

public class LeyLineShrine : MonoBehaviour
{
    public bool isClaimed = false;
    public float channelDuration = 5.0f;
    public string shrineName = "Ley Line Shrine"; // For logging
    public Color claimedColor = Color.green; // Visual feedback
    private Color originalColor;
    private Renderer shrineRenderer;

    void Start()
    {
        shrineRenderer = GetComponent<Renderer>();
        if (shrineRenderer != null && shrineRenderer.material != null)
        {
            originalColor = shrineRenderer.material.color;
        }
        else
        {
            Debug.LogWarning(shrineName + " (" + gameObject.name + "): Renderer or Material not found for color change.");
        }
    }

    public void ClaimShrine()
    {
        isClaimed = true;
        if (shrineRenderer != null && shrineRenderer.material != null)
        {
            shrineRenderer.material.color = claimedColor;
        }
        Debug.Log(shrineName + " at " + transform.position + " has been claimed.");
        // Potentially disable further interaction with this shrine
        // For example, by disabling its collider or this script, though isClaimed flag handles logic.
    }
}
