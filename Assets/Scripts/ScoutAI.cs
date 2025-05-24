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
    
    // Variables to store the current hero being focused on for detection/chase
    private HeroControllerBase focusedHeroController;
    private Transform focusedHeroTransform;
    private Health focusedHeroHealth;
    private GoblinOutlawController focusedGoblinHeroController; // For specific checks like cloak/village

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
                ResetFocus(); // Clear focused hero on stun recovery
            }
            return; 
        }
        
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (howlTimer > 0) howlTimer -= Time.deltaTime;

        GameObject[] heroObjects = GameObject.FindGameObjectsWithTag("HeroPlayer");
        GameObject bestTargetGO = null;
        float closestDistSqr = Mathf.Infinity;

        // Find the closest, visible hero
        foreach (GameObject heroGO in heroObjects)
        {
            if (!heroGO.activeInHierarchy) continue;
            if (CheckVision(heroGO))
            {
                float distSqr = (heroGO.transform.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    bestTargetGO = heroGO;
                }
            }
        }

        if (bestTargetGO != null) // A hero is visible
        {
            // If we are not already focusing on this hero, or if we had no focus, switch focus.
            if (focusedHeroTransform == null || focusedHeroTransform != bestTargetGO.transform)
            {
                Debug.Log(gameObject.name + " new focus: " + bestTargetGO.name);
                focusedHeroTransform = bestTargetGO.transform;
                focusedHeroController = bestTargetGO.GetComponent<HeroControllerBase>();
                focusedHeroHealth = bestTargetGO.GetComponent<Health>();
                focusedGoblinHeroController = bestTargetGO.GetComponent<GoblinOutlawController>();
                heroSpottedTime = 0f; // Reset spotted time for new target
                 // Reset detection on previous target if it existed and is different
                if(focusedHeroController != null && focusedHeroController.gameObject != bestTargetGO)
                {
                    focusedHeroController.isBeingActivelyDetected = false;
                    // detectionLevel will decay naturally
                }
            }

            // Now, all logic uses the 'focusedHero...' variables
            bool interactingWithVillage = false;
            GameObject currentVillage = null;
            float villageInteractionTime = 0f;

            if (focusedGoblinHeroController != null)
            {
                interactingWithVillage = focusedGoblinHeroController.isInteractingWithVillage;
                currentVillage = focusedGoblinHeroController.currentInteractingVillage;
                villageInteractionTime = focusedGoblinHeroController.currentVillageInteractionTime;
            }

            if (interactingWithVillage && currentVillage != null && villageInteractionTime > 1.0f && !isActivelyChasing)
            {
                Debug.Log(gameObject.name + " saw focused hero " + focusedHeroTransform.name + " interacting with " + currentVillage.name + ". Investigating village.");
                investigationTarget = currentVillage.transform.position;
                investigatingNoise = true;
                isWandering = false;
                isActivelyChasing = false;
                // heroSpottedTime = 0; // Keep heroSpottedTime for the hero, this is about village
            }
            else if (!investigatingNoise || !isWandering) 
            {
                investigatingNoise = false; 
                heroSpottedTime += Time.deltaTime;

                if (heroSpottedTime >= timeToDetectHero) 
                {
                    focusedHeroController.isBeingActivelyDetected = true;
                    if (focusedHeroController.detectionLevel < 1.0f)
                    {
                        focusedHeroController.detectionLevel += Time.deltaTime / 1.0f; 
                    }

                    if (focusedHeroController.detectionLevel >= 1.0f)
                    {
                        if (!isActivelyChasing && howlTimer <= 0) 
                        {
                            if (MinionAlertManager.Instance != null)
                            {
                                Debug.Log(gameObject.name + " howls! Alerting others about " + focusedHeroTransform.name);
                                MinionAlertManager.Instance.AlertOthers(gameObject, focusedHeroTransform.position);
                                howlTimer = howlCooldown;
                            }
                            else
                            {
                                Debug.LogWarning(gameObject.name + " wants to howl, but MinionAlertManager instance is not found.");
                            }
                        }
                        isActivelyChasing = true;
                        isWandering = false;
                        MoveTowards(focusedHeroTransform.position);

                        if (Vector3.Distance(transform.position, focusedHeroTransform.position) <= attackRange && attackTimer <= 0)
                        {
                            if (focusedHeroHealth != null && focusedHeroTransform.gameObject.activeInHierarchy)
                            {
                                focusedHeroHealth.TakeDamage(attackDamage);
                                attackTimer = attackCooldown;
                            }
                        }
                    }
                    else 
                    {
                        isActivelyChasing = false; 
                        MoveTowards(focusedHeroTransform.position); 
                    }
                }
                else 
                {
                    if (focusedHeroController.detectionLevel < 1.0f) focusedHeroController.isBeingActivelyDetected = false;
                }
            }
        }
        else // No hero is currently visible
        {
            if (focusedHeroController != null) // If we were focusing on a hero
            {
                focusedHeroController.isBeingActivelyDetected = false; // Mark them as not actively detected
            }
            ResetFocus(); // Clear current focus

            if (!isActivelyChasing && !investigatingNoise) 
            {
                bool foundClue = SearchForClues();
                if (!foundClue) 
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

    // CheckVision now takes a specific hero GameObject as a parameter
    bool CheckVision(GameObject heroGO)
    {
        if (heroGO == null || !heroGO.activeInHierarchy)
        {
            return false;
        }

        HeroControllerBase heroController = heroGO.GetComponent<HeroControllerBase>();
        GoblinOutlawController goblinController = heroGO.GetComponent<GoblinOutlawController>();
        // Add other specific hero types if needed for unique stealth checks

        if (heroController == null) return false; // Should always have HeroControllerBase

        // Generalized stealth check
        bool isStealthed = false;
        if (goblinController != null) 
        {
            isStealthed = goblinController.isCloaked || goblinController.IsHidden;
        }
        else 
        {
            AlchemistController ac = heroController as AlchemistController;
            if (ac != null) isStealthed = ac.IsHidden;
            else
            {
                WitchController wc = heroController as WitchController;
                if (wc != null) isStealthed = wc.IsHidden;
                // KnightController does not have IsHidden/isCloaked
            }
        }

        if (isStealthed)
        {
            return false;
        }

        Vector3 rayOrigin = eyePosition.position;
        Vector3 targetPosition = heroGO.transform.position + Vector3.up * 0.5f; 
        
        Vector3 directionToHero = (targetPosition - rayOrigin).normalized;
        float distanceToHero = Vector3.Distance(rayOrigin, targetPosition);

        if (distanceToHero > visionDistance) return false; 

        float angleToHero = Vector3.Angle(eyePosition.forward, directionToHero);
        if (angleToHero > visionAngle / 2) return false; 

        RaycastHit hit;
        if (Physics.Linecast(rayOrigin, targetPosition, out hit, obstacleLayerMask))
        {
            // Check if the hit was the hero itself (e.g. if hero's layer is part of obstacleLayerMask for some reason)
            if (hit.transform != heroGO.transform)
            {
                return false;
            }
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
            if(currentWaypoint != null && currentWaypoint != Vector3.zero) Gizmos.DrawLine(transform.position, currentWaypoint);
        }
    // Gizmos for current target hero
        if (focusedHeroTransform != null)
        {
            bool isStealthedGizmo = false;
            bool isHiddenGizmo = false;
            if (focusedGoblinHeroController != null)
            {
                isStealthedGizmo = focusedGoblinHeroController.isCloaked;
                isHiddenGizmo = focusedGoblinHeroController.IsHidden;
            }
            else if (focusedHeroController != null)
            {
                AlchemistController ac = focusedHeroController as AlchemistController;
                if (ac != null) isHiddenGizmo = ac.IsHidden;
                else
                {
                    WitchController wc = focusedHeroController as WitchController;
                    if (wc != null) isHiddenGizmo = wc.IsHidden;
                }
            }

            if (isStealthedGizmo) Gizmos.color = Color.blue; 
            else if (!isHiddenGizmo)
            {
                if(isActivelyChasing) Gizmos.color = Color.red; 
                else if (heroSpottedTime > 0) Gizmos.color = new Color(1f, 0.5f, 0f); 
                else Gizmos.color = Color.yellow; 
            }
             if(Vector3.Distance(eyePosition.position, focusedHeroTransform.position) <= visionDistance &&
                Vector3.Angle(eyePosition.forward, (focusedHeroTransform.position - eyePosition.position).normalized) <= visionAngle / 2)
             {
                if (!isHiddenGizmo || isStealthedGizmo) // Draw if not hidden, or if cloaked (blue)
                    Gizmos.DrawLine(transform.position, focusedHeroTransform.position);
             }
        }
    }

    void ResetFocus()
    {
        // If we were focusing on a hero, ensure their detection state is reset if appropriate
        if (focusedHeroController != null && focusedHeroController.isBeingActivelyDetected)
        {
            focusedHeroController.isBeingActivelyDetected = false;
            // detectionLevel will decay naturally on the hero's script
        }

        focusedHeroTransform = null;
        focusedHeroController = null;
        focusedHeroHealth = null;
        focusedGoblinHeroController = null;
        heroSpottedTime = 0f;
        isActivelyChasing = false; // Stop chasing if focus is lost
    }

    public void ReceiveAlert(Vector3 sourcePosition, Vector3 heroLastKnownPosition)
    {
        if (isWandering && !isActivelyChasing && !investigatingNoise)
        {
            // Check if currently seeing any hero. If so, might ignore alert or prioritize direct sight.
            bool currentlySeeingAHero = false;
            GameObject[] allHeroes = GameObject.FindGameObjectsWithTag("HeroPlayer");
            foreach(GameObject hero in allHeroes)
            {
                if(CheckVision(hero))
                {
                    currentlySeeingAHero = true;
                    break;
                }
            }
            if(currentlySeeingAHero)
            {
                 Debug.Log(gameObject.name + " received alert but is currently seeing a hero. Prioritizing direct sight.");
                 return;
            }

            if (Vector3.Distance(transform.position, heroLastKnownPosition) > 1.0f) 
            {
                Debug.Log(gameObject.name + " received alert from " + sourcePosition + ". Investigating hero at " + heroLastKnownPosition);
                investigationTarget = heroLastKnownPosition;
                investigatingNoise = true; 
                isWandering = false;
                ResetFocus(); // Clear any previous hero focus when investigating an alert
            }
        }
        else
        {
            // Log why the alert is being ignored, more specifically.
            string reason = "";
            if(isActivelyChasing) reason = "actively chasing";
            else if(investigatingNoise) reason = "investigating noise";
            else reason = "not wandering or has other priority";
            Debug.Log(gameObject.name + " received alert but is already busy (" + reason + ")");
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
