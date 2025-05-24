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
    // Removed single hero references: heroHealth, heroControllerBase, goblinHeroController, heroTransformInternal
    private float groundLevelY;

    // Current target variables
    private Transform currentTargetTransform;
    private Health currentTargetHealth;
    private HeroControllerBase currentTargetHeroController;
    private GoblinOutlawController currentTargetGoblinController; // For specific checks

    // Detection & Engagement
    private float heroSpottedTime = 0f; // Applies to currentTargetTransform
    public float timeToActuallyDetectHero = 2.0f; 
    private bool isActivelyEngaging = false; 

    // Noise Investigation
    private Vector3 investigationTarget = Vector3.zero;
    private bool investigatingNoise = false;
    public float noiseInvestigationRadiusMultiplier = 1.0f; 

    // Stun state
    private bool isStunned = false;
    private float stunTimer = 0f;

    void Start()
    {
        groundLevelY = transform.position.y; 
        // Hero target acquisition removed from Start()
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
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                Debug.Log(gameObject.name + " is no longer stunned.");
                ResetCurrentTarget(); // Clear target on stun recovery
            }
            return; 
        }

        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        GameObject[] heroObjects = GameObject.FindGameObjectsWithTag("HeroPlayer");
        GameObject bestHeroToEngage = null;
        float closestHeroDistSqr = detectionRadius * detectionRadius + 1.0f; // Start beyond detection radius

        foreach (GameObject heroGO in heroObjects)
        {
            if (!heroGO.activeInHierarchy) continue;

            Health CHealth = heroGO.GetComponent<Health>();
            if (CHealth == null || CHealth.GetCurrentHealth() <= 0) continue;

            HeroControllerBase CHeroController = heroGO.GetComponent<HeroControllerBase>();
            if (CHeroController == null) continue;
            
            bool isStealthed = false;
            GoblinOutlawController CGoblinController = heroGO.GetComponent<GoblinOutlawController>();
            if (CGoblinController != null)
            {
                isStealthed = CGoblinController.isCloaked || CGoblinController.IsHidden;
            }
            else
            {
                AlchemistController ac = CHeroController as AlchemistController;
                if (ac != null) isStealthed = ac.IsHidden;
                else
                {
                    WitchController wc = CHeroController as WitchController;
                    if (wc != null) isStealthed = wc.IsHidden;
                }
            }

            if (isStealthed) continue;

            float distSqr = (heroGO.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= detectionRadius * detectionRadius)
            {
                if (distSqr < closestHeroDistSqr) // Prioritize closest
                {
                    closestHeroDistSqr = distSqr;
                    bestHeroToEngage = heroGO;
                }
            }
        }

        // Update current target if a new best target is found or if current target is lost
        if (bestHeroToEngage != null)
        {
            if (currentTargetTransform == null || currentTargetTransform != bestHeroToEngage.transform)
            {
                Debug.Log(gameObject.name + " (Tank) new focus: " + bestHeroToEngage.name);
                currentTargetTransform = bestHeroToEngage.transform;
                currentTargetHealth = bestHeroToEngage.GetComponent<Health>();
                currentTargetHeroController = bestHeroToEngage.GetComponent<HeroControllerBase>();
                currentTargetGoblinController = bestHeroToEngage.GetComponent<GoblinOutlawController>();
                heroSpottedTime = 0f; // Reset spotted time for new target
                isActivelyEngaging = false; // Not yet engaging, need to pass timeToActuallyDetectHero
            }
        }
        else // No hero is currently a valid target (none in range or all stealthed)
        {
            if (currentTargetTransform != null) // If Tank was targeting someone
            {
                 Debug.Log(gameObject.name + " (Tank) lost target: " + currentTargetTransform.name);
                 ResetCurrentTarget();
            }
        }


        if (currentTargetTransform != null) // If we have a focused target
        {
            // Village interaction check (specific to Goblin for now)
            bool isGoblinInteractingWithVillage = false;
            GameObject currentVillageForGoblin = null;
            float goblinVillageInteractionTime = 0f;

            if (currentTargetGoblinController != null)
            {
                isGoblinInteractingWithVillage = currentTargetGoblinController.isInteractingWithVillage;
                currentVillageForGoblin = currentTargetGoblinController.currentInteractingVillage;
                goblinVillageInteractionTime = currentTargetGoblinController.currentVillageInteractionTime;
            }

            if (isGoblinInteractingWithVillage && currentVillageForGoblin != null && goblinVillageInteractionTime > 1.0f && !isActivelyEngaging)
            {
                Debug.Log(gameObject.name + " (Tank) saw focused hero " + currentTargetTransform.name + " interacting with village. Investigating village.");
                investigationTarget = currentVillageForGoblin.transform.position;
                investigatingNoise = true; 
                isActivelyEngaging = false; 
                // Keep heroSpottedTime for the hero, this is about village
            }
            else if (!investigatingNoise) 
            {
                heroSpottedTime += Time.deltaTime;
                if (heroSpottedTime >= timeToActuallyDetectHero)
                {
                    isActivelyEngaging = true;
                    investigatingNoise = false;

                    Vector3 directionToHero = (currentTargetTransform.position - transform.position).normalized;
                    directionToHero.y = 0;
                    transform.Translate(directionToHero * moveSpeed * Time.deltaTime, Space.World);
                    transform.LookAt(new Vector3(currentTargetTransform.position.x, transform.position.y, currentTargetTransform.position.z));

                    if (Vector3.Distance(transform.position, currentTargetTransform.position) <= attackRange && attackTimer <= 0)
                    {
                        currentTargetHealth.TakeDamage(attackDamage);
                        attackTimer = attackCooldown;
                    }
                }
                else // Not detected long enough
                {
                    isActivelyEngaging = false;
                    Vector3 directionToHero = (currentTargetTransform.position - transform.position).normalized;
                    directionToHero.y = 0;
                    if (directionToHero != Vector3.zero) transform.rotation = Quaternion.LookRotation(directionToHero);
                }
            }
        }
        else // No current target (no hero visible or in range)
        {
             isActivelyEngaging = false; // Ensure not stuck in engaging state
             heroSpottedTime = 0f;

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
        else if (currentTargetTransform != null && currentTargetHeroController != null)
        {
            bool isStealthedGizmo = false;
            bool isHiddenGizmo = false;
            if (currentTargetGoblinController != null)
            {
                isStealthedGizmo = currentTargetGoblinController.isCloaked;
                isHiddenGizmo = currentTargetGoblinController.IsHidden;
            }
            else
            {
                AlchemistController acGizmo = currentTargetHeroController as AlchemistController;
                if (acGizmo != null) isHiddenGizmo = acGizmo.IsHidden;
                else
                {
                    WitchController wcGizmo = currentTargetHeroController as WitchController;
                    if (wcGizmo != null) isHiddenGizmo = wcGizmo.IsHidden;
                }
            }
            
            bool isHeroActuallyVisibleGizmo = !isStealthedGizmo && !isHiddenGizmo &&
                                           Vector3.Distance(transform.position, currentTargetTransform.position) <= detectionRadius;

            if (isStealthedGizmo && Vector3.Distance(transform.position, currentTargetTransform.position) <= detectionRadius)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f); 
                Gizmos.DrawLine(transform.position, currentTargetTransform.position);
            }
            else if (isActivelyEngaging)
            {
                Gizmos.color = Color.magenta; 
                Gizmos.DrawLine(transform.position, currentTargetTransform.position);
            }
            else if (isHeroActuallyVisibleGizmo && heroSpottedTime > 0)
            {
                Gizmos.color = Color.yellow; 
                Gizmos.DrawLine(transform.position, currentTargetTransform.position);
            }
            else if (!isActivelyEngaging) 
            {
                Gizmos.color = Color.green;
                if(wanderTarget != null && wanderTarget != Vector3.zero) Gizmos.DrawLine(transform.position, wanderTarget);
            }
        }
        else
        {
            Gizmos.color = Color.green; 
            if(wanderTarget != null && wanderTarget != Vector3.zero) Gizmos.DrawLine(transform.position, wanderTarget);
        }
    }

    public void ReceiveAlert(Vector3 sourcePosition, Vector3 heroLastKnownPosition)
    {
        if (!isActivelyEngaging && !investigatingNoise)
        {
             // Check if currently seeing any hero. If so, might ignore alert.
            bool currentlySeeingAHero = false;
            GameObject[] allHeroes = GameObject.FindGameObjectsWithTag("HeroPlayer");
            foreach(GameObject hero in allHeroes)
            {
                if(hero.activeInHierarchy) 
                {
                    // Basic distance check as a proxy for visibility before full CheckVision
                    if(Vector3.Distance(transform.position, hero.transform.position) <= detectionRadius)
                    {
                         // A more robust check would involve calling a simplified visibility check here
                         // For now, assume if a hero is close, Tank might be "aware"
                        currentlySeeingAHero = true;
                        break;
                    }
                }
            }
            if(currentlySeeingAHero && currentTargetTransform != null) // If already focused or seeing someone
            {
                 Debug.Log(gameObject.name + " (Tank) received alert but is currently aware of a hero. Prioritizing direct awareness.");
                 return;
            }

            if (Vector3.Distance(transform.position, heroLastKnownPosition) > 1.0f)
            {
                Debug.Log(gameObject.name + " (Tank) received alert from " + sourcePosition + ". Investigating hero at " + heroLastKnownPosition);
                investigationTarget = heroLastKnownPosition;
                investigatingNoise = true; 
                ResetCurrentTarget(); // Clear current hero focus when investigating an alert
            }
        }
        else
        {
            Debug.Log(gameObject.name + " (Tank) received alert but is already busy (engaging: " + isActivelyEngaging + ", investigating: " + investigatingNoise + ")");
        }
    }
    
    void ResetCurrentTarget()
    {
        if (currentTargetHeroController != null && currentTargetHeroController.isBeingActivelyDetected)
        {
            currentTargetHeroController.isBeingActivelyDetected = false;
        }
        currentTargetTransform = null;
        currentTargetHealth = null;
        currentTargetHeroController = null;
        currentTargetGoblinController = null;
        heroSpottedTime = 0f;
        isActivelyEngaging = false;
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0) return;
        isStunned = true;
        stunTimer = duration;
        Debug.Log(gameObject.name + " is STUNNED for " + duration + " seconds!");
        ResetCurrentTarget(); // Clear target on stun
        investigatingNoise = false;
    }
}
