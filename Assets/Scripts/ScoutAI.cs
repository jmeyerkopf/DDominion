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
    public float timeToDetectHero = 2.0f; 

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

    void Start()
    {
        groundLevelY = transform.position.y;
        ChooseNewWaypoint();
        
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
        if (heroController == null || heroTransformInternal == null) 
        {
            if (isWandering) MoveToWanderPoint();
            return;
        }
        
        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        bool canSeeHero = CheckVision();

        if (canSeeHero)
        {
            investigatingNoise = false; // Visual detection overrides noise investigation
            heroController.isBeingActivelyDetected = true; 
            if (heroController.detectionLevel < 1.0f)
            {
                heroController.detectionLevel += Time.deltaTime / timeToDetectHero;
            }

            if (heroController.detectionLevel >= 1.0f)
            {
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
                isActivelyChasing = false; 
                MoveTowards(heroTransformInternal.position); 
            }
        }
        else // Cannot see Hero
        {
            if (isActivelyChasing && heroController.detectionLevel < 1.0f)
            {
                isActivelyChasing = false;
                investigatingNoise = false; 
            }

            // Noise Investigation Logic (only if not seeing hero and not already fully chasing)
            if (!isActivelyChasing && !investigatingNoise) 
            {
                Vector3 noisePos;
                float noiseRad;
                if (NoiseManager.GetLatestNoise(out noisePos, out noiseRad)) 
                {
                    if (Vector3.Distance(transform.position, noisePos) <= noiseRad * noiseInvestigationRadiusMultiplier) 
                    {
                        // Debug.Log(gameObject.name + " heard noise at " + noisePos);
                        investigationTarget = noisePos;
                        investigatingNoise = true;
                        isWandering = false; 
                    }
                }
            }

            if (investigatingNoise)
            {
                MoveTowards(investigationTarget);
                if (Vector3.Distance(transform.position, investigationTarget) < 1.0f) 
                {
                    investigatingNoise = false;
                    // Debug.Log(gameObject.name + " finished investigating noise.");
                }
            }
            else if (!isActivelyChasing) 
            {
                isWandering = true;
                MoveToWanderPoint();
            }
        }
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
        if (heroController == null || heroTransformInternal == null || !heroTransformInternal.gameObject.activeInHierarchy || heroController.IsHidden)
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
        else if (heroTransformInternal != null && heroController != null && !heroController.IsHidden)
        {
            if(isActivelyChasing) Gizmos.color = Color.red; 
            else Gizmos.color = new Color(1f, 0.5f, 0f); 
            Gizmos.DrawLine(transform.position, heroTransformInternal.position);
        }
    }
}
