using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private float turretRotateSpeed = 100f;

    [Header("References")]
    [SerializeField] private Transform turretTransform; // หอปืน
    [SerializeField] private Transform barrelTransform; // ปากกระบอก

    private Rigidbody rb;
    private Vector2 movement;
    private float mouseX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleInput();
        HandleTurretRotation();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleInput()
    {
        // WASD input
        movement.x = Input.GetAxisRaw("Horizontal"); // A,D หรือ ←,→
        movement.y = Input.GetAxisRaw("Vertical");   // W,S หรือ ↑,↓
        movement = movement.normalized;

        // Mouse input for turret rotation
        mouseX = Input.GetAxis("Mouse X");
    }

    private void HandleMovement()
    {
        // Forward/Backward movement
        Vector3 moveDirection = transform.forward * movement.y;
        rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);

        // Tank rotation (left/right)
        if (movement.x != 0)
        {
            Quaternion rotation = Quaternion.Euler(0f, movement.x * rotateSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * rotation);
        }
    }

    private void HandleTurretRotation()
    {
        if (turretTransform != null)
        {
            // Rotate turret based on mouse X movement
            turretTransform.Rotate(0f, mouseX * turretRotateSpeed * Time.deltaTime, 0f, Space.World);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            // Initialize any owner-specific setup
            Camera.main.transform.parent = turretTransform;
            Camera.main.transform.localPosition = new Vector3(0f, 2f, -4f);
            Camera.main.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }

    private void OnDestroy()
    {
        if (IsOwner)
        {
            // Reset cursor when destroyed
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
