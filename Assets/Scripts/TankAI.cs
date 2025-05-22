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

        bool heroVisuallyDetected = false;
        if (heroHealth.GetCurrentHealth() > 0 && !heroController.IsHidden)
        {
            if (Vector3.Distance(transform.position, heroTransformInternal.position) <= detectionRadius)
            {
                heroVisuallyDetected = true;
            }
        }

        if (heroVisuallyDetected)
        {
            investigatingNoise = false; // Visual detection overrides noise
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
        else // Not visually detecting Hero
        {
            if (!investigatingNoise) 
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
                    }
                }
            }

            if (investigatingNoise)
            {
                // Debug.Log(gameObject.name + " is investigating noise at " + investigationTarget);
                Vector3 directionToNoise = (investigationTarget - transform.position).normalized;
                directionToNoise.y = 0;
                transform.Translate(directionToNoise * moveSpeed * Time.deltaTime, Space.World);
                if(directionToNoise != Vector3.zero) transform.rotation = Quaternion.LookRotation(directionToNoise);

                if (Vector3.Distance(transform.position, investigationTarget) < 1.0f) 
                {
                    investigatingNoise = false;
                    // Debug.Log(gameObject.name + " finished investigating noise.");
                    SetNewWanderTarget(); 
                }
            }
            else 
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
        else if (heroTransformInternal != null && heroController != null && !heroController.IsHidden && Vector3.Distance(transform.position, heroTransformInternal.position) <= detectionRadius)
        {
            Gizmos.color = Color.magenta; 
            Gizmos.DrawLine(transform.position, heroTransformInternal.position);
        }
        else
        {
            Gizmos.color = Color.green; 
            if(wanderTarget != null && wanderTarget != Vector3.zero) Gizmos.DrawLine(transform.position, wanderTarget);
        }
    }
}
