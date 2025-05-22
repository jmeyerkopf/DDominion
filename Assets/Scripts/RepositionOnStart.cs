using UnityEngine;

public class RepositionOnStart : MonoBehaviour
{
    public string markerName; // Name of the empty GameObject marker in the scene

    void Start()
    {
        if (string.IsNullOrEmpty(markerName))
        {
            Debug.LogError(gameObject.name + ": MarkerName is not set in RepositionOnStart script.", this);
            return;
        }

        GameObject markerObject = GameObject.Find(markerName);
        if (markerObject != null)
        {
            transform.position = markerObject.transform.position;
            Debug.Log(gameObject.name + " repositioned to marker: " + markerName + " at " + transform.position, this);
        }
        else
        {
            Debug.LogError(gameObject.name + ": Could not find marker GameObject named '" + markerName + "'. Will not reposition.", this);
        }
    }
}
