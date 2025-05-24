using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class HeroController : MonoBehaviour
{
    public float moveSpeed = 3.0f;
    public bool IsHidden { get; private set; }
    public float timeToHide = 2.0f; 
    public string heroSpawnMarkerName = "HeroSpawnPointMarkerGO"; // Updated marker name

    private float hideTimer = 0f;
    private Renderer heroRenderer;
    private Color originalColor;
    private Color stealthColor;
    private Color cloakColor; // For cloak ability
    private float groundLevelY;

    // Cloak Ability properties
    public bool isCloaked = false;
    public float cloakDuration = 5.0f;
    public float cloakCooldown = 20.0f;
    private float cloakTimer = 0f;
    private float cloakCooldownTimer = 0f;

    // Attack properties
    public float attackDamage = 10.0f;
    public float attackRange = 2.0f;
    public float attackCooldown = 1.0f;
    private float attackTimer = 0f;
    public float attackNoiseRadius = 7.0f; 

    // Gold and Looting properties
    public int goldAmount = 0;
    public float lootRange = 2.0f;
    public int villageLootAmount = 10;
    private float villageLootCooldown = 10.0f; 
    private Dictionary<GameObject, float> villageLastLootTime = new Dictionary<GameObject, float>();
    public float villageLootNoiseRadius = 5.0f; 
    public bool isInteractingWithVillage = false;
    public float currentVillageInteractionTime = 0f;
    public float villageInteractionDuration = 1.5f; // How long the "interacting" flag stays true for minions to see
    public GameObject currentInteractingVillage = null;

    // Tavern Interaction & Upgrades
    public float tavernInteractRange = 3.0f;
    public GameObject tavernShopPanel; 

    private int attackUpgradeCost = 50;
    private float attackUpgradeAmount = 5f;
    private bool attackPurchased = false;

    private int speedUpgradeCost = 30;
    private float speedUpgradeAmount = 0.5f;
    private bool speedPurchased = false;

    // Detection Level
    public float detectionLevel = 0f; 
    public float timeToLoseDetection = 1.0f; 
    
    [HideInInspector] 
    public bool isBeingActivelyDetected = false; 

    // Clue Dropping
    public GameObject cluePrefab; 
    private float clueDropTimer = 0f;
    public float clueDropInterval = 1.0f; 
    public float clueLifetime = 10.0f;

    // Reveal Effect
    private bool isRevealed = false;
    private float revealEffectTimer = 0f;
    public Color revealedColor = Color.yellow; // Example color for reveal

    // Scry Effect
    private bool isScryed = false;
    private float scryEffectTimer = 0f;
    public Color scryedColor = Color.cyan;

    void Start()
    {
        // Reposition based on marker
        GameObject marker = GameObject.Find(heroSpawnMarkerName);
        if (marker != null)
        {
            transform.position = marker.transform.position;
            // Debug.Log(gameObject.name + " repositioned to marker: " + heroSpawnMarkerName + " at " + transform.position, this);
        }
        else
        {
            Debug.LogError(gameObject.name + ": Could not find hero spawn marker named '" + heroSpawnMarkerName + "'. Hero will not be repositioned.", this);
        }

        heroRenderer = GetComponent<Renderer>();
        if (heroRenderer != null && heroRenderer.material != null)
        {
            originalColor = heroRenderer.material.color;
            stealthColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f); 
            cloakColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0.1f); // Near invisibility for cloak
        }
        else
        {
            Debug.LogError("HeroController: Renderer or Material not found on Hero GameObject!");
        }

        groundLevelY = transform.position.y; 
        IsHidden = false; 

        if (tavernShopPanel != null && tavernShopPanel.activeSelf)
        {
            tavernShopPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (tavernShopPanel != null && tavernShopPanel.activeSelf)
        {
            return; 
        }

        HandleCloakInput(); 
        HandleMovementAndStealth();
        HandleAttack();
        HandleInteraction(); 
        HandleVillageInteractionState(); 
        HandleDetectionLevelDecay(); 
        UpdateCloakTimers(); 
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


    void HandleMovementAndStealth()
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

            // IsHidden no longer set to false when moving.
            // Visuals will be handled by UpdateVisuals()
            hideTimer = 0f;

            if (isInteractingWithVillage)
            {
                Debug.Log("Hero moved, stopping village interaction.");
                StopVillageInteraction();
            }

            // Handle Clue Dropping
            if (!isCloaked) // Do not drop clues if fully cloaked
            {
                clueDropTimer -= Time.deltaTime;
                if (clueDropTimer <= 0f)
                {
                    if (cluePrefab != null)
                    {
                        GameObject clueInstance = Instantiate(cluePrefab, transform.position, Quaternion.identity);
                        ClueObject clueScript = clueInstance.GetComponent<ClueObject>();
                        if (clueScript != null)
                        {
                            clueScript.lifetime = this.clueLifetime; // Pass hero's defined lifetime to the clue
                        }
                        Debug.Log("Hero dropped a clue at " + transform.position);
                    }
                    clueDropTimer = clueDropInterval;
                }
            }
        }
        else 
        {
            if (!IsHidden && !isCloaked && !isRevealed) // Can only hide if not revealed
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= timeToHide)
                {
                    IsHidden = true;
                    // Visuals will be handled by UpdateVisuals()
                }
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
            IsHidden = false; // Break stealth on attack
            if (isCloaked) // Break cloak on attack
            {
                isCloaked = false;
                cloakTimer = 0f; 
                // Visuals will be handled by UpdateVisuals()
            }

            GameObject[] scouts = GameObject.FindGameObjectsWithTag("Scout");
            GameObject[] tanks = GameObject.FindGameObjectsWithTag("Tank");
            List<GameObject> allMinions = new List<GameObject>();
            allMinions.AddRange(scouts);
            allMinions.AddRange(tanks);

            bool hitMinion = false;

            foreach (GameObject minionGO in allMinions)
            {
                if (!minionGO.activeInHierarchy) continue; 

                float distanceToMinion = Vector3.Distance(transform.position, minionGO.transform.position);
                if (distanceToMinion <= attackRange)
                {
                    Health minionHealth = minionGO.GetComponent<Health>();
                    if (minionHealth != null)
                    {
                        minionHealth.TakeDamage(attackDamage);
                        hitMinion = true; 
                        break; 
                    }
                }
            }
        }
    }

    void HandleInteraction() 
    {
        if (Input.GetKeyDown(KeyCode.E)) 
        {
            bool interactedThisPress = false;

            if (tavernShopPanel != null && !tavernShopPanel.activeSelf)
            {
                GameObject[] taverns = GameObject.FindGameObjectsWithTag("HiddenTavern");
                foreach (GameObject tavernGO in taverns)
                {
                    if (!tavernGO.activeInHierarchy) continue;
                    float distanceToTavern = Vector3.Distance(transform.position, tavernGO.transform.position);
                    if (distanceToTavern <= tavernInteractRange)
                    {
                        tavernShopPanel.SetActive(true);
                        IsHidden = false; // Break stealth on tavern interaction
                        if (isCloaked) // Break cloak on tavern interaction
                        {
                            isCloaked = false;
                            cloakTimer = 0f;
                        }
                        // Visuals will be handled by UpdateVisuals()
                        Debug.Log("Opened Tavern Shop Panel.");
                        interactedThisPress = true;
                        break; 
                    }
                }
            }

            if (!interactedThisPress)
            {
                GameObject[] villages = GameObject.FindGameObjectsWithTag("Village");
                foreach (GameObject villageGO in villages)
                {
                    if (!villageGO.activeInHierarchy) continue;

                    float distanceToVillage = Vector3.Distance(transform.position, villageGO.transform.position);
                    if (distanceToVillage <= lootRange)
                    {
                        bool canLootThisVillage = true;
                        if (villageLastLootTime.ContainsKey(villageGO))
                        {
                            if (Time.time < villageLastLootTime[villageGO] + villageLootCooldown)
                            {
                                canLootThisVillage = false;
                            }
                        }

                        if (canLootThisVillage)
                        {
                            goldAmount += villageLootAmount;
                            villageLastLootTime[villageGO] = Time.time; 
                            NoiseManager.MakeNoise(transform.position, villageLootNoiseRadius); 
                            IsHidden = false; // Break stealth on looting
                            if (isCloaked) // Break cloak on looting
                            {
                                isCloaked = false;
                                cloakTimer = 0f;
                            }
                            // Visuals will be handled by UpdateVisuals()
                            Debug.Log("Looted " + villageGO.name + " for " + villageLootAmount + " gold. Total gold: " + goldAmount + ". Noise made.");
                            
                            // Start interaction state for minion detection
                            isInteractingWithVillage = true;
                            currentVillageInteractionTime = 0f;
                            currentInteractingVillage = villageGO;
                            Debug.Log("Hero started interacting with village: " + villageGO.name);

                            interactedThisPress = true;
                            break; 
                        }
                    }
                }
            }
        }
        // If E is released and was interacting, stop.
        // This is a simplified check. A more robust solution might involve tracking E press/release more explicitly
        // if complex interactions beyond the timed duration are needed.
        if (Input.GetKeyUp(KeyCode.E) && isInteractingWithVillage)
        {
            StopVillageInteraction();
        }
    }

    void HandleVillageInteractionState()
    {
        if (isInteractingWithVillage)
        {
            currentVillageInteractionTime += Time.deltaTime;

            // Check conditions to stop interaction
            if (currentInteractingVillage == null || // Village somehow got destroyed/nulled
                Vector3.Distance(transform.position, currentInteractingVillage.transform.position) > lootRange + 0.5f || // Moved too far (added buffer)
                currentVillageInteractionTime >= villageInteractionDuration) 
            {
                if (currentVillageInteractionTime >= villageInteractionDuration) Debug.Log("Village interaction timed out.");
                else if (currentInteractingVillage == null) Debug.Log("Interacting village became null.");
                else Debug.Log("Moved too far from village during interaction.");
                StopVillageInteraction();
            }
            // Note: Movement check is primarily in HandleMovementAndStealth for immediate feedback.
            // 'E' key release check is in HandleInteraction.
        }
    }

    void StopVillageInteraction()
    {
        if (isInteractingWithVillage) // Ensure we only log/reset if actually interacting
        {
            Debug.Log("Stopping interaction with village: " + (currentInteractingVillage != null ? currentInteractingVillage.name : "N/A"));
        }
        isInteractingWithVillage = false;
        currentVillageInteractionTime = 0f;
        currentInteractingVillage = null;
    }

    public void PurchaseAttackUpgrade()
    {
        if (attackPurchased) { Debug.Log("Attack upgrade already purchased."); return; }
        if (goldAmount >= attackUpgradeCost)
        {
            goldAmount -= attackUpgradeCost;
            attackDamage += attackUpgradeAmount;
            attackPurchased = true;
            Debug.Log("Attack Upgraded! New Attack Damage: " + attackDamage + ". Gold remaining: " + goldAmount);
        }
        else { Debug.Log("Not enough gold for attack upgrade. Need " + attackUpgradeCost); }
    }

    public void PurchaseSpeedUpgrade()
    {
        if (speedPurchased) { Debug.Log("Speed upgrade already purchased."); return; }
        if (goldAmount >= speedUpgradeCost)
        {
            goldAmount -= speedUpgradeCost;
            moveSpeed += speedUpgradeAmount;
            speedPurchased = true;
            Debug.Log("Speed Upgraded! New Move Speed: " + moveSpeed + ". Gold remaining: " + goldAmount);
        }
        else { Debug.Log("Not enough gold for speed upgrade. Need " + speedUpgradeCost); }
    }

    public void CloseShopPanel()
    {
        if (tavernShopPanel != null) { tavernShopPanel.SetActive(false); Debug.Log("Closed Tavern Shop Panel."); }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("GoldNugget"))
        {
            goldAmount++;
            Debug.Log("Picked up gold nugget. Total gold: " + goldAmount);
            Destroy(other.gameObject);
        }
    }
    
    void OnDisable()
    {
        if (heroRenderer != null && heroRenderer.material != null) { heroRenderer.material.color = originalColor; }
    }

    // New methods for Cloak and Visuals

    void HandleCloakInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && cloakCooldownTimer <= 0 && !isCloaked && !isRevealed) // Cannot cloak if revealed
        {
            isCloaked = true;
            cloakTimer = cloakDuration;
            cloakCooldownTimer = cloakCooldown;
            hideTimer = 0f; // Reset hide timer as cloak is a more powerful stealth
            IsHidden = false; // Cloak overrides normal stealth initially
            Debug.Log("Cloak activated!");
            // Visuals will be handled by UpdateVisuals()
        }
    }

    void UpdateCloakTimers()
    {
        if (isCloaked)
        {
            cloakTimer -= Time.deltaTime;
            if (cloakTimer <= 0)
            {
                isCloaked = false;
                cloakTimer = 0f;
                Debug.Log("Cloak ended.");
                // Check if hero should immediately go into IsHidden state
                float horizontalInput = Input.GetAxis("Horizontal");
                float verticalInput = Input.GetAxis("Vertical");
                Vector3 movement = new Vector3(horizontalInput, 0, verticalInput);
                if (movement.magnitude < 0.01f)
                {
                    // If not moving, start the hide timer for normal stealth
                    hideTimer = 0f; // Reset to begin counting for IsHidden
                }
                // Visuals will be handled by UpdateVisuals()
            }
        }

        if (cloakCooldownTimer > 0)
        {
            cloakCooldownTimer -= Time.deltaTime;
            if (cloakCooldownTimer < 0) cloakCooldownTimer = 0;
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
        else if (isCloaked) 
        {
            heroRenderer.material.color = cloakColor; 
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

    public void ApplyRevealEffect(float duration)
    {
        Debug.Log(gameObject.name + " has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;

        // Break existing stealth/cloak
        IsHidden = false;
        isCloaked = false;
        hideTimer = 0f;
        cloakTimer = 0f; 
        // Cloak cooldown is not reset by reveal, only its active state.

        UpdateVisuals(); // Immediately update to revealed color
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
                UpdateVisuals(); // Revert to normal/stealth/cloak color based on current state
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
