using UnityEngine;

public class DarkLordController : MonoBehaviour
{
    public GameObject scoutPrefab; 
    public Vector3 initialSpawnPosition = new Vector3(0f, 0.25f, 5f); 
    public float scoutSpawnCost = 10f;

    // Tank Spawning Fields
    public GameObject tankPrefab;
    public float tankSpawnCost = 25f;

    // Priest Spawning Fields
    public GameObject priestPrefab;
    public float priestSpawnCost = 20f;

    // Evil Energy Fields
    public float currentEvilEnergy = 50f; 
    public float maxEvilEnergy = 100f;
    public float energyGenerationRate = 2f; 

    // Scout Dispatch
    private bool isPendingScoutDispatch = false;
    public LayerMask groundLayerMask; // Set this in the Inspector

    void Start()
    {
        if (scoutPrefab == null)
        {
            Debug.LogError("DarkLordController: ScoutPrefab is not assigned!");
        }
        if (tankPrefab == null) 
        {
            Debug.LogError("DarkLordController: TankPrefab is not assigned!");
        }
        if (priestPrefab == null)
        {
            Debug.LogError("DarkLordController: PriestPrefab is not assigned!");
        }
    }

    void Update()
    {
        // Generate Evil Energy
        currentEvilEnergy += energyGenerationRate * Time.deltaTime;
        currentEvilEnergy = Mathf.Clamp(currentEvilEnergy, 0, maxEvilEnergy);
        // Debug.Log("Current Evil Energy: " + currentEvilEnergy); 

        // Input for Spawning Scout
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (!isPendingScoutDispatch)
            {
                Debug.Log("Alpha1 key pressed - Pending Scout Dispatch. Click on the map to set target location.");
                isPendingScoutDispatch = true;
            }
            else
            {
                Debug.Log("Scout dispatch cancelled.");
                isPendingScoutDispatch = false; // Allow cancelling by pressing Alpha1 again
            }
        }

        if (isPendingScoutDispatch && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayerMask))
            {
                Vector3 targetDispatchLocation = hit.point;
                Debug.Log("Dispatching Scout to: " + targetDispatchLocation);
                SpawnScout(initialSpawnPosition, targetDispatchLocation); // Call modified SpawnScout
                isPendingScoutDispatch = false;
            }
            else
            {
                Debug.Log("Could not find a valid point on the ground to dispatch Scout. Click on terrain.");
            }
        }

        // Input for Spawning Tank
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("Alpha2 key pressed - attempting to spawn Tank.");
            TrySpawnTank();
        }

        // Input for Spawning Priest
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("Alpha3 key pressed - attempting to spawn Priest.");
            TrySpawnPriest();
        }
    }

    public void SpawnScout(Vector3 spawnPosition, Vector3 targetDispatchLocation)
    {
        if (scoutPrefab == null)
        {
            Debug.LogError("Cannot spawn Scout: ScoutPrefab is not assigned.");
            return;
        }

        if (currentEvilEnergy >= scoutSpawnCost)
        {
            currentEvilEnergy -= scoutSpawnCost;
            GameObject scoutGO = Instantiate(scoutPrefab, spawnPosition, Quaternion.identity);
            ScoutAI scoutAI = scoutGO.GetComponent<ScoutAI>();
            if (scoutAI != null)
            {
                scoutAI.SetInitialMission(targetDispatchLocation);
            }
            else
            {
                Debug.LogError("Spawned Scout does not have a ScoutAI component!");
            }
            Debug.Log("Spawned Scout at: " + spawnPosition + ", dispatched to: " + targetDispatchLocation + ". Energy deducted: " + scoutSpawnCost + ". Remaining energy: " + currentEvilEnergy);
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

    public void TrySpawnPriest()
    {
        if (priestPrefab == null)
        {
            Debug.LogError("Priest Prefab not assigned in DarkLordController!");
            return;
        }

        if (currentEvilEnergy >= priestSpawnCost)
        {
            currentEvilEnergy -= priestSpawnCost;
            Instantiate(priestPrefab, initialSpawnPosition, Quaternion.identity); // Using initialSpawnPosition for now
            Debug.Log("Priest spawned. Energy deducted: " + priestSpawnCost + ". Remaining energy: " + currentEvilEnergy);
        }
        else
        {
            Debug.Log("Not enough Evil Energy to spawn Priest. Current: " + currentEvilEnergy + ", Cost: " + priestSpawnCost);
        }
    }
}
