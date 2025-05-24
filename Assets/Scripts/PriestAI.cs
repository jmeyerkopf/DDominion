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
    // Removed heroScriptInstance and heroTransformInternal as persistent single target fields.
    // heroSpottedForRevealTimer will also need to be managed differently if tracking multiple heroes for reveal.
    // For now, it will act as a general timer for the first hero that meets spotting criteria.
    // A more advanced system would use a Dictionary<GameObject, float> for individual spotting timers.

    // Stun state
    private bool isStunned = false;
    private float stunTimer = 0f;

    void Start()
    {
        groundLevelY = transform.position.y;
        ChooseNewWaypoint();
        revealTimer = 0f; // Ensure ready at start, or set to cooldown if preferred
        heroSpottedForRevealTimer = 0f;
        // Hero acquisition removed from Start(). Will be done in PerformRevealLogic.
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

        GameObject[] heroObjects = GameObject.FindGameObjectsWithTag("HeroPlayer");
        bool revealedThisCycle = false; // To ensure we only reveal one hero per cooldown cycle

        foreach (GameObject heroGO in heroObjects)
        {
            if (revealedThisCycle) break; // Cooldown started, wait for next opportunity
            if (heroGO == null || !heroGO.activeInHierarchy) continue;

            HeroControllerBase heroBase = heroGO.GetComponent<HeroControllerBase>();
            if (heroBase == null) continue;

            float distanceToHero = Vector3.Distance(transform.position, heroGO.transform.position);

            if (distanceToHero <= revealRange)
            {
                Vector3 directionToHero = (heroGO.transform.position - transform.position).normalized;
                float angleToHero = Vector3.Angle(transform.forward, directionToHero);

                if (angleToHero <= revealAngle / 2)
                {
                    RaycastHit hit;
                    Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; 
                    Vector3 targetPosition = heroGO.transform.position + Vector3.up * 0.5f;

                    if (!Physics.Linecast(rayOrigin, targetPosition, out hit, obstacleLayerMask) || hit.transform == heroGO.transform)
                    {
                        bool heroIsStealthedOrHidden = false;
                        GoblinOutlawController goc = heroBase as GoblinOutlawController;
                        if (goc != null) heroIsStealthedOrHidden = goc.IsHidden; // Assumes GoblinOutlawController.IsHidden covers cloak too
                        else
                        {
                            AlchemistController ac = heroBase as AlchemistController;
                            if (ac != null) heroIsStealthedOrHidden = ac.IsHidden;
                            else
                            {
                                WitchController wc = heroBase as WitchController;
                                if (wc != null) heroIsStealthedOrHidden = wc.IsHidden;
                            }
                        }

                        if (heroIsStealthedOrHidden)
                        {
                            // Simplified: If a hero is spotted meeting criteria, reveal immediately.
                            // The heroSpottedForRevealTimer would ideally be per-hero.
                            // For now, this means any stealthed hero in LoS for one frame can trigger the timer.
                            // If multiple are, the first one processed this frame that is stealthed will be the focus of the timer.
                            // This is a simplification; a dictionary for timers is more robust.
                            
                            // Let's assume for now, if a stealthed hero is in LoS, we increment a general timer.
                            // If that timer passes, we reveal THE CURRENT heroGO in iteration that is stealthed.
                            // This isn't perfect if attention switches rapidly, but is a step.
                            
                            // For this pass, let's make it simpler: if a stealthed hero is seen, and the general timer is ready, reveal.
                            // This means 'heroSpottedForRevealTimer' is not tied to a *specific* hero being continuously watched.
                            heroSpottedForRevealTimer += Time.deltaTime; 
                            if (heroSpottedForRevealTimer >= timeToSpotForReveal)
                            {
                                heroBase.ApplyRevealEffect(revealEffectDuration);
                                Debug.Log(gameObject.name + " revealed " + heroGO.name + " (Type: " + heroBase.GetType().Name + ")!");
                                revealTimer = revealCooldown;
                                heroSpottedForRevealTimer = 0f; // Reset timer after a successful reveal
                                revealedThisCycle = true; // Break outer loop after reveal
                            }
                            // If we revealed, we should break from checking other heroes this cycle.
                            if(revealedThisCycle) break; 
                        }
                        else
                        {
                            // If the current hero in LoS is NOT stealthed, reset the general spotting timer.
                            // This prevents revealing a stealthed hero if a non-stealthed one walks into view
                            // and resets the "concentration".
                            heroSpottedForRevealTimer = 0f;
                        }
                    }
                    else { heroSpottedForRevealTimer = 0f; } // Obstacle, reset timer
                }
                else { heroSpottedForRevealTimer = 0f; } // Not in angle, reset timer
            }
            else { heroSpottedForRevealTimer = 0f; } // Out of range, reset timer for this hero (or general timer if not seeing anyone)
        }
        // If loop finishes and no hero was stealthed/visible to keep timer going, reset it.
        if (!revealedThisCycle && heroObjects.Length == 0) // Or if no heroes were found at all
        {
            heroSpottedForRevealTimer = 0f;
        }
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
