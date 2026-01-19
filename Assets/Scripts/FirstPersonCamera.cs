using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _cameraHeight = 0.6f;

    private Camera _camera;
    private float _verticalRotation = 0f;

    void Start()
    {
        // Create camera as child of player, or find existing main camera
        _camera = Camera.main;
        if (_camera == null)
        {
            GameObject camObj = new GameObject("FirstPersonCamera");
            _camera = camObj.AddComponent<Camera>();
        }

        // Parent camera to player and position at head height
        _camera.transform.SetParent(transform);
        _camera.transform.localPosition = new Vector3(0, _cameraHeight, 0);
        _camera.transform.localRotation = Quaternion.identity;

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        // Horizontal rotation - rotate the player
        transform.Rotate(0, mouseX, 0);

        // Vertical rotation - rotate only the camera
        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -90f, 90f);
        _camera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0, 0);

        // Press Escape to unlock cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
