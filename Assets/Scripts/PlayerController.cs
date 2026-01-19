using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera Settings")]
    [SerializeField] private float horizontalSensitivity = 5f;
    [SerializeField] private float verticalSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float cameraHeight = 0.6f;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("UI Control")]
    [SerializeField] private KeyCode taskListKey = KeyCode.T;

    private Rigidbody rb;
    private Camera playerCamera;

    private Vector3 moveDirection;
    private bool isGrounded;
    private float currentSpeed;
    private float xRotation = 0f;

    private bool isUIOpen = false;
    private bool canMove = true;

    private Vector2Int currentCell;
    private bool isInRoom = false;

    public delegate void InteractionHandler(GameObject interactedObject);
    public event InteractionHandler OnInteract;

    public delegate void UIToggleHandler(bool isOpen);
    public event UIToggleHandler OnTaskListToggle;

    public delegate void CellChangedHandler(Vector2Int newCell, bool isRoom);
    public event CellChangedHandler OnCellChanged;

    public Vector2Int CurrentCell => currentCell;
    public bool IsInRoom => isInRoom;

    private void Start()
    {
        SetupRigidbody();
        SetupCamera();
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupRigidbody()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void SetupCamera()
    {
        playerCamera = GetComponentInChildren<Camera>();
        
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            
            if (playerCamera == null)
            {
                GameObject camObj = new GameObject("PlayerCamera");
                playerCamera = camObj.AddComponent<Camera>();
            }
            
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0, cameraHeight, 0);
            playerCamera.transform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            return;
        }
        
        if (Input.GetKeyDown(taskListKey))
        {
            ToggleTaskList();
        }

        if (!isUIOpen && canMove)
        {
            HandleInput();
            HandleMouseLook();
            HandleInteraction();
        }

        UpdateCellPosition();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            return;
        }
        
        if (!isUIOpen && canMove)
        {
            HandleMovement();
        }
    }

    private void UpdateCellPosition()
    {
        if (MazeGenerator.Instance == null) return;

        Vector2Int newCell = MazeGenerator.Instance.GetCellFromWorldPosition(transform.position);
        
        if (newCell != currentCell)
        {
            currentCell = newCell;
            isInRoom = MazeGenerator.Instance.IsCellRoom(currentCell.x, currentCell.y);
            OnCellChanged?.Invoke(currentCell, isInRoom);
        }
    }

    private void HandleInput()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        moveDirection = (transform.right * horizontal + transform.forward * vertical).normalized;
    }

    private void HandleMovement()
    {
        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 targetVelocity = moveDirection * currentSpeed;
            targetVelocity.y = rb.velocity.y;
            rb.velocity = targetVelocity;
        }
        else
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * horizontalSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * verticalSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleInteraction()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactionRange, interactableMask))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();

                if (interactable != null)
                {
                    interactable.Interact(this);
                }

                OnInteract?.Invoke(hit.collider.gameObject);
            }
        }
    }

    private void ToggleTaskList()
    {
        isUIOpen = !isUIOpen;

        if (isUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            rb.velocity = Vector3.zero;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        OnTaskListToggle?.Invoke(isUIOpen);
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled)
        {
            rb.velocity = Vector3.zero;
        }
    }

    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
        rb.velocity = Vector3.zero;
    }

    public Camera GetCamera()
    {
        return playerCamera;
    }

    public bool IsUIOpen()
    {
        return isUIOpen;
    }

    public void ForceCloseUI()
    {
        if (isUIOpen)
        {
            ToggleTaskList();
        }
    }

    public interface IInteractable
    {
        void Interact(PlayerController player);
    }
}
