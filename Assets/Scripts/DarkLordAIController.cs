using UnityEngine;

public class DarkLordAIController : MonoBehaviour
{
    // Energy and Spawning Fields
    public float currentEvilEnergy = 50f;
    public float maxEvilEnergy = 100f;
    public float energyGenerationRate = 2f; // Energy per second
    public GameObject scoutPrefab;
    public float scoutSpawnCost = 10f;
    public GameObject tankPrefab;
    public float tankSpawnCost = 25f;
    public Transform initialSpawnPositionTransform; 

    // AI Spawning Strategy Fields
    private float spawnTimer = 0f;
    public float spawnInterval = 20.0f; // Time between spawn attempts
    private int waveCounter = 0; 

    void Start()
    {
        currentEvilEnergy = maxEvilEnergy * 0.5f; // Start with half energy, for example

        if (scoutPrefab == null)
        {
            Debug.LogError("DarkLordAIController: ScoutPrefab is not assigned!");
        }
        if (tankPrefab == null)
        {
            Debug.LogError("DarkLordAIController: TankPrefab is not assigned!");
        }
        if (initialSpawnPositionTransform == null)
        {
            Debug.LogError("DarkLordAIController: InitialSpawnPositionTransform is not assigned! Minions will spawn at Dark Lord's location.");
            initialSpawnPositionTransform = transform; // Fallback to own transform
        }
    }

    void Update()
    {
        // Generate Evil Energy
        currentEvilEnergy += energyGenerationRate * Time.deltaTime;
        currentEvilEnergy = Mathf.Clamp(currentEvilEnergy, 0, maxEvilEnergy);
        // Debug.Log("Dark Lord AI Current Evil Energy: " + currentEvilEnergy);

        // Spawning Logic
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f; // Reset timer
            ExecuteSpawnStrategy();
        }
    }

    void ExecuteSpawnStrategy()
    {
        waveCounter++;
        Debug.Log("Dark Lord AI attempting to spawn for wave: " + waveCounter + ". Current Energy: " + currentEvilEnergy);

        Vector3 spawnPos = initialSpawnPositionTransform.position;

        // Example simple strategy based on waveCounter
        if (waveCounter % 3 == 1) // e.g., Waves 1, 4, 7... try to spawn Scouts
        {
            if (currentEvilEnergy >= scoutSpawnCost) {
                Instantiate(scoutPrefab, spawnPos, Quaternion.identity);
                currentEvilEnergy -= scoutSpawnCost;
                Debug.Log("Dark Lord AI spawned a Scout. Energy left: " + currentEvilEnergy);
            } else {
                Debug.Log("Dark Lord AI: Not enough energy for Scout. Need: " + scoutSpawnCost);
            }
        }
        else if (waveCounter % 3 == 2) // e.g., Waves 2, 5, 8... try to spawn a Tank
        {
            if (currentEvilEnergy >= tankSpawnCost) {
                Instantiate(tankPrefab, spawnPos, Quaternion.identity);
                currentEvilEnergy -= tankSpawnCost;
                Debug.Log("Dark Lord AI spawned a Tank. Energy left: " + currentEvilEnergy);
            } else {
                Debug.Log("Dark Lord AI: Not enough energy for Tank. Need: " + tankSpawnCost);
            }
        }
        else // e.g., Waves 3, 6, 9... try to spawn a mix or more scouts
        {
            if (currentEvilEnergy >= scoutSpawnCost * 2) { // Try for two scouts
                Instantiate(scoutPrefab, spawnPos + (transform.right * 0.5f), Quaternion.identity); // Offset slightly
                Instantiate(scoutPrefab, spawnPos - (transform.right * 0.5f), Quaternion.identity);
                currentEvilEnergy -= scoutSpawnCost * 2;
                Debug.Log("Dark Lord AI spawned two Scouts. Energy left: " + currentEvilEnergy);
            } else if (currentEvilEnergy >= scoutSpawnCost) {
                Instantiate(scoutPrefab, spawnPos, Quaternion.identity);
                currentEvilEnergy -= scoutSpawnCost;
                Debug.Log("Dark Lord AI spawned one Scout. Energy left: " + currentEvilEnergy);
            } else {
                 Debug.Log("Dark Lord AI: Not enough energy for mixed wave (Scout). Need: " + scoutSpawnCost);
            }
        }
    }
}
