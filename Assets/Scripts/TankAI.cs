using UnityEngine;

public class TankAI : MonoBehaviour
{
    public float moveSpeed = 1.0f;
    public float detectionRadius = 6.0f; 
    public float attackDamage = 15.0f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2.0f;
    // public Transform heroTransform; // Using heroTransformInternal

    private float attackTimer = 0f;
    private Vector3 wanderTarget;
    private float wanderRadius = 5f; 
    private float waypointReachedThreshold = 0.5f;
    private Health heroHealth; 
    private HeroController heroController; 
    private float groundLevelY;
    private Transform heroTransformInternal;

    // Detection & Engagement
    private float heroSpottedTime = 0f;
    public float timeToActuallyDetectHero = 2.0f; // Time hero must be continuously "visible" to be detected
    private bool isActivelyEngaging = false; // True when detected and moving to attack

    // Noise Investigation
    private Vector3 investigationTarget = Vector3.zero;
    private bool investigatingNoise = false;
    public float noiseInvestigationRadiusMultiplier = 1.0f; 


    void Start()
    {
        groundLevelY = transform.position.y; 

        GameObject heroObject = GameObject.FindGameObjectWithTag("Hero");
        if (heroObject != null)
        {
            heroTransformInternal = heroObject.transform;
            heroHealth = heroObject.GetComponent<Health>();
            heroController = heroObject.GetComponent<HeroController>();

            if (heroHealth == null) Debug.LogError("TankAI: Hero Health component not found!");
            if (heroController == null) Debug.LogError("TankAI: HeroController component not found on Hero!");
        }
        else
        {
            Debug.LogError("TankAI: Hero GameObject not found! Tank AI will not function correctly.");
        }
        
        SetNewWanderTarget();
    }

    void SetNewWanderTarget()
    {
        float randomX = Random.Range(-wanderRadius, wanderRadius);
        float randomZ = Random.Range(-wanderRadius, wanderRadius);
        wanderTarget = new Vector3(transform.position.x + randomX, groundLevelY, transform.position.z + randomZ);
    }

    void Update()
    {
        if (heroController == null || heroTransformInternal == null || !heroTransformInternal.gameObject.activeInHierarchy)
        {
            if (!investigatingNoise) MoveToWanderPoint(); 
            else if(investigatingNoise) 
            {
                investigatingNoise = false; // Stop investigating if hero disappears
                SetNewWanderTarget();
            }
            return;
        }

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        bool isHeroPotentiallyVisible = false;
        if (heroHealth.GetCurrentHealth() > 0 && 
            !heroController.IsHidden && 
            !heroController.isCloaked &&
            Vector3.Distance(transform.position, heroTransformInternal.position) <= detectionRadius)
        {
            isHeroPotentiallyVisible = true;
        }

        if (isHeroPotentiallyVisible)
        {
            // Priority 1: Check if hero is interacting with a village (if visible)
            if (heroController.isInteractingWithVillage && 
                heroController.currentInteractingVillage != null && 
                heroController.currentVillageInteractionTime > 1.0f &&
                !isActivelyEngaging) // Don't override active engagement for this, but can start investigating
            {
                Debug.Log(gameObject.name + " (Tank) saw hero interacting with " + heroController.currentInteractingVillage.name + " for " + heroController.currentVillageInteractionTime + "s. Investigating village.");
                investigationTarget = heroController.currentInteractingVillage.transform.position;
                investigatingNoise = true; // Re-use state to move towards the village
                isActivelyEngaging = false; // Not engaging hero directly, but investigating village
                heroSpottedTime = 0f; // Reset direct hero spotting time as focus is now village
                // This path makes the tank go to the village.
            }
            // Priority 2: Normal detection and engagement logic if not investigating village
            else if (!investigatingNoise) // if investigatingNoise is true from above, skip this else if.
            {
                heroSpottedTime += Time.deltaTime;

                if (heroSpottedTime >= timeToActuallyDetectHero)
                {
                    isActivelyEngaging = true;
                    investigatingNoise = false; // Full detection overrides other investigations

                    Vector3 directionToHero = (heroTransformInternal.position - transform.position).normalized;
                directionToHero.y = 0;
                transform.Translate(directionToHero * moveSpeed * Time.deltaTime, Space.World);
                transform.LookAt(new Vector3(heroTransformInternal.position.x, transform.position.y, heroTransformInternal.position.z));

                if (Vector3.Distance(transform.position, heroTransformInternal.position) <= attackRange && attackTimer <= 0)
                {
                    heroHealth.TakeDamage(attackDamage);
                    attackTimer = attackCooldown;
                }
            }
            else
                else
                {
                    // Hero is "seen" (within radius, not hidden/cloaked) but not yet "detected" for full engagement
                    isActivelyEngaging = false;
                    // Optionally, make the Tank look at the hero
                    Vector3 directionToHero = (heroTransformInternal.position - transform.position).normalized;
                    directionToHero.y = 0;
                    if (directionToHero != Vector3.zero) transform.rotation = Quaternion.LookRotation(directionToHero);
                    // Don't reset investigatingNoise here if it was set due to village interaction.
                }
            }
        }
        else // Hero is not potentially visible (out of range, hidden, or cloaked)
        {
            heroSpottedTime = 0f; // Reset direct hero spotting time
            // If was actively engaging but lost sight, stop engaging.
            // Keep investigatingNoise if it was set for village or alert.
            if (isActivelyEngaging) 
            {
                 isActivelyEngaging = false;
            }

            // Noise investigation or wandering (only if not already investigating something e.g. a village)
            if (!investigatingNoise)
            {
                Vector3 noisePos;
                float noiseRad;
                if (NoiseManager.GetLatestNoise(out noisePos, out noiseRad))
                {
                    if (Vector3.Distance(transform.position, noisePos) <= noiseRad * noiseInvestigationRadiusMultiplier)
                    {
                        investigationTarget = noisePos;
                        investigatingNoise = true; // Investigate general noise
                    }
                }
            }
            
            // This block handles movement towards an investigation target (could be noise, alert, or village)
            if (investigatingNoise)
            {
                Vector3 directionToTarget = (investigationTarget - transform.position).normalized;
                directionToTarget.y = 0;
                transform.Translate(directionToTarget * moveSpeed * Time.deltaTime, Space.World);
                if (directionToTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(directionToTarget);

                if (Vector3.Distance(transform.position, investigationTarget) < 1.0f)
                {
                    investigatingNoise = false; // Reached the investigation target
                    SetNewWanderTarget(); // After investigating, pick a new wander target
                }
            }
            // If not engaging, not investigating, then wander.
            else if(!isActivelyEngaging) // Ensure not to wander if was engaging but just lost sight
            {
                MoveToWanderPoint();
            }
        }

        Vector3 currentPosition = transform.position;
        currentPosition.y = groundLevelY;
        transform.position = currentPosition;
    }

    void MoveToWanderPoint()
    {
        Vector3 directionToWanderTarget = (wanderTarget - transform.position).normalized;
        directionToWanderTarget.y = 0; 
        transform.Translate(directionToWanderTarget * moveSpeed * Time.deltaTime, Space.World);
        if(directionToWanderTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(directionToWanderTarget);

        if (Vector3.Distance(transform.position, wanderTarget) <= waypointReachedThreshold)
        {
            SetNewWanderTarget();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue; 
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.red; 
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (investigatingNoise)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, investigationTarget);
            Gizmos.DrawWireSphere(investigationTarget, 0.5f);
        }
        else if (heroTransformInternal != null && heroController != null)
        {
            bool isHeroPotentiallyVisibleCheck = !heroController.IsHidden && !heroController.isCloaked &&
                                             Vector3.Distance(transform.position, heroTransformInternal.position) <= detectionRadius;

            if (heroController.isCloaked && Vector3.Distance(transform.position, heroTransformInternal.position) <= detectionRadius)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f); // Purple for cloaked hero in range
                Gizmos.DrawLine(transform.position, heroTransformInternal.position);
            }
            else if (isActivelyEngaging)
            {
                Gizmos.color = Color.magenta; // Actively engaging
                Gizmos.DrawLine(transform.position, heroTransformInternal.position);
            }
            else if (isHeroPotentiallyVisibleCheck && heroSpottedTime > 0)
            {
                Gizmos.color = Color.yellow; // Spotted but not yet engaging
                Gizmos.DrawLine(transform.position, heroTransformInternal.position);
            }
            else if (!isActivelyEngaging) // Default wander line
            {
                Gizmos.color = Color.green;
                if(wanderTarget != null && wanderTarget != Vector3.zero) Gizmos.DrawLine(transform.position, wanderTarget);
            }
        }
        else // Fallback if no heroController or heroTransformInternal
        {
            Gizmos.color = Color.green; 
            if(wanderTarget != null && wanderTarget != Vector3.zero) Gizmos.DrawLine(transform.position, wanderTarget);
        }
    }

    public void ReceiveAlert(Vector3 sourcePosition, Vector3 heroLastKnownPosition)
    {
        // Only respond if not already actively engaging or investigating a different noise/alert.
        // Also, ensure heroController and heroTransformInternal are not null.
        if (heroController == null || heroTransformInternal == null) return;

        if (!isActivelyEngaging && !investigatingNoise)
        {
            // Avoid investigating if already very close to the target position of the alert.
            if (Vector3.Distance(transform.position, heroLastKnownPosition) > 1.0f)
            {
                Debug.Log(gameObject.name + " (Tank) received alert from " + sourcePosition + ". Investigating hero at " + heroLastKnownPosition);
                investigationTarget = heroLastKnownPosition;
                investigatingNoise = true; // Use existing state for simplicity
                // isActivelyEngaging will remain false until direct detection conditions are met
                // heroSpottedTime is not reset here, as this is an indirect alert.
            }
        }
        else
        {
            Debug.Log(gameObject.name + " (Tank) received alert but is already busy (engaging: " + isActivelyEngaging + ", investigating: " + investigatingNoise + ")");
        }
    }
}
