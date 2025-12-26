using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("UI Control")]
    [SerializeField] private KeyCode taskListKey = KeyCode.T;

    // Components
    private CharacterController controller;

    // Movement
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;

    // Camera rotation
    private float xRotation = 0f;

    // State
    private bool isUIOpen = false;
    private bool canMove = true;

    // Events for game management
    public delegate void InteractionHandler(GameObject interactedObject);
    public event InteractionHandler OnInteract;

    public delegate void UIToggleHandler(bool isOpen);
    public event UIToggleHandler OnTaskListToggle;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Handle UI toggle
        if (Input.GetKeyDown(taskListKey))
        {
            ToggleTaskList();
        }

        // Only allow movement and interaction when UI is closed and movement is enabled
        if (!isUIOpen && canMove)
        {
            HandleMovement();
            HandleMouseLook();
            HandleInteraction();
        }
    }

    void HandleMovement()
    {
        // Ground check
        isGrounded = Physics.CheckSphere(transform.position, groundCheckDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        // Determine speed (shift to run)
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        // Calculate movement direction relative to where player is looking
        Vector3 move = transform.right * horizontal + transform.forward * vertical;

        // Move the controller
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotate camera up/down 
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate player left/right
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleInteraction()
    {
        // Check for interaction input
        if (Input.GetKeyDown(interactKey))
        {
            // Raycast from camera to detect interactable objects
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactionRange, interactableMask))
            {
                // Check if the object has an interactable component
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();

                if (interactable != null)
                {
                    interactable.Interact(this);
                }

                // Trigger event for game management
                OnInteract?.Invoke(hit.collider.gameObject);
            }
        }
    }

    void ToggleTaskList()
    {
        isUIOpen = !isUIOpen;

        // Toggle cursor visibility and lock state
        if (isUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Notify game manager about UI state change
        OnTaskListToggle?.Invoke(isUIOpen);
    }

    // Public methods for game management
    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }

    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
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


    // Interface for interactable objects (teachers, students, etc.)
    public interface IInteractable
    {
        void Interact(PlayerController player);
    }
}