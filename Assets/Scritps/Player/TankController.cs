using Unity.Netcode;
using UnityEngine;

public class TankController : NetworkBehaviour
{
    [Header("Tank Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    
    private Rigidbody rb;
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();

    private void Awake()
    {
        // ไม่จำเป็นต้องกำหนด NetworkVariableSettings แล้ว
        // เพราะ NetworkVariable จะจัดการสิทธิ์การเข้าถึงให้อัตโนมัติ
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
        if (!IsOwner) 
        {
            // ถ้าไม่ใช่เจ้าของ ให้อัพเดทตำแหน่งตาม NetworkVariable
            transform.position = networkPosition.Value;
            transform.rotation = networkRotation.Value;
            return;
        }

        HandleMovement();
    }

    void HandleMovement()
    {
        float moveInput = 0f;
        float rotateInput = 0f;

        // W = เดินหน้า, S = ถอยหลัง
        if (Input.GetKey(KeyCode.W))
            moveInput = 1f;
        else if (Input.GetKey(KeyCode.S))
            moveInput = -1f;

        // A = หมุนซ้าย, D = หมุนขวา
        if (Input.GetKey(KeyCode.D))
            rotateInput = 1f;
        else if (Input.GetKey(KeyCode.A))
            rotateInput = -1f;

        // คำนวณการเคลื่อนที่
        Vector3 movement = transform.forward * moveInput * moveSpeed * Time.deltaTime;
        
        // คำนวณการหมุน
        float rotation = rotateInput * rotationSpeed * Time.deltaTime;

        if (rb != null)
        {
            // เคลื่อนที่ local ก่อน
            rb.MovePosition(rb.position + movement);
            Quaternion turnRotation = Quaternion.Euler(0f, rotation, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);

            // ส่ง ServerRpc เพื่ออัพเดทตำแหน่งบน server
            UpdatePositionServerRpc(rb.position, rb.rotation);
        }
    }

    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        // อัพเดทค่าใน NetworkVariable
        networkPosition.Value = position;
        networkRotation.Value = rotation;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
        
        // เพิ่มตรงนี้: เมื่อรถถังของผู้เล่นท้องถิ่นถูกสร้าง ให้บอกกล้องว่าควรติดตามรถถังนี้
        if (IsOwner)
        {
            Debug.Log("รถถังผู้เล่นท้องถิ่นถูกสร้าง: " + gameObject.name);
            
            // ตั้ง tag ให้กับรถถัง (ถ้ายังไม่มี)
            gameObject.tag = "isLocalPlayer";
            
            // หา Main Camera และตั้งค่าให้ติดตามรถถังนี้
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                PlayerCameraFollow cameraFollow = mainCamera.GetComponent<PlayerCameraFollow>();
                if (cameraFollow != null)
                {
                    cameraFollow.SetTarget(gameObject);
                    Debug.Log("ตั้งค่ากล้องให้ติดตามรถถัง: " + gameObject.name);
                }
                else
                {
                    Debug.LogError("ไม่พบคอมโพเนนต์ PlayerCameraFollow บนกล้อง");
                }
            }
            else
            {
                Debug.LogError("ไม่พบ Main Camera ในซีน");
            }
        }
    }
}