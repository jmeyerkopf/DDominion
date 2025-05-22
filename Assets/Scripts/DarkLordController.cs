using UnityEngine;

public class DarkLordController : MonoBehaviour
{
    public GameObject scoutPrefab; 
    public Vector3 initialSpawnPosition = new Vector3(0f, 0.25f, 5f); 
    public float scoutSpawnCost = 10f;

    // Tank Spawning Fields
    public GameObject tankPrefab;
    public float tankSpawnCost = 25f;

    // Evil Energy Fields
    public float currentEvilEnergy = 50f; 
    public float maxEvilEnergy = 100f;
    public float energyGenerationRate = 2f; 

    void Start()
    {
        if (scoutPrefab == null)
        {
            Debug.LogError("DarkLordController: ScoutPrefab is not assigned!");
        }
        if (tankPrefab == null) // Added check for tank prefab
        {
            Debug.LogError("DarkLordController: TankPrefab is not assigned!");
        }
    }

    void Update()
    {
        // Generate Evil Energy
        currentEvilEnergy += energyGenerationRate * Time.deltaTime;
        currentEvilEnergy = Mathf.Clamp(currentEvilEnergy, 0, maxEvilEnergy);
        // Debug.Log("Current Evil Energy: " + currentEvilEnergy); 

        // Test Input for Spawning Scout
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("Alpha1 key pressed - attempting to spawn Scout.");
            SpawnScout(initialSpawnPosition);
        }

        // Test Input for Spawning Tank
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("Alpha2 key pressed - attempting to spawn Tank.");
            TrySpawnTank();
        }
    }

    public void SpawnScout(Vector3 spawnPosition)
    {
        if (scoutPrefab == null)
        {
            Debug.LogError("Cannot spawn Scout: ScoutPrefab is not assigned.");
            return;
        }

        if (currentEvilEnergy >= scoutSpawnCost)
        {
            currentEvilEnergy -= scoutSpawnCost;
            Instantiate(scoutPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("Spawned Scout at: " + spawnPosition + ". Energy deducted: " + scoutSpawnCost + ". Remaining energy: " + currentEvilEnergy);
        }
        else
        {
            Debug.Log("Not enough Evil Energy to spawn Scout. Current: " + currentEvilEnergy + ", Cost: " + scoutSpawnCost);
        }
    }

    public void TrySpawnTank()
    {
        if (tankPrefab == null)
        {
            Debug.LogError("Tank Prefab not assigned in DarkLordController!");
            return;
        }

        if (currentEvilEnergy >= tankSpawnCost)
        {
            currentEvilEnergy -= tankSpawnCost;
            Instantiate(tankPrefab, initialSpawnPosition, Quaternion.identity); // Using initialSpawnPosition for consistency
            Debug.Log("Tank spawned. Energy deducted: " + tankSpawnCost + ". Remaining energy: " + currentEvilEnergy);
        }
        else
        {
            Debug.Log("Not enough Evil Energy to spawn Tank. Current: " + currentEvilEnergy + ", Cost: " + tankSpawnCost);
        }
    }
}
