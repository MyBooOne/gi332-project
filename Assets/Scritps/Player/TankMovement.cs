using Unity.Netcode;
using UnityEngine;

public class TankMovement : NetworkBehaviour
{
    [Header("Tank Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    
    private Rigidbody rb;
    private NetworkVariable<Vector3> networkPosition;
    private NetworkVariable<Quaternion> networkRotation;

    private void Awake()
    {
        networkPosition = new NetworkVariable<Vector3>();
        networkRotation = new NetworkVariable<Quaternion>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void Update()
    {
        // ตรวจสอบว่าเป็นรถถังของเราหรือไม่
        if (!IsOwner) return;

        HandleMovement();
    }

    void HandleMovement()
    {
        float moveInput = Input.GetAxis("Vertical");    // W และ S
        float rotateInput = Input.GetAxis("Horizontal"); // A และ D

        // คำนวณการเคลื่อนที่
        Vector3 movement = transform.forward * moveInput * moveSpeed * Time.deltaTime;
        
        // คำนวณการหมุน
        float rotation = rotateInput * rotationSpeed * Time.deltaTime;

        if (rb != null)
        {
            // ส่งคำขอเคลื่อนที่ไปยัง Server
            MoveServerRpc(movement, rotation);
        }
    }

    [ServerRpc]
    private void MoveServerRpc(Vector3 movement, float rotation)
    {
        // อัพเดทตำแหน่งและการหมุน
        rb.MovePosition(rb.position + movement);
        
        Quaternion turnRotation = Quaternion.Euler(0f, rotation, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);

        // อัพเดทค่าใน NetworkVariable
        networkPosition.Value = rb.position;
        networkRotation.Value = rb.rotation;
    }

    public override void OnNetworkSpawn()
    {
        // ตั้งค่าเริ่มต้นเมื่อ Spawn ในเครือข่าย
        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
    }
}