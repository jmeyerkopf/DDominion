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

    // Ingredient Source Spawning
    public List<GameObject> ingredientSourcePrefabs;
    public List<Transform> possibleIngredientSpawnLocations;
    public int numberOfIngredientsToSpawn = 5;

    // Ley Line Shrine Spawning
    public GameObject leyLineShrinePrefab;
    public List<Transform> possibleShrineSpawnLocations;
    public int numberOfShrinesToSpawn = 3;

    // Heroic Deed Spawning
    public List<GameObject> heroicDeedPrefabs;
    public List<Transform> possibleDeedSpawnLocations;
    public int numberOfDeedsToSpawn = 2;

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
        Debug.Log("MapLayoutManager: Layout randomization complete for standard markers.");

        // --- Ingredient Source Spawning Logic ---
        if (ingredientSourcePrefabs == null || ingredientSourcePrefabs.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: ingredientSourcePrefabs list is null or empty. Skipping ingredient spawning.");
            return; // Exit if no ingredient prefabs to spawn
        }
        if (ingredientSourcePrefabs.Any(prefab => prefab == null))
        {
            Debug.LogWarning("MapLayoutManager: ingredientSourcePrefabs list contains one or more null entries. These will be skipped.");
            ingredientSourcePrefabs = ingredientSourcePrefabs.Where(prefab => prefab != null).ToList();
            if (ingredientSourcePrefabs.Count == 0)
            {
                Debug.LogWarning("MapLayoutManager: After removing null entries, ingredientSourcePrefabs list is empty. Skipping ingredient spawning.");
                return;
            }
        }


        if (possibleIngredientSpawnLocations == null || possibleIngredientSpawnLocations.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: possibleIngredientSpawnLocations list is null or empty. Skipping ingredient spawning.");
            return; // Exit if no spawn locations
        }

        List<Transform> availableIngredientSpawns = new List<Transform>(possibleIngredientSpawnLocations.Where(loc => loc != null));
        if (availableIngredientSpawns.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: All entries in possibleIngredientSpawnLocations are null. Skipping ingredient spawning.");
            return;
        }
        
        // Shuffle availableIngredientSpawns (using the same Fisher-Yates shuffle)
        int m = availableIngredientSpawns.Count;
        while (m > 1)
        {
            m--;
            int k = rng.Next(m + 1); // rng is from the previous part of Awake()
            Transform value = availableIngredientSpawns[k];
            availableIngredientSpawns[k] = availableIngredientSpawns[m];
            availableIngredientSpawns[m] = value;
        }

        int countToSpawn = Mathf.Min(numberOfIngredientsToSpawn, availableIngredientSpawns.Count);
        if (numberOfIngredientsToSpawn > availableIngredientSpawns.Count)
        {
            Debug.LogWarning("MapLayoutManager: Requested to spawn " + numberOfIngredientsToSpawn + 
                             " ingredients, but only " + availableIngredientSpawns.Count + 
                             " unique spawn locations are available. Spawning " + countToSpawn + " ingredients.");
        }
        if (countToSpawn == 0 && numberOfIngredientsToSpawn > 0) // This case happens if availableIngredientSpawns.Count was 0 but numberOfIngredientsToSpawn > 0
        {
             Debug.LogWarning("MapLayoutManager: No valid spawn locations available for ingredients, although " + numberOfIngredientsToSpawn + " were requested.");
        }


        for (int i = 0; i < countToSpawn; i++)
        {
            if (ingredientSourcePrefabs.Count == 0) // Should be caught earlier, but as a safeguard
            {
                Debug.LogError("MapLayoutManager: No ingredient source prefabs available to spawn.");
                break;
            }

            GameObject prefabToSpawn = ingredientSourcePrefabs[Random.Range(0, ingredientSourcePrefabs.Count)];
            Transform spawnPoint = availableIngredientSpawns[i];

            Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation); // Using spawnPoint.rotation
            Debug.Log("MapLayoutManager: Spawned " + prefabToSpawn.name + " at " + spawnPoint.name + " (" + spawnPoint.position + ")");
        }
        Debug.Log("MapLayoutManager: Ingredient source spawning complete. Spawned " + countToSpawn + " ingredients.");

        // --- Ley Line Shrine Spawning Logic ---
        if (leyLineShrinePrefab == null)
        {
            Debug.LogWarning("MapLayoutManager: leyLineShrinePrefab is not assigned. Skipping shrine spawning.");
            return; // Exit if no shrine prefab to spawn
        }

        if (possibleShrineSpawnLocations == null || possibleShrineSpawnLocations.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: possibleShrineSpawnLocations list is null or empty. Skipping shrine spawning.");
            return; // Exit if no spawn locations
        }

        List<Transform> availableShrineSpawns = new List<Transform>(possibleShrineSpawnLocations.Where(loc => loc != null));
        if (availableShrineSpawns.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: All entries in possibleShrineSpawnLocations are null. Skipping shrine spawning.");
            return;
        }
        
        // Shuffle availableShrineSpawns (using the same Fisher-Yates shuffle - rng is from earlier in Awake)
        int s_n = availableShrineSpawns.Count;
        while (s_n > 1)
        {
            s_n--;
            int k = rng.Next(s_n + 1);
            Transform value = availableShrineSpawns[k];
            availableShrineSpawns[k] = availableShrineSpawns[s_n];
            availableShrineSpawns[s_n] = value;
        }

        int shrinesToSpawnCount = Mathf.Min(numberOfShrinesToSpawn, availableShrineSpawns.Count);
        if (numberOfShrinesToSpawn > availableShrineSpawns.Count)
        {
            Debug.LogWarning("MapLayoutManager: Requested to spawn " + numberOfShrinesToSpawn + 
                             " shrines, but only " + availableShrineSpawns.Count + 
                             " unique spawn locations are available. Spawning " + shrinesToSpawnCount + " shrines.");
        }
         if (shrinesToSpawnCount == 0 && numberOfShrinesToSpawn > 0)
        {
             Debug.LogWarning("MapLayoutManager: No valid spawn locations available for shrines, although " + numberOfShrinesToSpawn + " were requested.");
        }


        for (int i = 0; i < shrinesToSpawnCount; i++)
        {
            Transform spawnPoint = availableShrineSpawns[i];
            Instantiate(leyLineShrinePrefab, spawnPoint.position, spawnPoint.rotation);
            Debug.Log("MapLayoutManager: Spawned Ley Line Shrine at " + spawnPoint.name + " (" + spawnPoint.position + ")");
        }
        Debug.Log("MapLayoutManager: Ley Line Shrine spawning complete. Spawned " + shrinesToSpawnCount + " shrines.");

        // --- Heroic Deed Spawning Logic ---
        if (heroicDeedPrefabs == null || heroicDeedPrefabs.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: heroicDeedPrefabs list is null or empty. Skipping deed spawning.");
            return; 
        }
        if (heroicDeedPrefabs.Any(prefab => prefab == null))
        {
            Debug.LogWarning("MapLayoutManager: heroicDeedPrefabs list contains one or more null entries. These will be skipped.");
            heroicDeedPrefabs = heroicDeedPrefabs.Where(prefab => prefab != null).ToList();
            if(heroicDeedPrefabs.Count == 0) 
            {
                 Debug.LogWarning("MapLayoutManager: After removing nulls, heroicDeedPrefabs list is empty. Skipping deed spawning.");
                 return;
            }
        }

        if (possibleDeedSpawnLocations == null || possibleDeedSpawnLocations.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: possibleDeedSpawnLocations list is null or empty. Skipping deed spawning.");
            return; 
        }

        List<Transform> availableDeedSpawns = new List<Transform>(possibleDeedSpawnLocations.Where(loc => loc != null));
        if (availableDeedSpawns.Count == 0)
        {
            Debug.LogWarning("MapLayoutManager: All entries in possibleDeedSpawnLocations are null. Skipping deed spawning.");
            return;
        }

        // Shuffle availableDeedSpawns (rng is from earlier in Awake)
        int d_n = availableDeedSpawns.Count;
        while (d_n > 1)
        {
            d_n--;
            int k = rng.Next(d_n + 1);
            Transform value = availableDeedSpawns[k];
            availableDeedSpawns[k] = availableDeedSpawns[d_n];
            availableDeedSpawns[d_n] = value;
        }

        int deedsToSpawnCount = Mathf.Min(numberOfDeedsToSpawn, availableDeedSpawns.Count);
        if (numberOfDeedsToSpawn > availableDeedSpawns.Count)
        {
            Debug.LogWarning("MapLayoutManager: Requested to spawn " + numberOfDeedsToSpawn + 
                             " deeds, but only " + availableDeedSpawns.Count + 
                             " unique spawn locations are available. Spawning " + deedsToSpawnCount + " deeds.");
        }
        if (deedsToSpawnCount == 0 && numberOfDeedsToSpawn > 0)
        {
             Debug.LogWarning("MapLayoutManager: No valid spawn locations available for deeds, although " + numberOfDeedsToSpawn + " were requested.");
        }


        for (int i = 0; i < deedsToSpawnCount; i++)
        {
            GameObject prefabToSpawn = heroicDeedPrefabs[Random.Range(0, heroicDeedPrefabs.Count)];
            Transform spawnPoint = availableDeedSpawns[i];
            Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            Debug.Log("MapLayoutManager: Spawned Heroic Deed '" + prefabToSpawn.name + "' at " + spawnPoint.name + " (" + spawnPoint.position + ")");
        }
        Debug.Log("MapLayoutManager: Heroic Deed spawning complete. Spawned " + deedsToSpawnCount + " deeds.");
    }
}
