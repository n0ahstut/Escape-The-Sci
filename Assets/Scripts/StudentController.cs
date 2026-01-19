using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum StudentState
{
    Wander,
    FollowPlayer,
    Disperse
}

public class StudentController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float wanderSpeed = 2f;
    [SerializeField] private float followSpeed = 3.5f;
    [SerializeField] private float disperseSpeed = 4f;
    
    [Header("Wander Settings")]
    [SerializeField] private float wanderRadius = 20f;
    [SerializeField] private float wanderWaitTime = 2f;
    
    [Header("Follow Settings")]
    [SerializeField] private float followDistance = 2f;
    [SerializeField] private float crowdRadius = 3f;
    
    [Header("Disperse Settings")]
    [SerializeField] private float disperseDuration = 5f;
    [SerializeField] private float disperseDistance = 15f;
    
    [Header("Debug")]
    [SerializeField] private StudentState currentState = StudentState.Wander;
    
    private NavMeshAgent navAgent;
    private Vector3 startPosition;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private float disperseTimer = 0f;
    
    private Transform player;
    private static List<StudentController> allStudents = new List<StudentController>();
    private static bool isFollowingPlayer = false;
    
    public StudentState CurrentState => currentState;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        navAgent.speed = wanderSpeed;
        navAgent.stoppingDistance = 1f;
        navAgent.angularSpeed = 360f;
    }

    private void Start()
    {
        startPosition = transform.position;
        
        if (!allStudents.Contains(this))
        {
            allStudents.Add(this);
        }
        
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        
        // Subscribe to bell ring event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBellRing += OnBellRing;
        }
        
        // Start wandering
        SetNewWanderDestination();
    }

    private void OnDestroy()
    {
        allStudents.Remove(this);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBellRing -= OnBellRing;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            StopMoving();
            return;
        }
        
        // Try to find player if not found
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
        
        switch (currentState)
        {
            case StudentState.Wander:
                UpdateWanderState();
                break;
            case StudentState.FollowPlayer:
                UpdateFollowPlayerState();
                break;
            case StudentState.Disperse:
                UpdateDisperseState();
                break;
        }
    }

    private void UpdateWanderState()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewWanderDestination();
            }
            return;
        }
        
        if (HasReachedDestination())
        {
            isWaiting = true;
            waitTimer = wanderWaitTime + Random.Range(-1f, 1f);
        }
    }

    private void UpdateFollowPlayerState()
    {
        if (player == null)
        {
            TransitionToState(StudentState.Wander);
            return;
        }
        
        // Calculate offset position around player (so students crowd around, not stack on top)
        Vector3 targetPos = GetCrowdPosition();
        
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
        
        if (distanceToTarget > followDistance)
        {
            MoveTowards(targetPos, followSpeed);
        }
        else
        {
            StopMoving();
        }
    }

    private Vector3 GetCrowdPosition()
    {
        // Get a position around the player based on this student's index
        int index = allStudents.IndexOf(this);
        float angle = (index * 137.5f) * Mathf.Deg2Rad; // Golden angle for nice distribution
        float radius = crowdRadius + (index / 10f); // Spread out more as more students
        
        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * radius,
            0,
            Mathf.Sin(angle) * radius
        );
        
        return player.position + offset;
    }

    private void UpdateDisperseState()
    {
        disperseTimer -= Time.deltaTime;
        
        if (disperseTimer <= 0f)
        {
            TransitionToState(StudentState.Wander);
            return;
        }
        
        if (HasReachedDestination())
        {
            // Pick another disperse point
            SetDisperseDestination();
        }
    }

    private void TransitionToState(StudentState newState)
    {
        currentState = newState;
        
        switch (newState)
        {
            case StudentState.Wander:
                navAgent.speed = wanderSpeed;
                SetNewWanderDestination();
                break;
            case StudentState.FollowPlayer:
                navAgent.speed = followSpeed;
                break;
            case StudentState.Disperse:
                navAgent.speed = disperseSpeed;
                disperseTimer = disperseDuration;
                SetDisperseDestination();
                break;
        }
    }

    private void SetNewWanderDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += startPosition;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    private void SetDisperseDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * disperseDistance;
        randomDirection += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, disperseDistance, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    private void MoveTowards(Vector3 targetPosition, float speed)
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return;
        
        navAgent.speed = speed;
        navAgent.SetDestination(targetPosition);
    }

    private void StopMoving()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }
    }

    private bool HasReachedDestination()
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return true;
        
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            return true;
        }
        return false;
    }

    // Called when bell rings - toggle between following player and dispersing
    private void OnBellRing()
    {
        isFollowingPlayer = !isFollowingPlayer;
        
        if (isFollowingPlayer)
        {
            TransitionToState(StudentState.FollowPlayer);
            Debug.Log("Bell rang! Students following player!");
        }
        else
        {
            TransitionToState(StudentState.Disperse);
            Debug.Log("Bell rang! Students dispersing!");
        }
    }

    // Static method to make all students follow player
    public static void AllFollowPlayer()
    {
        isFollowingPlayer = true;
        foreach (StudentController student in allStudents)
        {
            student.TransitionToState(StudentState.FollowPlayer);
        }
        Debug.Log("All students following player!");
    }

    // Static method to disperse all students
    public static void DisperseAllStudents()
    {
        isFollowingPlayer = false;
        foreach (StudentController student in allStudents)
        {
            student.TransitionToState(StudentState.Disperse);
        }
        Debug.Log("All students dispersing!");
    }

    private void OnDrawGizmosSelected()
    {
        // Wander radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(startPosition != Vector3.zero ? startPosition : transform.position, wanderRadius);
        
        // Crowd radius
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.position, crowdRadius);
        }
    }
}
