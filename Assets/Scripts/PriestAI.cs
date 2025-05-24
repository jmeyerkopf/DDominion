using UnityEngine;
using System.Collections.Generic; // For finding multiple minions

public class PriestAI : MonoBehaviour
{
    // Movement Properties
    public float moveSpeed = 1.5f;
    public float followDistance = 3.0f;
    public float patrolRadius = 5.0f;
    public float waypointReachedThreshold = 0.5f;

    private Vector3 currentWaypoint;
    private bool isFollowing = false;
    private Transform targetFollowMinion = null;
    private float groundLevelY;

    // Healing Properties
    public float healRange = 5.0f;
    public float healAmount = 10.0f;
    public float healCooldown = 5.0f;
    private float healTimer = 0f;
    public float healHealthThreshold = 0.5f; // Heal if target is below 50% health

    // Reveal Ability Properties
    public float revealRange = 6.0f;
    public float revealAngle = 90.0f; 
    public float timeToSpotForReveal = 1.0f; 
    private float heroSpottedForRevealTimer = 0f;
    public float revealEffectDuration = 4.0f;
    public float revealCooldown = 15.0f;
    private float revealTimer = 0f;
    public LayerMask obstacleLayerMask; 
    // Store the generic MonoBehaviour for the hero, and attempt to get specific controllers when needed.
    private MonoBehaviour heroScriptInstance = null; 
    private Transform heroTransformInternal = null; 

    // Stun state
    private bool isStunned = false;
    private float stunTimer = 0f;

    void Start()
    {
        groundLevelY = transform.position.y;
        ChooseNewWaypoint();
        revealTimer = 0f; // Ensure ready at start, or set to cooldown if preferred
        heroSpottedForRevealTimer = 0f;

        // Attempt to find Hero at start for efficiency
        GameObject heroObject = GameObject.FindGameObjectWithTag("Hero");
        if (heroObject != null)
        {
            heroTransformInternal = heroObject.transform;
            // Try to get KnightController first, then HeroController as a fallback for heroScriptInstance
            heroScriptInstance = heroObject.GetComponent<KnightController>();
            if (heroScriptInstance == null)
            {
                heroScriptInstance = heroObject.GetComponent<HeroController>();
            }

            if (heroScriptInstance == null)
            {
                Debug.LogError(gameObject.name + ": Hero GameObject does not have a KnightController or HeroController component!");
            }
        }
        else
        {
            Debug.LogWarning(gameObject.name + ": Could not find GameObject with tag 'Hero' at Start. Reveal ability might be delayed.");
        }
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
            return; 
        }

        if (healTimer > 0)
        {
            healTimer -= Time.deltaTime;
        }
        if (revealTimer > 0)
        {
            revealTimer -= Time.deltaTime;
        }

        PerformRevealLogic(); // Prioritize reveal
        PerformHealingLogic();
        PerformMovementLogic();
    }

    void PerformRevealLogic()
    {
        if (revealTimer > 0) return; // Ability on cooldown

        if (heroScriptInstance == null || heroTransformInternal == null)
        {
            GameObject heroObject = GameObject.FindGameObjectWithTag("Hero");
            if (heroObject != null)
            {
                heroTransformInternal = heroObject.transform;
                heroScriptInstance = heroObject.GetComponent<KnightController>();
                if (heroScriptInstance == null)
                {
                    heroScriptInstance = heroObject.GetComponent<HeroController>();
                }
                if (heroScriptInstance == null) return; // No valid hero script
            }
            else return; // No hero object
        }
        
        if (!heroScriptInstance.gameObject.activeInHierarchy)
        {
            heroSpottedForRevealTimer = 0f; 
            return;
        }

        float distanceToHero = Vector3.Distance(transform.position, heroTransformInternal.position);

        if (distanceToHero <= revealRange)
        {
            Vector3 directionToHero = (heroTransformInternal.position - transform.position).normalized;
            float angleToHero = Vector3.Angle(transform.forward, directionToHero);

            if (angleToHero <= revealAngle / 2)
            {
                RaycastHit hit;
                Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; 
                Vector3 targetPosition = heroTransformInternal.position + Vector3.up * 0.5f;

                if (!Physics.Linecast(rayOrigin, targetPosition, out hit, obstacleLayerMask)) // No obstacle
                {
                    bool heroIsStealthed = false;
                    HeroController hc = heroScriptInstance as HeroController; // Try to cast to HeroController (Goblin)
                    if (hc != null)
                    {
                        heroIsStealthed = hc.IsHidden || hc.isCloaked;
                    }
                    // If hc is null, it's a Knight (or other non-HeroController hero), heroIsStealthed remains false.

                    if (heroIsStealthed)
                    {
                        heroSpottedForRevealTimer += Time.deltaTime;
                        if (heroSpottedForRevealTimer >= timeToSpotForReveal)
                        {
                            // Call ApplyRevealEffect on the specific type
                            if (hc != null) hc.ApplyRevealEffect(revealEffectDuration);
                            else 
                            {
                                KnightController kc = heroScriptInstance as KnightController;
                                if (kc != null) kc.ApplyRevealEffect(revealEffectDuration);
                            }
                            Debug.Log(gameObject.name + " revealed " + heroScriptInstance.name + "!");
                            revealTimer = revealCooldown;
                            heroSpottedForRevealTimer = 0f;
                        }
                    }
                    else
                    {
                        heroSpottedForRevealTimer = 0f; 
                    }
                    return; 
                }
            }
        }
        heroSpottedForRevealTimer = 0f;
    }

    void PerformHealingLogic()
    {
        if (healTimer <= 0)
        {
            List<GameObject> potentialTargets = new List<GameObject>();
            potentialTargets.AddRange(GameObject.FindGameObjectsWithTag("Scout"));
            potentialTargets.AddRange(GameObject.FindGameObjectsWithTag("Tank"));
            // Could add "Priest" tag here too if priests can heal each other

            GameObject bestTarget = null;
            float lowestHealthRatio = 1.0f; // Start with full health

            foreach (GameObject minionGO in potentialTargets)
            {
                if (!minionGO.activeInHierarchy || Vector3.Distance(transform.position, minionGO.transform.position) > healRange)
                {
                    continue;
                }

                Health minionHealth = minionGO.GetComponent<Health>();
                if (minionHealth != null)
                {
                    float currentHealth = minionHealth.GetCurrentHealth();
                    float maxHealth = minionHealth.GetMaxHealth();
                    if (maxHealth <= 0) continue; // Avoid division by zero if max health is not set or invalid

                    float healthRatio = currentHealth / maxHealth;
                    if (healthRatio < healHealthThreshold && healthRatio < lowestHealthRatio)
                    {
                        lowestHealthRatio = healthRatio;
                        bestTarget = minionGO;
                    }
                }
            }

            if (bestTarget != null)
            {
                Health targetHealth = bestTarget.GetComponent<Health>();
                targetHealth.Heal(healAmount); // Assuming Health.cs has Heal method
                healTimer = healCooldown;
                Debug.Log(gameObject.name + " healed " + bestTarget.name + " for " + healAmount + " HP. Current HP: " + targetHealth.GetCurrentHealth());

                targetFollowMinion = bestTarget.transform;
                isFollowing = true;
                // No need to set currentWaypoint here, as PerformMovementLogic will use targetFollowMinion
            }
        }
    }

    void PerformMovementLogic()
    {
        if (isFollowing && targetFollowMinion != null && targetFollowMinion.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(transform.position, targetFollowMinion.position) > followDistance)
            {
                MoveTowards(targetFollowMinion.position);
            }
            // Else, stay put if close enough
        }
        else // Not following, or target lost/inactive
        {
            if (targetFollowMinion != null && !targetFollowMinion.gameObject.activeInHierarchy)
            {
                 Debug.Log(gameObject.name + " lost target " + targetFollowMinion.name + ", resuming patrol.");
            }
            isFollowing = false;
            targetFollowMinion = null;

            MoveTowards(currentWaypoint);
            if (Vector3.Distance(transform.position, currentWaypoint) < waypointReachedThreshold)
            {
                ChooseNewWaypoint();
            }
        }
    }

    void ChooseNewWaypoint()
    {
        float randomX = Random.Range(-patrolRadius, patrolRadius);
        float randomZ = Random.Range(-patrolRadius, patrolRadius);
        currentWaypoint = new Vector3(transform.position.x + randomX, groundLevelY, transform.position.z + randomZ);
        // Ensure waypoint is reasonably different if it's the very first one or after losing a target.
        // This simple version might pick a point very close to current pos, which is fine for basic wander.
        Debug.Log(gameObject.name + " chose new waypoint: " + currentWaypoint);
    }

    void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // Keep movement on the ground plane

        transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
        if (direction != Vector3.zero) // Look towards movement direction
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Ensure priest stays on ground level
        Vector3 currentPosition = transform.position;
        currentPosition.y = groundLevelY;
        transform.position = currentPosition;
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0) return;
        isStunned = true;
        stunTimer = duration;
        Debug.Log(gameObject.name + " is STUNNED for " + duration + " seconds!");
        // Reset other states that might be interrupted by stun
        isFollowing = false; // Stop following if stunned
        // No direct attack or complex investigation state to reset in Priest
    }
}
