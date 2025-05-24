using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class KnightController : MonoBehaviour
{
    // Knight Combat Stats
    public float moveSpeed = 2.8f; 
    public float attackDamage = 15.0f; 
    public float attackRange = 2.5f; 
    public float attackCooldown = 1.2f;
    public float attackNoiseRadius = 9.0f; 

    public string heroSpawnMarkerName = "HeroSpawnPointMarkerGO"; 

    private Renderer heroRenderer;
    private Color originalColor;
    private float groundLevelY;

    private float attackTimer = 0f;

    // Valor System
    public int valorPoints = 0;
    public int valorFromKill = 10;

    // Village Interaction (kept for potential Priest interaction, can be removed if not used by Knight for anything)
    // If Knight doesn't directly interact with villages in a way that minions should notice, these can be removed.
    // For now, keeping them to ensure compatibility with existing Priest logic if it targets "Hero" interacting.
    public bool isInteractingWithVillage = false; 
    public float currentVillageInteractionTime = 0f; 
    public float villageInteractionDuration = 1.5f; 
    public GameObject currentInteractingVillage = null; 

    // Detection Level (standard for any hero-like character)
    public float detectionLevel = 0f; 
    public float timeToLoseDetection = 1.0f; 
    
    [HideInInspector] 
    public bool isBeingActivelyDetected = false; 

    // Reveal Effect (standard for any hero-like character)
    private bool isRevealed = false;
    private float revealEffectTimer = 0f;
    public Color revealedColor = Color.yellow; 

    // Shield Bash Ability
    public float shieldBashRange = 1.0f;
    public float shieldBashStunDuration = 1.5f;
    public float shieldBashCooldown = 8.0f;
    public float shieldBashNoiseRadius = 6.0f;
    private float shieldBashTimer = 0f;

    // Defensive Stance Ability
    public float defensiveStanceDuration = 5.0f;
    public float defensiveStanceCooldown = 12.0f;
    public float defensiveStanceDamageReduction = 0.5f; // 50% reduction
    private float defensiveStanceTimer = 0f;
    private float defensiveStanceActiveTimer = 0f;
    public bool isDefensiveStanceActive = false;
    private Health selfHealth; // Renamed knightHealthComponent for clarity and leveling

    // Leveling System
    public int[] valorToNextLevel = { 50, 100, 150 }; 
    public int currentLevel = 1;
    public int maxLevel; // Will be set in Start
    public float attackDamagePerLevel = 5f;
    public float maxHealthPerLevel = 20f;

    // Scry Effect
    private bool isScryed = false;
    private float scryEffectTimer = 0f;
    public Color scryedColor = Color.cyan;

    void Start()
    {
        GameObject marker = GameObject.Find(heroSpawnMarkerName);
        if (marker != null)
        {
            transform.position = marker.transform.position;
        }
        else
        {
            Debug.LogError(gameObject.name + ": Could not find hero spawn marker named '" + heroSpawnMarkerName + "'. Knight will not be repositioned.", this);
        }

        heroRenderer = GetComponent<Renderer>();
        if (heroRenderer != null && heroRenderer.material != null)
        {
            originalColor = heroRenderer.material.color;
        }
        else
        {
            Debug.LogError(gameObject.name + ": Renderer or Material not found on Knight GameObject!");
        }
        selfHealth = GetComponent<Health>(); // Assign selfHealth
        if (selfHealth == null)
        {
            Debug.LogError(gameObject.name + ": Health component not found on Knight GameObject!");
        }

        groundLevelY = transform.position.y; 
        maxLevel = valorToNextLevel.Length + 1; // Initialize maxLevel
    }

    void Update()
    {
        // Decrement ability timers
        if (shieldBashTimer > 0) shieldBashTimer -= Time.deltaTime;
        if (defensiveStanceTimer > 0) defensiveStanceTimer -= Time.deltaTime;

        if (isDefensiveStanceActive)
        {
            defensiveStanceActiveTimer -= Time.deltaTime;
            if (defensiveStanceActiveTimer <= 0f)
            {
                isDefensiveStanceActive = false;
                if (knightHealthComponent != null) knightHealthComponent.SetDamageReduction(0f);
                Debug.Log("Knight's Defensive Stance wore off.");
                // Potentially change visuals back if stance had a visual effect
            }
        }

        HandleMovement(); 
        HandleAttack();
        HandleShieldBashInput(); 
        HandleDefensiveStanceInput(); 
        HandleInteractionWithVillage(); 
        HandleDetectionLevelDecay(); 
        HandleRevealState(); 
        HandleScryState(); // Manage scry effect duration
        UpdateVisuals(); 
    }
    
    void HandleDetectionLevelDecay()
    {
        if (!isBeingActivelyDetected && detectionLevel > 0)
        {
            detectionLevel -= Time.deltaTime / timeToLoseDetection;
        }
        detectionLevel = Mathf.Clamp01(detectionLevel);
        isBeingActivelyDetected = false; 
    }

    void HandleMovement() 
    {
        float horizontalInput = Input.GetAxis("Horizontal"); 
        float verticalInput = Input.GetAxis("Vertical");   

        Vector3 movement = new Vector3(horizontalInput, 0, verticalInput);
        movement.Normalize(); 

        if (movement.magnitude > 0.01f) 
        {
            transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);

            Vector3 currentPosition = transform.position;
            currentPosition.y = groundLevelY;
            transform.position = currentPosition;

            if (isInteractingWithVillage) 
            {
                Debug.Log("Knight moved, stopping village interaction.");
                StopVillageInteraction();
            }
        }
    }

    void HandleAttack()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Space) && attackTimer <= 0) 
        {
            attackTimer = attackCooldown;
            NoiseManager.MakeNoise(transform.position, attackNoiseRadius); 

            // Define which tags the Knight can attack
            string[] attackableTags = {"Scout", "Tank", "Priest"}; 
            List<GameObject> allMinions = new List<GameObject>();
            foreach(string tag in attackableTags)
            {
                allMinions.AddRange(GameObject.FindGameObjectsWithTag(tag));
            }

            foreach (GameObject minionGO in allMinions)
            {
                if (!minionGO.activeInHierarchy) continue; 

                float distanceToMinion = Vector3.Distance(transform.position, minionGO.transform.position);
                if (distanceToMinion <= attackRange)
                {
                    Health minionHealth = minionGO.GetComponent<Health>();
                    if (minionHealth != null)
                    {
                        bool wasKilled = minionHealth.TakeDamage(attackDamage);
                        if (wasKilled)
                        {
                            valorPoints += valorFromKill;
                            Debug.Log("Knight gained " + valorFromKill + " Valor for slaying " + minionGO.name + ". Total Valor: " + valorPoints);
                            CheckForLevelUp(); // Check for level up after gaining valor
                        }
                        break; // Knight hits one target per swing
                    }
                }
            }
        }
    }

    private void CheckForLevelUp()
    {
        while (currentLevel < maxLevel && valorPoints >= valorToNextLevel[currentLevel - 1])
        {
            int valorNeededForThisLevel = valorToNextLevel[currentLevel - 1];
            valorPoints -= valorNeededForThisLevel; // Valor carries over excess
            currentLevel++;

            // Apply Stat Boosts
            attackDamage += attackDamagePerLevel;
            if (selfHealth != null)
            {
                selfHealth.IncreaseMaxHealth(maxHealthPerLevel);
                selfHealth.Heal(maxHealthPerLevel); // Heal by the increased amount
            }
            
            Debug.Log("Knight leveled up to Level " + currentLevel + "! Attack: " + attackDamage + 
                      (selfHealth != null ? ", Max Health: " + selfHealth.GetMaxHealth() : "") + 
                      ". Valor towards next level: " + valorPoints);

            if (currentLevel == maxLevel)
            {
                Debug.Log("Knight reached Max Level!");
                // valorPoints might still accumulate but won't trigger further level ups.
                break; 
            }
        }
    }

    // Renamed from HandleInteraction to be more specific if this functionality is kept solely for village interaction.
    // If Knight has other 'E' interactions, this can be expanded.
    void HandleInteractionWithVillage() 
    {
        if (Input.GetKeyDown(KeyCode.E)) 
        {
            // Example: Knight "inspects" a village, making them a target for Priests or other interactions.
            // This does not involve looting or gold.
            GameObject[] villages = GameObject.FindGameObjectsWithTag("Village");
            foreach (GameObject villageGO in villages)
            {
                if (!villageGO.activeInHierarchy) continue;

                float distanceToVillage = Vector3.Distance(transform.position, villageGO.transform.position);
                // Using a generic interaction range, not lootRange.
                // This range (1.0f) should ideally be a public variable if this interaction is complex.
                if (distanceToVillage <= 1.0f) 
                {
                    isInteractingWithVillage = true;
                    currentVillageInteractionTime = 0f;
                    currentInteractingVillage = villageGO;
                    Debug.Log("Knight started 'inspecting' village: " + villageGO.name);
                    break; 
                }
            }
        }

        // This manages the duration of the interaction state.
        if (isInteractingWithVillage)
        {
            currentVillageInteractionTime += Time.deltaTime;
            if (currentInteractingVillage == null || 
                Vector3.Distance(transform.position, currentInteractingVillage.transform.position) > 1.5f || // Range + buffer
                currentVillageInteractionTime >= villageInteractionDuration ||
                Input.GetKeyUp(KeyCode.E)) // Stop if E is released
            {
                if(Input.GetKeyUp(KeyCode.E)) Debug.Log("Knight stopped inspecting village (E released).");
                else if (currentVillageInteractionTime >= villageInteractionDuration) Debug.Log("Knight village inspection timed out.");
                else if (currentInteractingVillage == null) Debug.Log("Inspected village became null.");
                else Debug.Log("Knight moved too far from inspected village.");
                StopVillageInteraction();
            }
        }
    }

    void HandleShieldBashInput()
    {
        if (Input.GetKeyDown(KeyCode.Q) && shieldBashTimer <= 0)
        {
            shieldBashTimer = shieldBashCooldown;
            NoiseManager.MakeNoise(transform.position, shieldBashNoiseRadius);
            Debug.Log("Knight attempts Shield Bash!");

            GameObject closestMinion = null;
            float minDistance = shieldBashRange + 1f; // Start with a distance greater than range

            string[] targetTags = { "Scout", "Tank", "Priest" };
            List<GameObject> potentialTargets = new List<GameObject>();
            foreach (string tag in targetTags)
            {
                potentialTargets.AddRange(GameObject.FindGameObjectsWithTag(tag));
            }

            foreach (GameObject minionGO in potentialTargets)
            {
                if (!minionGO.activeInHierarchy) continue;

                float distanceToMinion = Vector3.Distance(transform.position, minionGO.transform.position);
                Vector3 directionToMinion = (minionGO.transform.position - transform.position).normalized;
                float dotProduct = Vector3.Dot(transform.forward, directionToMinion);

                if (distanceToMinion <= shieldBashRange && dotProduct > 0.7f) // Check range and if in front
                {
                    if (distanceToMinion < minDistance) // Found a closer target in front
                    {
                        minDistance = distanceToMinion;
                        closestMinion = minionGO;
                    }
                }
            }

            if (closestMinion != null)
            {
                Debug.Log("Knight Shield Bashed " + closestMinion.name);
                // Try to get AI components and apply stun
                ScoutAI scoutAI = closestMinion.GetComponent<ScoutAI>();
                if (scoutAI != null) scoutAI.ApplyStun(shieldBashStunDuration);

                TankAI tankAI = closestMinion.GetComponent<TankAI>();
                if (tankAI != null) tankAI.ApplyStun(shieldBashStunDuration);

                PriestAI priestAI = closestMinion.GetComponent<PriestAI>();
                if (priestAI != null) priestAI.ApplyStun(shieldBashStunDuration);
            }
            else
            {
                Debug.Log("Knight Shield Bash: No target in range/front.");
            }
        }
    }

    void HandleDefensiveStanceInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && defensiveStanceTimer <= 0 && !isDefensiveStanceActive)
        {
            isDefensiveStanceActive = true;
            defensiveStanceActiveTimer = defensiveStanceDuration;
            defensiveStanceTimer = defensiveStanceCooldown;
            if (knightHealthComponent != null) knightHealthComponent.SetDamageReduction(defensiveStanceDamageReduction);
            Debug.Log("Knight activated Defensive Stance! Damage reduction: " + (defensiveStanceDamageReduction * 100) + "%");
            // Potentially change visuals to indicate stance
        }
    }


    void StopVillageInteraction() 
    {
        if (isInteractingWithVillage) 
        {
            Debug.Log("Knight stopping 'inspection' of village: " + (currentInteractingVillage != null ? currentInteractingVillage.name : "N/A"));
        }
        isInteractingWithVillage = false;
        currentVillageInteractionTime = 0f;
        currentInteractingVillage = null;
    }
    
    void OnDisable()
    {
        if (heroRenderer != null && heroRenderer.material != null && originalColor != null) 
        { 
            heroRenderer.material.color = originalColor; 
        }
    }

    void UpdateVisuals() 
    {
        if (heroRenderer == null || heroRenderer.material == null) return;

        if (isRevealed)
        {
            heroRenderer.material.color = revealedColor;
        }
        else if (isScryed)
        {
            heroRenderer.material.color = scryedColor;
        }
        else
        {
            heroRenderer.material.color = originalColor;
        }
    }

    public void ApplyRevealEffect(float duration)
    {
        Debug.Log(gameObject.name + " has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;
        UpdateVisuals(); 
    }

    void HandleRevealState()
    {
        if (isRevealed)
        {
            revealEffectTimer -= Time.deltaTime;
            if (revealEffectTimer <= 0)
            {
                isRevealed = false;
                revealEffectTimer = 0f;
                Debug.Log(gameObject.name + " reveal effect wore off.");
                UpdateVisuals(); 
            }
        }
    }

    public void ApplyScryEffect(float duration)
    {
        Debug.Log(gameObject.name + " is being SCRYED for " + duration + " seconds!");
        isScryed = true;
        scryEffectTimer = duration;
        UpdateVisuals(); // Immediately update visuals
    }

    void HandleScryState()
    {
        if (isScryed)
        {
            scryEffectTimer -= Time.deltaTime;
            if (scryEffectTimer <= 0)
            {
                isScryed = false;
                scryEffectTimer = 0f;
                Debug.Log(gameObject.name + " scry effect wore off.");
                UpdateVisuals(); 
            }
        }
    }
}
