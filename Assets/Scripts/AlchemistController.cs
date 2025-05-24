using UnityEngine;
using System.Collections.Generic; // Required for Dictionary

public class AlchemistController : MonoBehaviour
{
    // Alchemist Stats
    public float moveSpeed = 3.0f;
    public bool IsHidden { get; private set; }
    public float timeToHide = 3.0f; 
    public string heroSpawnMarkerName = "HeroSpawnPointMarkerGO"; 

    private float hideTimer = 0f;
    private Renderer heroRenderer;
    private Color originalColor;
    private Color stealthColor; 
    private float groundLevelY;

    // Attack properties (weak self-defense)
    public float attackDamage = 8.0f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.8f;
    private float attackTimer = 0f;
    public float attackNoiseRadius = 6.0f; 

    // Ingredient Collection
    public float interactionRange = 2.0f; 
    public List<IngredientType> collectedIngredients = new List<IngredientType>();

    // Village Interaction (Kept for compatibility, but Alchemist doesn't use it for looting/tavern)
    // This can be removed if no other system (like Priest AI targeting "hero interacting with village") needs it.
    // For now, it's harmless but mostly unused by Alchemist's core loop.
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

    // Scry Effect (standard for any hero-like character)
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
            Debug.LogError(gameObject.name + ": Could not find hero spawn marker named '" + heroSpawnMarkerName + "'. Alchemist will not be repositioned.", this);
        }

        heroRenderer = GetComponent<Renderer>();
        if (heroRenderer != null && heroRenderer.material != null)
        {
            originalColor = heroRenderer.material.color;
            stealthColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f); 
        }
        else
        {
            Debug.LogError(gameObject.name + ": Renderer or Material not found on Alchemist GameObject!");
        }

        groundLevelY = transform.position.y; 
        IsHidden = false; 
    }

    void Update()
    {
        HandleMovementAndStealth();
        HandleAttack();
        HandleInteraction(); 
        // HandleVillageInteractionState(); // This can be commented out if Alchemist has no specific timed village interaction
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

            if (IsHidden) // If was hidden, break stealth
            {
                IsHidden = false;
                 Debug.Log("Alchemist moved, stealth broken.");
            }
            hideTimer = 0f;

            if (isInteractingWithVillage) // If Alchemist has some form of village interaction that movement breaks
            {
                Debug.Log("Alchemist moved, stopping village interaction.");
                StopVillageInteraction();
            }
        }
        else // Standing still
        {
            // Only try to hide if not revealed and not already hidden
            if (!isRevealed && !isScryed && !IsHidden) 
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= timeToHide)
                {
                    IsHidden = true;
                    Debug.Log("Alchemist is now hidden.");
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
            if (IsHidden)
            {
                IsHidden = false; // Break stealth on attack
                Debug.Log("Alchemist attacked, stealth broken.");
                hideTimer = 0f;
            }

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
                        minionHealth.TakeDamage(attackDamage); // Alchemist does not use Valor system
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
            bool interactionOccurred = false; 

            // Ingredient Collection Logic
            GameObject[] sources = GameObject.FindGameObjectsWithTag("IngredientSource");
            GameObject closestSource = null;
            float minDistance = Mathf.Infinity;

            foreach (GameObject sourceGO in sources)
            {
                if (!sourceGO.activeInHierarchy) continue;
                float distanceToSource = Vector3.Distance(transform.position, sourceGO.transform.position);
                if (distanceToSource < minDistance)
                {
                    minDistance = distanceToSource;
                    closestSource = sourceGO;
                }
            }

            if (closestSource != null && minDistance <= interactionRange)
            {
                IngredientSource ingredientComp = closestSource.GetComponent<IngredientSource>();
                if (ingredientComp != null)
                {
                    for (int i = 0; i < ingredientComp.quantity; i++)
                    {
                        collectedIngredients.Add(ingredientComp.type);
                    }
                    Debug.Log("Alchemist collected " + ingredientComp.quantity + " of " + ingredientComp.type + 
                              " from " + ingredientComp.ingredientSourceName + 
                              ". Total " + ingredientComp.type + " collected: " + collectedIngredients.FindAll(ing => ing == ingredientComp.type).Count);
                    
                    NoiseManager.MakeNoise(transform.position, 5.0f); 

                    Destroy(closestSource); 
                    
                    if (IsHidden) // Break stealth on interaction
                    {
                        IsHidden = false;
                        Debug.Log("Alchemist collected ingredient, stealth broken.");
                        hideTimer = 0f;
                    }
                    interactionOccurred = true; 
                }
            }

            // Village Interaction (only if no ingredient was collected and if this logic is intended for Alchemist)
            // This part is optional and depends on whether Alchemist has a non-looting interaction with villages.
            if (!interactionOccurred && currentInteractingVillage == null) 
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
                        if (IsHidden) 
                        {
                            IsHidden = false; 
                            Debug.Log("Alchemist interacted with village, stealth broken.");
                            hideTimer = 0f;
                        }
                        Debug.Log("Alchemist started interacting with village: " + villageGO.name + " (No looting)");
                        interactionOccurred = true; 
                        break; 
                    }
                }
            }
        }

        // If E is released and was interacting with a village, stop.
        if (Input.GetKeyUp(KeyCode.E) && isInteractingWithVillage)
        {
            StopVillageInteraction();
        }
    }

    // This method may not be needed if Alchemist's village interaction is instantaneous or doesn't have a timed duration.
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
            Debug.Log("Alchemist stopping interaction with village: " + (currentInteractingVillage != null ? currentInteractingVillage.name : "N/A"));
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

    public void ApplyRevealEffect(float duration)
    {
        Debug.Log(gameObject.name + " has been REVEALED for " + duration + " seconds!");
        isRevealed = true;
        revealEffectTimer = duration;
        IsHidden = false; 
        hideTimer = 0f;
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
}
