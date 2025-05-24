using UnityEngine;

public class ScoutAI : MonoBehaviour
{
    public float moveSpeed = 2.0f;
    public float wanderRadius = 5.0f; 
    public float waypointReachedThreshold = 0.5f; 

    // Vision Cone Fields
    public float visionDistance = 7.0f;
    public float visionAngle = 90.0f; 
    public LayerMask obstacleLayerMask; 
    public Transform eyePosition; 

    // Detection Level Management
    public float timeToDetectHero = 2.0f; // This is the time it takes for detectionLevel to go from 0 to 1 once spotting starts.
                                          // The new 2-second rule is about *continuous* LoS before this timer even starts.
    private float heroSpottedTime = 0f; // Time hero has been continuously in LoS.

    private Vector3 currentWaypoint;
    private bool isWandering = true;
    private float groundLevelY = 0.5f; 
    
    private HeroController heroController; 
    private Health heroHealth; 
    private Transform heroTransformInternal;

    // Attack properties
    public float attackDamage = 5.0f;
    public float attackRange = 1.0f; 
    public float attackCooldown = 1.5f;
    private float attackTimer = 0f;

    private bool isActivelyChasing = false; 

    // Noise Investigation
    private Vector3 investigationTarget = Vector3.zero;
    private bool investigatingNoise = false;
    public float noiseInvestigationRadiusMultiplier = 1.5f; 

    // Howl Alert
    public float howlCooldown = 10.0f;
    private float howlTimer = 0f;

    // Clue Detection
    public float clueDetectionRadius = 4.0f; 
    public string clueTag = "Clue";
    // private bool isInvestigatingClue = false; // Could be a separate state if needed
    private bool hasInitialMission = false;

    // Stun state
    private bool isStunned = false;
    private float stunTimer = 0f;

    void Start()
    {
        groundLevelY = transform.position.y;
        if (!hasInitialMission) // Only choose a random waypoint if no initial mission is set
        {
            ChooseNewWaypoint();
        }
        
        GameObject heroObject = GameObject.FindGameObjectWithTag("Hero");
        if (heroObject != null)
        {
            heroTransformInternal = heroObject.transform;
            heroController = heroObject.GetComponent<HeroController>();
            if (heroController == null) Debug.LogError("ScoutAI: Hero GameObject does not have a HeroController component!");
            heroHealth = heroObject.GetComponent<Health>();
            if (heroHealth == null) Debug.LogError("ScoutAI: Hero GameObject does not have a Health component!");
        }
        else
        {
            Debug.LogWarning("ScoutAI: Could not find GameObject with tag 'Hero'. Detection/Chasing/Noise will not work effectively.");
        }

        if (eyePosition == null) eyePosition = transform;
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
            }
            // Potentially stop any current movement (e.g., if using NavMeshAgent, agent.isStopped = true)
            // For Translate-based movement, simply returning here prevents further action processing.
            return; 
        }

        if (heroController == null || heroTransformInternal == null) 
        {
            if (isWandering) MoveToWanderPoint();
            return;
        }
        
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (howlTimer > 0) howlTimer -= Time.deltaTime;

        bool canSeeHero = CheckVision();

        if (canSeeHero)
        {
            // Priority 1: Check if hero is interacting with a village (if visible)
            if (heroController.isInteractingWithVillage && 
                heroController.currentInteractingVillage != null && 
                heroController.currentVillageInteractionTime > 1.0f && 
                !isActivelyChasing) // Don't override active chase for this, but can start investigating
            {
                // Player is seen interacting with a village for more than 1 second.
                // Scout becomes suspicious of the village.
                Debug.Log(gameObject.name + " saw hero interacting with " + heroController.currentInteractingVillage.name + " for " + heroController.currentVillageInteractionTime + "s. Investigating village.");
                investigationTarget = heroController.currentInteractingVillage.transform.position;
                investigatingNoise = true; // Re-use state to move towards the village
                isWandering = false;
                isActivelyChasing = false; // Not chasing hero directly, but investigating village
                heroSpottedTime = 0; // Reset direct hero spotting time as focus is now village
                // This path makes the scout go to the village. 
                // Further logic for what happens upon reaching the village can be added later.
            }
            // Priority 2: Normal detection and chasing logic if not investigating village due to interaction
            else if (!investigatingNoise || !isWandering) // if investigatingNoise is true from above, skip this else if.
                                                          // if isWandering is false from above, skip this else if.
                                                          // This condition ensures that if we just set investigatingNoise to true for village, we don't immediately fall into hero chase.
            {
                investigatingNoise = false; // Visual detection of hero (not village interaction) overrides general noise investigation
                heroSpottedTime += Time.deltaTime;

                if (heroSpottedTime >= timeToDetectHero) // Using timeToDetectHero as the 2-second continuous LoS requirement
                {
                    heroController.isBeingActivelyDetected = true;
                if (heroController.detectionLevel < 1.0f)
                {
                    // The original timeToDetectHero now represents how fast detectionLevel fills *after* continuous sight.
                    // For simplicity, let's make it fill reasonably fast once continuous sight is achieved.
                    // Or, we can assume timeToDetectHero was meant for the continuous sight part.
                    // Let's adjust so detectionLevel fills over 1 second *after* the 2-second continuous LoS.
                    heroController.detectionLevel += Time.deltaTime / 1.0f; 
                }

                if (heroController.detectionLevel >= 1.0f)
                {
                    if (!isActivelyChasing && howlTimer <= 0) // Just reached full detection and can howl
                    {
                        if (MinionAlertManager.Instance != null)
                        {
                            Debug.Log(gameObject.name + " howls! Alerting others.");
                            MinionAlertManager.Instance.AlertOthers(gameObject, heroTransformInternal.position);
                            howlTimer = howlCooldown;
                        }
                        else
                        {
                            Debug.LogWarning(gameObject.name + " wants to howl, but MinionAlertManager instance is not found.");
                        }
                    }

                    isActivelyChasing = true;
                    isWandering = false;
                    MoveTowards(heroTransformInternal.position);

                    if (Vector3.Distance(transform.position, heroTransformInternal.position) <= attackRange && attackTimer <= 0)
                    {
                        if (heroHealth != null && heroTransformInternal.gameObject.activeInHierarchy)
                        {
                            heroHealth.TakeDamage(attackDamage);
                            attackTimer = attackCooldown;
                        }
                    }
                }
                else
                {
                    // Still in the phase of increasing detection level, but continuously spotted
                    isActivelyChasing = false; // Not fully chasing yet, but aware
                    MoveTowards(heroTransformInternal.position); // Turn towards hero
                }
            }
                else
                {
                    // Hero is in sight, but not for long enough to be "detected"
                    // Optionally, turn towards hero or enter a "suspicious" state here.
                    // For now, we just wait until heroSpottedTime reaches the threshold.
                    // Resetting isBeingActivelyDetected if they were previously detected but LoS was broken and reacquired.
                    if (heroController.detectionLevel < 1.0f) heroController.isBeingActivelyDetected = false;
                }
            }
        }
        else // Cannot see Hero
        {
            heroSpottedTime = 0f; // Reset spotted time if hero is not visible
            
            // If we were chasing but lost sight, and detection level is low, stop chasing.
            if (isActivelyChasing && heroController.detectionLevel < 1.0f) 
            {
                isActivelyChasing = false;
                // Keep investigatingNoise true if it was set due to village interaction or other alerts.
                // Only set investigatingNoise to false if the reason for it (like direct hero sight) is gone.
                // If investigatingNoise was true due to an alert or village, it should persist until target is reached.
            }
            // If not actively chasing (hero or village) and not already investigating a noise source (like a direct sound or alert):
            // This is where we can look for clues.
            if (!isActivelyChasing && !investigatingNoise) 
            {
                bool foundClue = SearchForClues(); // Try to find clues first

                if (!foundClue) // If no clues found, then check for general noise
                {
                    Vector3 noisePos;
                float noiseRad;
                if (NoiseManager.GetLatestNoise(out noisePos, out noiseRad))
                {
                        if (Vector3.Distance(transform.position, noisePos) <= noiseRad * noiseInvestigationRadiusMultiplier)
                        {
                            investigationTarget = noisePos;
                            investigatingNoise = true;
                            isWandering = false;
                        }
                    }
                }
            }

            // This block handles movement towards an investigation target (could be noise, alert, village, or clue)
            if (investigatingNoise)
            {
                MoveTowards(investigationTarget);
                if (Vector3.Distance(transform.position, investigationTarget) < 1.0f) // Reached the target
                {
                    investigatingNoise = false; // Stop investigating this specific point
                    // If this was a clue, it would have been destroyed by SearchForClues.
                    // Scout will now either wander, find another clue, or react to other stimuli.
                    Debug.Log(gameObject.name + " reached investigation target: " + investigationTarget);
                }
            }
            // If not chasing, not investigating, then wander.
            else if (!isActivelyChasing) 
            {
                isWandering = true;
                MoveToWanderPoint();
            }
        }
    }

    bool SearchForClues()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, clueDetectionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag(clueTag))
            {
                Debug.Log(gameObject.name + " found a clue at " + hitCollider.transform.position);
                investigationTarget = hitCollider.transform.position;
                investigatingNoise = true; // Re-use state for moving towards the clue
                isWandering = false;
                // isInvestigatingClue = true; // If a separate state is desired

                Destroy(hitCollider.gameObject); // Consume the clue
                return true; // Found and reacted to a clue
            }
        }
        return false; // No clue found
    }
    
    void MoveToWanderPoint()
    {
        MoveTowards(currentWaypoint);
        if (Vector3.Distance(new Vector3(transform.position.x, groundLevelY, transform.position.z), currentWaypoint) < waypointReachedThreshold)
        {
            ChooseNewWaypoint();
        }
    }

    bool CheckVision()
    {
        if (heroController == null || heroTransformInternal == null || !heroTransformInternal.gameObject.activeInHierarchy)
        {
            return false;
        }

        // Respect Cloak and standard stealth (IsHidden)
        if (heroController.isCloaked || heroController.IsHidden)
        {
            return false;
        }

        Vector3 rayOrigin = eyePosition.position;
        Vector3 targetPosition = heroTransformInternal.position + Vector3.up * 0.5f; 
        
        Vector3 directionToHero = (targetPosition - rayOrigin).normalized;
        float distanceToHero = Vector3.Distance(rayOrigin, targetPosition);

        if (distanceToHero > visionDistance) return false; 

        float angleToHero = Vector3.Angle(eyePosition.forward, directionToHero);
        if (angleToHero > visionAngle / 2) return false; 

        RaycastHit hit;
        if (Physics.Linecast(rayOrigin, targetPosition, out hit, obstacleLayerMask))
        {
            return false;
        }
        
        return true; 
    }

    void ChooseNewWaypoint()
    {
        float randomX = Random.Range(-wanderRadius, wanderRadius);
        float randomZ = Random.Range(-wanderRadius, wanderRadius);
        currentWaypoint = new Vector3(transform.position.x + randomX, groundLevelY, transform.position.z + randomZ);
    }

    void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; 

        transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
        if(direction != Vector3.zero && (isActivelyChasing || investigatingNoise)) // Only look at target if chasing or investigating
        {
             transform.rotation = Quaternion.LookRotation(direction);
        }


        Vector3 currentPosition = transform.position;
        currentPosition.y = groundLevelY;
        transform.position = currentPosition;
    }

    void OnDrawGizmosSelected()
    {
        if (eyePosition != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 forward = eyePosition.forward; 
            Vector3 coneBoundaryLeft = Quaternion.Euler(0, -visionAngle / 2, 0) * forward;
            Vector3 coneBoundaryRight = Quaternion.Euler(0, visionAngle / 2, 0) * forward;
            Gizmos.DrawRay(eyePosition.position, coneBoundaryLeft * visionDistance);
            Gizmos.DrawRay(eyePosition.position, coneBoundaryRight * visionDistance);
            
            int segments = 20;
            Vector3 prevPoint = eyePosition.position + coneBoundaryLeft * visionDistance;
            for(int i = 1; i <= segments; i++)
            {
                float subAngle = Mathf.Lerp(-visionAngle / 2, visionAngle / 2, (float)i / segments);
                Vector3 currentRayDir = Quaternion.Euler(0, subAngle, 0) * forward;
                Vector3 currentPoint = eyePosition.position + currentRayDir * visionDistance;
                Gizmos.DrawLine(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }
             Gizmos.DrawLine(eyePosition.position, eyePosition.position + coneBoundaryLeft * visionDistance);
             Gizmos.DrawLine(eyePosition.position, eyePosition.position + coneBoundaryRight * visionDistance);
        }
        
        Gizmos.color = Color.red; 
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (investigatingNoise)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, investigationTarget);
            Gizmos.DrawWireSphere(investigationTarget, 0.5f);
        }
        else if (isWandering && !isActivelyChasing) 
        {
            Gizmos.color = Color.green;
            if(currentWaypoint != null) Gizmos.DrawLine(transform.position, currentWaypoint);
        }
        else if (heroTransformInternal != null && heroController != null)
        {
            if (heroController.isCloaked)
            {
                Gizmos.color = Color.blue; // Cloaked hero, special Gizmo color
                // Optionally, only draw if scout would otherwise see the hero
                // For simplicity, just draw blue line to cloaked hero if scout is trying to "see"
                 if(Vector3.Distance(eyePosition.position, heroTransformInternal.position) <= visionDistance &&
                    Vector3.Angle(eyePosition.forward, (heroTransformInternal.position - eyePosition.position).normalized) <= visionAngle / 2)
                 {
                    Gizmos.DrawLine(transform.position, heroTransformInternal.position);
                 }
            }
            else if (!heroController.IsHidden) // Hero is not hidden (and not cloaked)
            {
                if(isActivelyChasing) Gizmos.color = Color.red; 
                else if (heroSpottedTime > 0) Gizmos.color = new Color(1f, 0.5f, 0f); // Orange if spotted but not fully detected
                else Gizmos.color = Color.yellow; // Default vision color if just in LoS but not yet "spotted"
                Gizmos.DrawLine(transform.position, heroTransformInternal.position);
            }
            // If heroController.IsHidden is true (and not cloaked), no line is drawn by this section
        }
    }

    public void ReceiveAlert(Vector3 sourcePosition, Vector3 heroLastKnownPosition)
    {
        // Only respond if wandering and not already chasing, investigating, or seeing the hero directly
        if (isWandering && !isActivelyChasing && !investigatingNoise && !CheckVision())
        {
            // Check if the source of the alert is different enough or if this scout is already near the heroLastKnownPosition
            // This check helps prevent a scout from immediately re-alerting if it was the source or already heading there.
            if (Vector3.Distance(transform.position, heroLastKnownPosition) > 1.0f) // Avoid investigating if already at the spot
            {
                Debug.Log(gameObject.name + " received alert from " + sourcePosition + ". Investigating hero at " + heroLastKnownPosition);
                investigationTarget = heroLastKnownPosition;
                investigatingNoise = true; // Re-use investigatingNoise state for simplicity
                isWandering = false;
                // Do not reset heroSpottedTime here, as this is an indirect alert
            }
        }
        else
        {
            Debug.Log(gameObject.name + " received alert but is already busy (chasing: " + isActivelyChasing + ", investigating: " + investigatingNoise + ", canSeeHero: " + CheckVision() + ")");
        }
    }

    public void SetInitialMission(Vector3 missionTarget)
    {
        Debug.Log(gameObject.name + " received initial mission to: " + missionTarget);
        currentWaypoint = missionTarget;
        isWandering = true; // Use wandering state to move towards the mission target
        investigatingNoise = false; // Ensure not in investigation mode from a previous state (if pooling or reusing)
        isActivelyChasing = false; // Ensure not chasing from a previous state
        hasInitialMission = true; 

        // Adjust waypoint to be on the same ground level as the scout
        currentWaypoint.y = groundLevelY;
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0) return;
        isStunned = true;
        stunTimer = duration;
        Debug.Log(gameObject.name + " is STUNNED for " + duration + " seconds!");
        // Reset other states that might be interrupted by stun
        isActivelyChasing = false;
        investigatingNoise = false; 
        // isWandering might become true after stun if no other stimuli, or false if it should re-evaluate
    }
}
