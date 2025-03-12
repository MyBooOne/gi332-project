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
    
    // เพิ่มตัวแปรสำหรับทำ interpolation
    private Vector3 oldPosition;
    private Quaternion oldRotation;
    private float lerpTime = 0f;
    private const float LERP_RATE = 10f; // ปรับค่านี้ตามความเหมาะสม (ค่าสูง = เคลื่อนที่เร็วขึ้น)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            // เพิ่ม interpolation ให้กับ Rigidbody
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        oldPosition = transform.position;
        oldRotation = transform.rotation;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
        
        // ติดตามการเปลี่ยนแปลงของตำแหน่งและการหมุน
        networkPosition.OnValueChanged += OnPositionChanged;
        networkRotation.OnValueChanged += OnRotationChanged;
        
        if (IsOwner)
        {
            Debug.Log("รถถังผู้เล่นท้องถิ่นถูกสร้าง: " + gameObject.name);
            
            // ตั้ง tag ให้กับรถถัง
            gameObject.tag = "isLocalPlayer";
            
            // ตั้งค่ากล้อง
            SetupCamera();
        }
    }
    
    private void SetupCamera()
    {
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
    
    private void OnPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        if (!IsOwner)
        {
            // เมื่อได้รับตำแหน่งใหม่ บันทึกตำแหน่งเก่าและเริ่มการ lerp ใหม่
            oldPosition = transform.position;
            lerpTime = 0f;
        }
    }
    
    private void OnRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        if (!IsOwner)
        {
            // เมื่อได้รับการหมุนใหม่ บันทึกการหมุนเก่าและเริ่มการ lerp ใหม่
            oldRotation = transform.rotation;
            lerpTime = 0f;
        }
    }

    void Update()
    {
        if (!IsOwner) 
        {
            // ถ้าไม่ใช่เจ้าของ ให้ทำ interpolation
            lerpTime += Time.deltaTime;
            float t = lerpTime * LERP_RATE;
            
            // Lerp ตำแหน่งและการหมุน
            transform.position = Vector3.Lerp(oldPosition, networkPosition.Value, t);
            transform.rotation = Quaternion.Slerp(oldRotation, networkRotation.Value, t);
            
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
}