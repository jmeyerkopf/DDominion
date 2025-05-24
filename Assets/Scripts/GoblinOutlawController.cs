using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class GoblinOutlawController : HeroControllerBase // Inherit from HeroControllerBase
{
    public float moveSpeed = 3.0f;
    public bool IsHidden { get; private set; } // isDefeated is now in HeroControllerBase
    public float timeToHide = 2.0f; 
    public string heroSpawnMarkerName = "HeroSpawnPointMarkerGO"; 

    private float hideTimer = 0f;
    private Renderer heroRenderer;
    private Color originalColor;
    private Color stealthColor;
    private Color cloakColor; 
    private float groundLevelY;

    public bool isCloaked = false;
    public float cloakDuration = 5.0f;
    public float cloakCooldown = 20.0f;
    private float cloakTimer = 0f;
    private float cloakCooldownTimer = 0f;

    public float attackDamage = 10.0f;
    public float attackRange = 2.0f;
    public float attackCooldown = 1.0f;
    private float attackTimer = 0f;
    public float attackNoiseRadius = 7.0f; 

    public int goldAmount = 0;
    public float lootRange = 2.0f;
    public int villageLootAmount = 10;
    private float villageLootCooldown = 10.0f; 
    private Dictionary<GameObject, float> villageLastLootTime = new Dictionary<GameObject, float>();
    public float villageLootNoiseRadius = 5.0f; 
    public bool isInteractingWithVillage = false;
    public float currentVillageInteractionTime = 0f;
    public float villageInteractionDuration = 1.5f; 
    public GameObject currentInteractingVillage = null;

    public float tavernInteractRange = 3.0f;
    public GameObject tavernShopPanel; 

    private int attackUpgradeCost = 50;
    private float attackUpgradeAmount = 5f;
    private bool attackPurchased = false;

    private int speedUpgradeCost = 30;
    private float speedUpgradeAmount = 0.5f;
    private bool speedPurchased = false;

    public float detectionLevel = 0f; 
    public float timeToLoseDetection = 1.0f; 
    
    [HideInInspector] 
    public bool isBeingActivelyDetected = false; 

    public GameObject cluePrefab; 
    private float clueDropTimer = 0f;
    public float clueDropInterval = 1.0f; 
    public float clueLifetime = 10.0f;

    private bool isRevealed = false;
    private float revealEffectTimer = 0f;
    public Color revealedColor = Color.yellow; 

    private bool isScryed = false;
    private float scryEffectTimer = 0f;
    public Color scryedColor = Color.cyan;

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
            Debug.LogError(gameObject.name + ": Could not find hero spawn marker named '" + heroSpawnMarkerName + "'. Goblin will not be repositioned.", this);
        }

        heroRenderer = GetComponent<Renderer>();
        if (heroRenderer != null && heroRenderer.material != null)
        {
            originalColor = heroRenderer.material.color;
            stealthColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f); 
            cloakColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0.1f); 
        }
        else
        {
            Debug.LogError("GoblinOutlawController: Renderer or Material not found on Goblin GameObject!");
        }

        groundLevelY = transform.position.y; 
        IsHidden = false; 

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError(gameObject.name + ": CharacterController component not found! Adding one.");
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 2.0f;
            characterController.radius = 0.5f;
            characterController.center = new Vector3(0, 1.0f, 0); 
        }

        if (tavernShopPanel != null && tavernShopPanel.activeSelf)
        {
            tavernShopPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (isDefeated) return; // Stop updates if defeated

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
            hideTimer = 0f; // Reset hide timer when moving
            if(IsHidden) IsHidden = false; // Break stealth on move

            if (isInteractingWithVillage)
            {
                Debug.Log("Goblin moved, stopping village interaction.");
                StopVillageInteraction();
            }

            if (!isCloaked && !isRevealed) 
            {
                clueDropTimer -= Time.deltaTime;
                if (clueDropTimer <= 0f)
                {
                    if (cluePrefab != null)
                    {
                        Vector3 clueSpawnPos = transform.position - transform.forward * 0.5f + Vector3.up * 0.1f; 
                        GameObject clueInstance = Instantiate(cluePrefab, clueSpawnPos, Quaternion.identity);
                        ClueObject clueScript = clueInstance.GetComponent<ClueObject>();
                        if (clueScript != null)
                        {
                            clueScript.lifetime = this.clueLifetime; 
                        }
                    }
                    clueDropTimer = clueDropInterval;
                }
            }
        }
        else 
        {
            if (!IsHidden && !isCloaked && !isRevealed) 
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= timeToHide)
                {
                    IsHidden = true;
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
            IsHidden = false; 
            if (isCloaked) 
            {
                isCloaked = false;
                cloakTimer = 0f; 
            }

            GameObject[] scouts = GameObject.FindGameObjectsWithTag("Scout");
            GameObject[] tanks = GameObject.FindGameObjectsWithTag("Tank");
            List<GameObject> allMinions = new List<GameObject>();
            allMinions.AddRange(scouts);
            allMinions.AddRange(tanks);

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
                        IsHidden = false; 
                        if (isCloaked) { isCloaked = false; cloakTimer = 0f; }
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
                            IsHidden = false; 
                            if (isCloaked) { isCloaked = false; cloakTimer = 0f; }
                            Debug.Log("Looted " + villageGO.name + " for " + villageLootAmount + " gold. Total gold: " + goldAmount + ". Noise made.");
                            isInteractingWithVillage = true;
                            currentVillageInteractionTime = 0f;
                            currentInteractingVillage = villageGO;
                            Debug.Log("Goblin started interacting with village: " + villageGO.name);
                            interactedThisPress = true;
                            break; 
                        }
                    }
                }
            }
        }
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
            if (currentInteractingVillage == null || 
                Vector3.Distance(transform.position, currentInteractingVillage.transform.position) > lootRange + 0.5f || 
                currentVillageInteractionTime >= villageInteractionDuration) 
            {
                StopVillageInteraction();
            }
        }
    }

    void StopVillageInteraction()
    {
        if (isInteractingWithVillage) 
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

    void HandleCloakInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && cloakCooldownTimer <= 0 && !isCloaked && !isRevealed) 
        {
            isCloaked = true;
            cloakTimer = cloakDuration;
            cloakCooldownTimer = cloakCooldown;
            hideTimer = 0f; 
            IsHidden = false; 
            Debug.Log("Cloak activated!");
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
                Vector3 movementInput = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                if (movementInput.magnitude < 0.01f && !isRevealed) // Check if should try to hide
                {
                    hideTimer = 0f; 
                }
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

    public override void ApplyRevealEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;
        IsHidden = false;
        isCloaked = false;
        hideTimer = 0f;
        cloakTimer = 0f; 
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

    public override void ApplyScryEffect(float duration) // Added override
    {
        Debug.Log(gameObject.name + " is being SCRYED for " + duration + " seconds!");
        isScryed = true;
        scryEffectTimer = duration;
        UpdateVisuals(); 
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

    public override void ApplyKnockback(Vector3 force, float duration) // Added override
    {
        if (duration <= 0) return;
        knockbackVelocity = force; 
        knockbackDuration = duration;
        knockbackTimer = 0f;
        IsHidden = false; 
        if (isCloaked) isCloaked = false; 
        hideTimer = 0f;
        cloakTimer = 0f;
        Debug.Log(gameObject.name + " is knocked back with velocity " + force + " for " + duration + "s!");
        UpdateVisuals(); 
    }

    public override void SetDefeated()
    {
        if (base.isDefeated) return; 
        base.isDefeated = true; 
        Debug.Log(gameObject.name + " (GoblinOutlawController) has been defeated via SetDefeated!");
        if (characterController != null) characterController.enabled = false;
        this.enabled = false; 
    }
}
