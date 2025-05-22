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
    private float groundLevelY;

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

        HandleMovementAndStealth();
        HandleAttack();
        HandleInteraction(); 
        HandleDetectionLevelDecay(); 
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

            if (IsHidden)
            {
                IsHidden = false;
                if (heroRenderer != null) heroRenderer.material.color = originalColor;
            }
            hideTimer = 0f; 
        }
        else 
        {
            if (!IsHidden)
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= timeToHide)
                {
                    IsHidden = true;
                    if (heroRenderer != null) heroRenderer.material.color = stealthColor;
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
                            Debug.Log("Looted " + villageGO.name + " for " + villageLootAmount + " gold. Total gold: " + goldAmount + ". Noise made.");
                            interactedThisPress = true;
                            break; 
                        }
                    }
                }
            }
        }
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
}
