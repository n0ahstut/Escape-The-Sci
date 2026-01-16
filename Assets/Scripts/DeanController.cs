using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Dean states
public enum DeanState
{
    Patrol,  // Walking around
    Chase,   // Chasing player
    Stalk    // Aggressive + prediction
}

// Dean AI - patrols, chases, catches player
public class DeanController : MonoBehaviour
{
    // Movement speeds
    [Header("Movement Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float stalkSpeed = 3.5f;
    [SerializeField] private float catchDistance = 1.5f;
    
    // Vision cone settings
    [Header("Field of View Settings")]
    [SerializeField] private float viewDistance = 10f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleLayer;
    
    // Patrol points
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 2f;
    
    // Sounds
    [Header("Audio")]
    [SerializeField] private AudioClip chaseMusic;
    [SerializeField] private AudioClip stalkMusic;
    [SerializeField] private AudioClip catchSound;
    
    // Components
    private AudioSource audioSource;
    private Transform player;
    private PlayerController playerController;
    private Animator animator;
    private NavMeshAgent navAgent;
    
    [Header("Debug (Read Only)")]
    [SerializeField] private DeanState _debugState;
    
    // State tracking
    private DeanState currentState = DeanState.Patrol;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private bool canMove = true;
    private float chaseTimer = 0f;
    private const float CHASE_DURATION = 20f;
    private const int STALK_THRESHOLD = 2; // Detentions needed for stalk mode
    
    // Bigram prediction - learns player patterns
    private BigramModel bigramModel;
    private Vector2Int lastKnownPlayerCell = new Vector2Int(-1, -1);
    private Vector3 predictedPosition = Vector3.zero;
    private bool isPlayerInRoom = false;
    
    // Animator hashes
    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsChasing = Animator.StringToHash("IsChasing");
    private static readonly int AnimIsStalking = Animator.StringToHash("IsStalking");

    private void Awake()
    {
        bigramModel = new BigramModel();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        navAgent = GetComponent<NavMeshAgent>();
        
        if (navAgent == null)
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        
        // Auto-add audio source with 3D sound
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 30f;
        }
        
        navAgent.speed = patrolSpeed;
        navAgent.stoppingDistance = 1.5f;
        navAgent.angularSpeed = 360f;
    }

    private void Start()
    {
        StartCoroutine(FindPlayerRoutine());
        StartCoroutine(CheckStalkModeDelayed());
    }

    // Check if should start in stalk mode
    private IEnumerator CheckStalkModeDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        int detentions = 0;
        if (GameManager.Instance != null)
            detentions = GameManager.Instance.CurrentDetentions;
        
        if (detentions >= STALK_THRESHOLD)
        {
            currentState = DeanState.Stalk;
            PlayAudio(stalkMusic);
        }
    }

    // Find player object
    private IEnumerator FindPlayerRoutine()
    {
        while (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerController = playerObj.GetComponent<PlayerController>();
                
                // Subscribe to player movement
                if (playerController != null)
                    playerController.OnCellChanged += HandlePlayerCellChanged;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDestroy()
    {
        if (playerController != null)
            playerController.OnCellChanged -= HandlePlayerCellChanged;
    }

    // Player moved - record for prediction
    private void HandlePlayerCellChanged(Vector2Int newCell, bool isRoom)
    {
        if (lastKnownPlayerCell.x >= 0)
            bigramModel.RecordTransition(CellToString(lastKnownPlayerCell), CellToString(newCell));
        
        lastKnownPlayerCell = newCell;
        isPlayerInRoom = isRoom;
    }

    // MAIN UPDATE - runs state machine
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            StopMoving();
            return;
        }
        if (!canMove)
        {
            StopMoving();
            return;
        }
        
        // Run current state
        switch (currentState)
        {
            case DeanState.Patrol:
                UpdatePatrolState();
                break;
            case DeanState.Chase:
                UpdateChaseState();
                break;
            case DeanState.Stalk:
                UpdateStalkState();
                break;
        }
        
        UpdateAnimator();
    }

    // Switch states
    private void TransitionToState(DeanState newState)
    {
        if (currentState == DeanState.Stalk) return; // Can't leave stalk
        
        DeanState previousState = currentState;
        currentState = newState;
        
        switch (newState)
        {
            case DeanState.Patrol:
                StopAudio();
                isWaiting = false;
                break;
            case DeanState.Chase:
                PlayAudio(chaseMusic);
                break;
            case DeanState.Stalk:
                PlayAudio(stalkMusic);
                break;
        }
        
        if (newState == DeanState.Chase)
        {
            navAgent.speed = chaseSpeed;
            chaseTimer = CHASE_DURATION;
        }
        else if (newState == DeanState.Patrol)
        {
            navAgent.speed = patrolSpeed;
        }
        
        Debug.Log($"{gameObject.name}: {previousState} -> {newState}");
    }

    // PATROL - walk between points, look for player
    private void UpdatePatrolState()
    {
        // See player? Chase!
        if (player != null && CanSeePlayer() && !isPlayerInRoom)
        {
            TransitionToState(DeanState.Chase);
            return;
        }
        
        // Waiting at patrol point
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                MoveToNextPatrolPoint();
            }
            return;
        }
        
        // Reached point? Wait
        if (HasReachedDestination())
        {
            isWaiting = true;
            waitTimer = patrolWaitTime;
        }
    }

    // CHASE - run at player
    private void UpdateChaseState()
    {
        if (player == null) return;
        
        chaseTimer -= Time.deltaTime;
        MoveTowards(player.position, chaseSpeed);
        
        // Close enough? Catch!
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= catchDistance && !isPlayerInRoom)
        {
            CatchPlayer();
            return;
        }
        
        // Lost player? Back to patrol
        if (chaseTimer <= 0 && !CanSeePlayer())
            TransitionToState(DeanState.Patrol);
    }

    // STALK - aggressive, uses prediction
    private void UpdateStalkState()
    {
        navAgent.speed = stalkSpeed;
        
        bool canSee = CanSeePlayer();
        bool playerInHallway = !isPlayerInRoom;
        
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            
            if (distanceToPlayer <= catchDistance && playerInHallway)
            {
                CatchPlayer();
                return;
            }
            else if (isPlayerInRoom)
            {
                WaitOutsideRoom();
            }
            else if (canSee)
            {
                MoveTowards(player.position, stalkSpeed);
                if (distanceToPlayer <= catchDistance)
                    CatchPlayer();
            }
            else
            {
                // Can't see? Use prediction!
                UsePredictionToFindPlayer();
            }
        }
    }

    // BIGRAM PREDICTION - guess where player goes next
    private void UsePredictionToFindPlayer()
    {
        string predictedCellStr = bigramModel.PredictNextRoom(CellToString(lastKnownPlayerCell));
        
        if (!string.IsNullOrEmpty(predictedCellStr) && MazeGenerator.Instance != null)
        {
            Vector2Int predictedCell = StringToCell(predictedCellStr);
            predictedPosition = MazeGenerator.Instance.GetWorldPositionFromCell(predictedCell.x, predictedCell.y);
            MoveTowards(predictedPosition, stalkSpeed);
        }
        else if (player != null)
        {
            MoveTowards(player.position, stalkSpeed);
        }
    }

    // Player in room - wait outside
    private void WaitOutsideRoom()
    {
        if (player != null)
            MoveTowards(player.position, stalkSpeed * 0.5f);
    }

    // VISION - cone check with wall blocking
    private bool CanSeePlayer()
    {
        if (player == null) return false;
        if (isPlayerInRoom) return false;
        
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Too far?
        if (distanceToPlayer > viewDistance) return false;
        
        // Outside view angle?
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > viewAngle / 2f) return false;
        
        // Wall in the way?
        Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
        Vector3 playerCenter = player.position + Vector3.up * 1f;
        if (Physics.Raycast(eyePosition, (playerCenter - eyePosition).normalized, distanceToPlayer, obstacleLayer))
            return false;
        
        return true;
    }

    // MOVEMENT - use navmesh
    private void MoveTowards(Vector3 targetPosition, float speed)
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return;
        navAgent.speed = speed;
        navAgent.SetDestination(targetPosition);
    }

    private void MoveToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        if (patrolPoints[currentPatrolIndex] != null)
            MoveTowards(patrolPoints[currentPatrolIndex].position, patrolSpeed);
    }

    private void StopMoving()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
            navAgent.ResetPath();
    }

    private bool HasReachedDestination()
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return true;
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            return true;
        return false;
    }

    // CATCH - give detention
    private void CatchPlayer()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        
        if (catchSound != null && audioSource != null)
            audioSource.PlayOneShot(catchSound);
        
        StopAudio();
        GameManager.Instance.GiveDetention();
        
        Debug.Log($"{gameObject.name} caught the player!");
        
        // Enter stalk mode?
        if (GameManager.Instance.CurrentDetentions >= STALK_THRESHOLD && currentState != DeanState.Stalk)
        {
            currentState = DeanState.Stalk;
            PlayAudio(stalkMusic);
        }
        else
        {
            TransitionToState(DeanState.Patrol);
        }
    }

    private void UpdateAnimator()
    {
        _debugState = currentState;
        if (animator == null) return;
        
        try
        {
            animator.SetBool(AnimIsMoving, !isWaiting);
            animator.SetBool(AnimIsChasing, currentState == DeanState.Chase);
            animator.SetBool(AnimIsStalking, currentState == DeanState.Stalk);
        }
        catch (System.Exception)
        {
            animator = null;
        }
    }

    // AUDIO
    private void PlayAudio(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopAudio()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    // Helpers - convert cell to string
    private string CellToString(Vector2Int cell) => $"{cell.x},{cell.y}";
    
    private Vector2Int StringToCell(string cellStr)
    {
        string[] parts = cellStr.Split(',');
        if (parts.Length == 2)
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        return Vector2Int.zero;
    }

    // Called when teacher reports player
    public void AlertToPlayer()
    {
        if (currentState == DeanState.Stalk) return;
        chaseTimer = CHASE_DURATION;
        TransitionToState(DeanState.Chase);
    }

    // Alert all deans at once
    public static void AlertAllDeans()
    {
        DeanController[] allDeans = FindObjectsOfType<DeanController>();
        foreach (DeanController dean in allDeans)
            dean.AlertToPlayer();
    }

    // Debug - draw vision cone in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward * viewDistance;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward * viewDistance;
        
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
    }
}

// BIGRAM MODEL - learns player movement patterns
// Records: "from cell A, player went to cell B"
// Predicts: "player is at A, they usually go to B"
public class BigramModel
{
    // fromCell -> (toCell -> count)
    private Dictionary<string, Dictionary<string, int>> transitions;

    public BigramModel()
    {
        transitions = new Dictionary<string, Dictionary<string, int>>();
    }

    // Record a move
    public void RecordTransition(string fromRoom, string toRoom)
    {
        if (!transitions.ContainsKey(fromRoom))
            transitions[fromRoom] = new Dictionary<string, int>();

        if (!transitions[fromRoom].ContainsKey(toRoom))
            transitions[fromRoom][toRoom] = 0;

        transitions[fromRoom][toRoom]++;
    }

    // Predict next cell - returns most common destination
    public string PredictNextRoom(string currentRoom)
    {
        if (!transitions.ContainsKey(currentRoom))
            return null;

        string mostLikely = null;
        int highestCount = 0;

        foreach (var pair in transitions[currentRoom])
        {
            if (pair.Value > highestCount)
            {
                highestCount = pair.Value;
                mostLikely = pair.Key;
            }
        }

        return mostLikely;
    }
}
