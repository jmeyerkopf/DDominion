using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapLayoutManager : MonoBehaviour
{
    // These should ALL be assigned to Empty GameObjects in the scene that act as positional markers.
    public Transform heroSpawnPointMarker; 
    public Transform hiddenTavernMarker;   
    public Transform village1Marker;       
    public Transform village2Marker;       
    public Transform noiseTrapLocationMarker; 

    public GameObject noiseTrapPrefab; 
    public List<Transform> possibleSpawnLocations = new List<Transform>();

    void Awake()
    {
        List<Transform> markersToPosition = new List<Transform>();
        
        if (heroSpawnPointMarker != null) markersToPosition.Add(heroSpawnPointMarker);
        else Debug.LogError("MapLayoutManager: HeroSpawnPointMarker is not assigned!");

        if (hiddenTavernMarker != null) markersToPosition.Add(hiddenTavernMarker);
        else Debug.LogError("MapLayoutManager: HiddenTavernMarker is not assigned!");

        if (village1Marker != null) markersToPosition.Add(village1Marker);
        else Debug.LogError("MapLayoutManager: Village1Marker is not assigned!");
        
        if (village2Marker != null) markersToPosition.Add(village2Marker);
        else Debug.LogError("MapLayoutManager: Village2Marker is not assigned!");

        if (noiseTrapLocationMarker != null) markersToPosition.Add(noiseTrapLocationMarker);
        else Debug.LogError("MapLayoutManager: NoiseTrapLocationMarker is not assigned!");
        
        if (noiseTrapLocationMarker != null && noiseTrapPrefab == null) 
        {
            Debug.LogError("MapLayoutManager: NoiseTrapLocationMarker is assigned, but NoiseTrapPrefab is not! Cannot instantiate noise trap.");
        }

        if (possibleSpawnLocations == null || possibleSpawnLocations.Count == 0)
        {
            Debug.LogError("MapLayoutManager: PossibleSpawnLocations list is null or empty! Cannot randomize locations.");
            return; 
        }
        
        List<Transform> validPossibleLocations = possibleSpawnLocations.Where(loc => loc != null).ToList();

        if (markersToPosition.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: No markers assigned to be placed. Randomization will not occur.");
            return;
        }
        
        if (validPossibleLocations.Count < markersToPosition.Count)
        {
             Debug.LogError("MapLayoutManager: Not enough valid possibleSpawnLocations (" + validPossibleLocations.Count + 
                           ") to place all assigned markers (" + markersToPosition.Count + "). Some items/markers may not be placed.");
            // Allow partial placement if some markers are missing, but log error.
            // return; // Decide if this should be a hard stop or allow partial. For now, allow partial.
        }

        System.Random rng = new System.Random();
        int n = validPossibleLocations.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            Transform value = validPossibleLocations[k];
            validPossibleLocations[k] = validPossibleLocations[n];
            validPossibleLocations[n] = value;
        }

        for(int i=0; i < markersToPosition.Count; i++)
        {
            if (i < validPossibleLocations.Count) // Ensure we don't go out of bounds for locations
            {
                markersToPosition[i].position = validPossibleLocations[i].position;
                Debug.Log("MapLayoutManager: Set position for marker " + markersToPosition[i].name + " to " + validPossibleLocations[i].name + " (" + validPossibleLocations[i].position + ")");
            }
            else
            {
                Debug.LogError("MapLayoutManager: Ran out of validPossibleLocations for marker: " + markersToPosition[i].name);
            }
        }

        if (noiseTrapPrefab != null && noiseTrapLocationMarker != null && noiseTrapLocationMarker.gameObject.activeInHierarchy) // Check if marker was successfully placed
        {
            // Check if noiseTrapLocationMarker received a valid position from the list
            bool markerWasPlaced = false;
            foreach(Transform loc in validPossibleLocations)
            {
                if(noiseTrapLocationMarker.position == loc.position)
                {
                    markerWasPlaced = true;
                    break;
                }
            }
            // A simpler check: if its position is not default (0,0,0) if that's not a valid spawn point
            // Or, more robustly, ensure it was part of the markersToPosition list that got a position.
            // For now, we assume if noiseTrapLocationMarker is not null, it's intended to be used.
            
            // Ensure noiseTrapLocationMarker was actually assigned a position from the list
            if (markersToPosition.Contains(noiseTrapLocationMarker)) {
                 Instantiate(noiseTrapPrefab, noiseTrapLocationMarker.position, Quaternion.identity);
                 Debug.Log("MapLayoutManager: Instantiated NoiseTrap at " + noiseTrapLocationMarker.position);
            } else {
                Debug.LogWarning("MapLayoutManager: NoiseTrapLocationMarker was assigned in inspector but perhaps not enough possible locations to assign it a random spot. Trap not instantiated.");
            }
        }
        else if (noiseTrapPrefab == null && noiseTrapLocationMarker != null) 
        {
             Debug.LogWarning("MapLayoutManager: NoiseTrapLocationMarker is set, but NoiseTrapPrefab not assigned. Trap not instantiated.");
        }
        Debug.Log("MapLayoutManager: Layout randomization complete.");
    }
}
