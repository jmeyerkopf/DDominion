using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class KnightController : HeroControllerBase // Inherit from HeroControllerBase
{
    // Knight Combat Stats
    public float moveSpeed = 2.8f; // isDefeated is now in HeroControllerBase
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
    
    // Interaction Range (already used for village, now for deeds too)
    public float interactionRange = 2.0f; 

    // Knockback
    private CharacterController characterController;
    private Vector3 knockbackVelocity = Vector3.zero;
    private float knockbackDuration = 0f;
    private float knockbackTimer = 0f;

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

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError(gameObject.name + ": CharacterController component not found! Adding one.");
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 2.0f;
            characterController.radius = 0.5f;
            characterController.center = new Vector3(0, 1.0f, 0);
        }

        groundLevelY = transform.position.y; // Still useful for reference or if CC settings are minimal
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
        HandleInteraction(); 
        HandleDetectionLevelDecay(); 
        HandleRevealState(); 
        HandleScryState(); 
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
        if (knockbackTimer < knockbackDuration)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackTimer += Time.deltaTime;
            return; // Skip normal movement if being knocked back
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }

        float horizontalInput = Input.GetAxis("Horizontal"); 
        float verticalInput = Input.GetAxis("Vertical");   

        Vector3 movementInput = new Vector3(horizontalInput, 0, verticalInput);
        movementInput.Normalize(); 
        
        Vector3 finalMovement = movementInput * moveSpeed;
        if (!characterController.isGrounded)
        {
            finalMovement.y = Physics.gravity.y * Time.deltaTime; 
        }

        if (movementInput.magnitude > 0.01f) 
        {
            characterController.Move(finalMovement * Time.deltaTime);

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
                            GainValor(valorFromKill); 
                            // The GainValor method now handles the primary "gained X Valor" log.
                            // We can add a more specific log here if needed, or let GainValor be the sole reporter.
                            Debug.Log("Knight gained " + valorFromKill + " Valor specifically for slaying " + minionGO.name + ".");
                        }
                        break; 
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

    void HandleInteraction() 
    {
        if (Input.GetKeyDown(KeyCode.E)) 
        {
            bool interactedThisPress = false;

            // --- Heroic Deed Interaction ---
            GameObject[] deedObjects = GameObject.FindGameObjectsWithTag("HeroicDeed");
            GameObject closestDeedObject = null;
            float minDistanceDeed = Mathf.Infinity;

            foreach (GameObject deedGO in deedObjects)
            {
                if (!deedGO.activeInHierarchy) continue;
                HeroicDeedObjective objective = deedGO.GetComponent<HeroicDeedObjective>();
                if (objective != null && !objective.isCompleted) 
                {
                    float distanceToDeed = Vector3.Distance(transform.position, deedGO.transform.position);
                    if (distanceToDeed < minDistanceDeed)
                    {
                        minDistanceDeed = distanceToDeed;
                        closestDeedObject = deedGO;
                    }
                }
            }

            if (closestDeedObject != null && minDistanceDeed <= interactionRange)
            {
                HeroicDeedObjective deedToComplete = closestDeedObject.GetComponent<HeroicDeedObjective>();
                if (deedToComplete != null) 
                {
                    deedToComplete.CompleteDeed(this); 
                    // The CompleteDeed method in HeroicDeedObjective now calls knight.GainValor()
                    interactedThisPress = true; 
                    // Knight does not have IsHidden or stealth visuals to break.
                }
            }

            // --- Village Interaction (if no deed was interacted with) ---
            if (!interactedThisPress)
            {
                GameObject[] villages = GameObject.FindGameObjectsWithTag("Village");
                foreach (GameObject villageGO in villages)
                {
                    if (!villageGO.activeInHierarchy) continue;

                    float distanceToVillage = Vector3.Distance(transform.position, villageGO.transform.position);
                    if (distanceToVillage <= interactionRange) 
                    {
                        isInteractingWithVillage = true;
                        currentVillageInteractionTime = 0f;
                        currentInteractingVillage = villageGO;
                        Debug.Log("Knight started 'inspecting' village: " + villageGO.name);
                        interactedThisPress = true; 
                        break; 
                    }
                }
            }
            
            if (!interactedThisPress)
            {
                Debug.Log("Knight pressed E, but no interactable (Deed or Village) found in range.");
            }
        }

        // This manages the duration of the village interaction state.
        if (isInteractingWithVillage)
        {
            currentVillageInteractionTime += Time.deltaTime;
            if (currentInteractingVillage == null || 
                Vector3.Distance(transform.position, currentInteractingVillage.transform.position) > interactionRange + 0.5f || 
                currentVillageInteractionTime >= villageInteractionDuration ||
                Input.GetKeyUp(KeyCode.E)) 
            {
                if(Input.GetKeyUp(KeyCode.E)) Debug.Log("Knight stopped inspecting village (E released).");
                else if (currentVillageInteractionTime >= villageInteractionDuration) Debug.Log("Knight village inspection timed out.");
                else if (currentInteractingVillage == null) Debug.Log("Inspected village became null.");
                else Debug.Log("Knight moved too far from inspected village.");
                StopVillageInteraction();
            }
        }
    }

    public void GainValor(int amount)
    {
        if (amount <= 0) return;

        // Check if already at max level and if valor for the last actual level up was met.
        // valorToNextLevel.Length is the number of level transitions defined.
        // So maxLevel = valorToNextLevel.Length + 1.
        // The last index in valorToNextLevel is valorToNextLevel.Length - 1, which corresponds to leveling up from (maxLevel - 1) to maxLevel.
        
        // If currentLevel is already maxLevel, we only add valor if it's for "post-max-level" accumulation.
        // Otherwise, we check if the valor is enough for the *next* level up.
        if (currentLevel >= maxLevel) 
        {
            // This condition means the Knight is at maxLevel.
            // The CheckForLevelUp() will not trigger further level increases.
            // We can just add valor or cap it if desired.
            valorPoints += amount;
            Debug.Log(name + " is at Max Level (" + maxLevel + "). Gained " + amount + " Valor. Total Valor: " + valorPoints);
            // No call to CheckForLevelUp needed if already at max level and no more progression defined.
            return; 
        }
        
        valorPoints += amount;
        Debug.Log(name + " gained " + amount + " Valor. Total Valor: " + valorPoints);
        CheckForLevelUp();
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

    public void ApplyKnockback(Vector3 force, float duration)
    {
        if (duration <= 0) return;
        knockbackVelocity = force; // Directly use force as velocity
        knockbackDuration = duration;
        knockbackTimer = 0f;
        Debug.Log(gameObject.name + " is knocked back with velocity " + force + " for " + duration + "s!");
        // Knight does not have stealth/cloak to break, but if it had other channeled actions, they'd be interrupted here.
        // For example, if village interaction was a channel:
        // if (isInteractingWithVillage) StopVillageInteraction(); 
    }

    public override void SetDefeated()
    {
        if (isDefeated) return;

        base.isDefeated = true; // Set the inherited flag
        Debug.Log(gameObject.name + " (KnightController) has been defeated via SetDefeated!");

        if (characterController != null) characterController.enabled = false;
        this.enabled = false; // Disable this script's Update loop
        // Health.cs will handle gameObject.SetActive(false)
    }

    // ApplyRevealEffect and ApplyScryEffect are already public and will act as overrides
    // if they were virtual/abstract in base. Since they are concrete in base,
    // we don't need 'override' unless HeroControllerBase's versions were virtual/abstract.
    // For this task, we assume HeroControllerBase defines them as public abstract.
    // If HeroControllerBase made them public virtual, then 'override' is needed here.
    // Given the current HeroControllerBase, these are direct implementations of abstract methods.

    public override void ApplyRevealEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;
        UpdateVisuals(); 
    }

    public override void ApplyScryEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " is being SCRYED for " + duration + " seconds!");
        isScryed = true;
        scryEffectTimer = duration;
        UpdateVisuals(); 
    }
}
