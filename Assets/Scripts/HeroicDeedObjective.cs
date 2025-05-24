using UnityEngine;

public class HeroicDeedObjective : MonoBehaviour
{
    public string deedName = "Default Heroic Deed";
    public int valorReward = 25;
    public bool isCompleted = false;
    public Color completedColor = Color.gray; // Visual feedback for completion
    private Color originalColor;
    private Renderer objectiveRenderer;
    private bool colorChangePossible = false;

    void Start()
    {
        objectiveRenderer = GetComponent<Renderer>();
        if (objectiveRenderer != null && objectiveRenderer.material != null)
        {
            originalColor = objectiveRenderer.material.color;
            colorChangePossible = true;
        }
        else
        {
            Debug.LogWarning("HeroicDeedObjective ("+ deedName +"): Renderer or Material not found for color change on " + gameObject.name);
        }
    }

    public void CompleteDeed(KnightController knight)
    {
        if (isCompleted) return;

        isCompleted = true;
        // The KnightController will handle valor gain and logging.
        // knight.GainValor(valorReward); // This line will be called from KnightController after it gets the reward amount.
        
        Debug.Log("Heroic Deed '" + deedName + "' on " + gameObject.name + " has been completed by " + knight.name + ".");

        if (colorChangePossible)
        {
            objectiveRenderer.material.color = completedColor;
        }
        
        Collider col = GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = false; // Prevent re-interaction by disabling the collider
        }
        // Alternatively, disable the script:
        // this.enabled = false; 
    }
}
