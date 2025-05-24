using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class WitchController : HeroControllerBase // Inherit from HeroControllerBase
{
    // Witch Stats
    public float moveSpeed = 3.0f; // isDefeated is now in HeroControllerBase
    public bool IsHidden { get; private set; } 
    public float timeToHide = 3.0f; 
    public string heroSpawnMarkerName = "HeroSpawnPointMarkerGO"; 

    private float hideTimer = 0f;
    private Renderer heroRenderer; // Renamed from witchRenderer to heroRenderer for consistency
    private Color originalColor;
    private Color stealthColor; 
    private float groundLevelY;
    // private Health selfHealthComponent; // Health component is managed by HeroControllerBase or an attached Health script

    // Attack properties (placeholder, will be for spells)
    public float attackDamage = 7.0f; 
    public float attackRange = 1.5f;  // Will likely change for ranged spells
    public float attackCooldown = 2.0f; 
    private float attackTimer = 0f;
    public float attackNoiseRadius = 5.0f; 

    // Interaction (for Shrines)
    public float interactionRange = 2.0f; 

    // Village Interaction (Placeholder, likely not used by Witch directly. Kept for structural compatibility if needed by other systems).
    public bool isInteractingWithVillage = false; 
    public float currentVillageInteractionTime = 0f; 
    public float villageInteractionDuration = 1.5f; 
    public GameObject currentInteractingVillage = null; 

    // Detection Level (standard)
    public float detectionLevel = 0f; 
    public float timeToLoseDetection = 1.0f; 
    
    [HideInInspector] 
    public bool isBeingActivelyDetected = false; 

    // Reveal Effect (standard) - Fields already present, ensure they are used by new methods
    private bool isRevealed = false;
    private float revealEffectTimer = 0f;
    public Color revealedColor = Color.yellow; 

    // Scry Effect (standard) - Fields already present, ensure they are used by new methods
    private bool isScryed = false;
    private float scryEffectTimer = 0f;
    public Color scryedColor = Color.cyan;

    // Witch-Specific: Arcane Energy
    public float arcaneEnergy = 0f;
    public float maxArcaneEnergy = 100f;
    public float arcaneEnergyPerShrine = 25f;

    // Spellcasting
    public GameObject shadowBoltPrefab; // Assign in Inspector
    public Transform spellSpawnPoint;   // Optional: child GameObject for spell origin
    public float shadowBoltCost = 10f;
    public float shadowBoltDamage = 15f;
    public float shadowBoltCooldown = 2.0f;
    private float shadowBoltTimer = 0f;

    // Channeling-Related Fields
    private bool isChanneling = false;
    private float currentChannelTime = 0f;
    private LeyLineShrine targetShrine = null;
    private Vector3 positionAtChannelStart; 

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
            Debug.LogError(gameObject.name + ": Could not find hero spawn marker named '" + heroSpawnMarkerName + "'. Witch will not be repositioned.", this);
        }

        heroRenderer = GetComponent<Renderer>();
        if (heroRenderer != null && heroRenderer.material != null)
        {
            originalColor = heroRenderer.material.color;
            stealthColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f); 
        }
        else
        {
            Debug.LogError(gameObject.name + " (WitchController): Renderer or Material not found on Witch GameObject!");
        }

        // selfHealthComponent = GetComponent<Health>(); // Health is handled by base or separate component
        // if (selfHealthComponent == null)
        // {
        //     Debug.LogError(gameObject.name + ": Health component not found for Witch!");
        // }

        groundLevelY = transform.position.y; 
        IsHidden = false; 

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError(gameObject.name + " (WitchController): CharacterController component not found! Adding one.");
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 2.0f;
            characterController.radius = 0.5f;
            characterController.center = new Vector3(0, 1.0f, 0);
        }
    }

    void Update()
    {
        if (isDefeated) return; // Stop updates if defeated

        // Timers
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (shadowBoltTimer > 0) shadowBoltTimer -= Time.deltaTime;
        // (Channeling timer is handled in HandleChannelingProgress)

        HandleMovementAndStealth();
        HandleAttack(); // Weak Melee Jab
        HandleShadowBoltInput(); // Ranged Spell
        HandleInteraction(); 
        if(isChanneling) HandleChannelingProgress(); 
        // HandleVillageInteractionState(); 
        HandleDetectionLevelDecay(); 
        HandleRevealState(); // Handles timer for reveal effect
        HandleScryState();   // Handles timer for scry effect
        UpdateVisuals(); // Updates color based on state (hidden, revealed, scryed)
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

    void HandleMovementAndStealth()
    {
        if (knockbackTimer < knockbackDuration)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackTimer += Time.deltaTime;
            return; 
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

            if (IsHidden) 
            {
                IsHidden = false;
                Debug.Log("Witch moved, stealth broken.");
            }
            hideTimer = 0f;

            if (isInteractingWithVillage && !isChanneling) 
            {
                Debug.Log("Witch moved, stopping village interaction.");
                StopVillageInteraction();
            }
        }
        else 
        {
            if (!isRevealed && !isScryed && !IsHidden) 
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= timeToHide)
                {
                    IsHidden = true;
                    Debug.Log("Witch is now hidden.");
                }
            }
        }
    }

    void HandleAttack() // Repurposed as Weak Arcane Jab (Melee)
    {
        // Note: attackDamage, attackRange, attackCooldown, attackNoiseRadius are now set to weaker values for this jab.
        // This is the Spacebar attack.

        if (Input.GetKeyDown(KeyCode.Space) && attackTimer <= 0) 
        {
            attackTimer = attackCooldown; // Uses the Witch's general attackCooldown
            NoiseManager.MakeNoise(transform.position, attackNoiseRadius); 
            if (IsHidden)
            {
                IsHidden = false; 
                Debug.Log("Witch used Arcane Jab, stealth broken.");
                hideTimer = 0f;
            }

            string[] attackableTags = {"Scout", "Tank", "Priest"}; 
            foreach(string tag in attackableTags)
            {
                GameObject[] minions = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject minionGO in minions)
                {
                    if (!minionGO.activeInHierarchy) continue;
                    float distanceToMinion = Vector3.Distance(transform.position, minionGO.transform.position);
                    if (distanceToMinion <= attackRange) // Uses Witch's melee attackRange
                    {
                        Health minionHealth = minionGO.GetComponent<Health>();
                        if (minionHealth != null)
                        {
                            minionHealth.TakeDamage(attackDamage); // Uses Witch's weak attackDamage
                            Debug.Log("Witch jabbed " + minionGO.name + " for " + attackDamage + " damage.");
                            return; 
                        }
                    }
                }
            }
        }
    }

    void HandleShadowBoltInput()
    {
        if (Input.GetMouseButtonDown(1) && shadowBoltTimer <= 0) // Right Mouse Button
        {
            if (arcaneEnergy >= shadowBoltCost)
            {
                arcaneEnergy -= shadowBoltCost;
                shadowBoltTimer = shadowBoltCooldown;

                Vector3 spawnPos = (spellSpawnPoint != null) ? spellSpawnPoint.position : transform.position + transform.forward * 1.0f;
                Quaternion spawnRot = transform.rotation; // Fire in direction Witch is facing

                GameObject bolt = Instantiate(shadowBoltPrefab, spawnPos, spawnRot);
                Projectile projectileComponent = bolt.GetComponent<Projectile>();
                if (projectileComponent != null)
                {
                    projectileComponent.damagePayload = shadowBoltDamage;
                    // projectileComponent.targetTag can be left as default or set if needed
                }
                
                NoiseManager.MakeNoise(transform.position, 7.0f); // Shadow Bolt makes noise
                if (IsHidden)
                {
                    IsHidden = false;
                    hideTimer = 0f;
                    Debug.Log("Witch fired Shadow Bolt, stealth broken.");
                }
                Debug.Log("Fired Shadow Bolt. Arcane Energy: " + arcaneEnergy);
            }
            else
            {
                Debug.Log("Not enough Arcane Energy for Shadow Bolt. Current: " + arcaneEnergy + ", Cost: " + shadowBoltCost);
            }
        }
    }

    void HandleInteraction() 
    {
        if (isChanneling) // Do not start new interaction if already channeling
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E)) 
        {
            GameObject[] shrines = GameObject.FindGameObjectsWithTag("LeyLineShrine");
            GameObject closestShrineGO = null;
            float minDistance = Mathf.Infinity;

            foreach (GameObject shrineGO in shrines)
            {
                if (!shrineGO.activeInHierarchy) continue;

                LeyLineShrine shrineComponent = shrineGO.GetComponent<LeyLineShrine>();
                if (shrineComponent != null && !shrineComponent.isClaimed)
                {
                    float distanceToShrine = Vector3.Distance(transform.position, shrineGO.transform.position);
                    if (distanceToShrine < minDistance)
                    {
                        minDistance = distanceToShrine;
                        closestShrineGO = shrineGO;
                    }
                }
            }

            if (closestShrineGO != null && minDistance <= interactionRange)
            {
                targetShrine = closestShrineGO.GetComponent<LeyLineShrine>();
                if (targetShrine != null && !targetShrine.isClaimed) // Double check not claimed before starting
                {
                    isChanneling = true;
                    currentChannelTime = 0f;
                    positionAtChannelStart = transform.position;
                    Debug.Log("Witch started channeling " + targetShrine.shrineName);
                    if (IsHidden)
                    {
                        IsHidden = false;
                        hideTimer = 0f;
                        Debug.Log("Witch started channeling, stealth broken.");
                    }
                    // Stop any village interaction if it was somehow active
                    if(isInteractingWithVillage) StopVillageInteraction(); 
                }
            }
            else
            {
                 Debug.Log("Witch pressed E - No unclaimed Ley Line Shrine in range.");
            }
        }
    }

    void HandleChannelingProgress()
    {
        if (targetShrine == null || targetShrine.isClaimed)
        {
            StopChanneling(false); 
            return;
        }

        if (Vector3.Distance(transform.position, positionAtChannelStart) > 0.1f)
        {
            Debug.Log("Witch channeling interrupted by movement.");
            StopChanneling(false); 
            return;
        }

        currentChannelTime += Time.deltaTime;
        // Optional: Debug.Log("Channeling " + targetShrine.shrineName + ": " + currentChannelTime.ToString("F1") + "/" + targetShrine.channelDuration.ToString("F1"));

        if (currentChannelTime >= targetShrine.channelDuration)
        {
            targetShrine.ClaimShrine();
            arcaneEnergy = Mathf.Min(arcaneEnergy + arcaneEnergyPerShrine, maxArcaneEnergy);
            Debug.Log("Witch successfully channeled " + targetShrine.shrineName + ". Gained " + arcaneEnergyPerShrine + " Arcane Energy. Total: " + arcaneEnergy);
            StopChanneling(true);
        }
    }

    void StopChanneling(bool successfullyCompleted)
    {
        isChanneling = false;
        currentChannelTime = 0f;
        if (!successfullyCompleted && targetShrine != null)
        {
            Debug.Log("Channeling of " + targetShrine.shrineName + " failed or was interrupted.");
        }
        targetShrine = null;
    }
    
    // Village interaction methods kept for potential future use or if another system relies on them,
    // but they are not actively used by the Witch's core interaction loop now.
    // void HandleVillageInteractionState()
    // {
    //     if (isInteractingWithVillage)
    //     {
    //         currentVillageInteractionTime += Time.deltaTime;
    //         if (currentInteractingVillage == null || 
    //             Vector3.Distance(transform.position, currentInteractingVillage.transform.position) > interactionRange + 0.5f || 
    //             currentVillageInteractionTime >= villageInteractionDuration) 
    //         {
    //             StopVillageInteraction();
    //         }
    //     }
    // }

    void StopVillageInteraction()
    {
        if (isInteractingWithVillage) 
        {
            Debug.Log("Witch stopping interaction with village: " + (currentInteractingVillage != null ? currentInteractingVillage.name : "N/A"));
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
        else if (IsHidden) 
        {
            heroRenderer.material.color = stealthColor;
        } 
        else 
        {
            heroRenderer.material.color = originalColor;
        }
    }

    public override void ApplyRevealEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " (WitchController) has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;
        IsHidden = false; 
        hideTimer = 0f;
        UpdateVisuals(); 
    }

    void HandleRevealState() // Existing method, ensures timer ticks down
    {
        if (isRevealed)
        {
            revealEffectTimer -= Time.deltaTime;
            if (revealEffectTimer <= 0)
            {
                isRevealed = false;
                revealEffectTimer = 0f;
                Debug.Log(gameObject.name + " (WitchController) reveal effect wore off.");
                UpdateVisuals(); 
            }
        }
    }

    public override void ApplyScryEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " (WitchController) is being SCRYED for " + duration + " seconds!");
        isScryed = true;
        scryEffectTimer = duration;
        UpdateVisuals(); 
    }

    void HandleScryState() // Existing method, ensures timer ticks down
    {
        if (isScryed)
        {
            scryEffectTimer -= Time.deltaTime;
            if (scryEffectTimer <= 0)
            {
                isScryed = false;
                scryEffectTimer = 0f;
                Debug.Log(gameObject.name + " (WitchController) scry effect wore off.");
                UpdateVisuals(); 
            }
        }
    }

    public override void ApplyKnockback(Vector3 force, float duration) // Added override
    {
        if (isDefeated) return;
        Debug.Log(gameObject.name + " (WitchController) received knockback.");

        if (duration <= 0) return;
        knockbackVelocity = force; 
        knockbackDuration = duration;
        knockbackTimer = 0f;

        IsHidden = false; 
        hideTimer = 0f;
        
        if (isChanneling)
        {
            StopChanneling(false); // Interrupt channeling
            Debug.Log(gameObject.name + " (WitchController) channeling interrupted by knockback!");
        }
        
        // If there's any cloak-like effect for the Witch, it should be handled here too.
        // For now, just updating visuals.
        UpdateVisuals(); 
    }

    public override void SetDefeated() // Added override
    {
        if (isDefeated) return; // Already defeated

        base.isDefeated = true; // Set flag in base class
        Debug.Log(gameObject.name + " (WitchController) SetDefeated called and has been defeated!");

        if (isChanneling)
        {
            StopChanneling(false); // Stop channeling if defeated
        }

        // Disable components
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        // Disable any other Witch-specific components that should stop on defeat (e.g., spell casting scripts if they were separate)
        
        this.enabled = false; // Disable this script

        // Optional: Change appearance to indicate defeat
        if (heroRenderer != null && heroRenderer.material != null)
        {
            heroRenderer.material.color = Color.gray; // Example: turn gray
        }
        // Optional: Instantiate a defeat effect prefab
        // if (defeatEffectPrefab != null) Instantiate(defeatEffectPrefab, transform.position, Quaternion.identity);
    }
}
