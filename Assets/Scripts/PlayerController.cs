using UnityEngine;
using Unity.Netcode;
using Complete;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    private GameObject cameraRig;
    
    // ตัวแปรสำหรับการตั้งค่ากล้อง
    private Vector3 cameraRotation = new Vector3(40, 60, 0);
    private float dampTime = 0.2f;
    private float screenEdgeBuffer = 4f;
    private float minSize = 6.5f;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            CreatePlayerCamera();
            GetComponent<MeshRenderer>().material.color = Color.blue;
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Color.red;
        }
    }

    private void CreatePlayerCamera()
    {
        // สร้างกล้อง
        Camera playerCamera = new GameObject("Main Camera").AddComponent<Camera>();
        
        // สร้าง CameraRig
        cameraRig = new GameObject($"CameraRig_{OwnerClientId}");
        playerCamera.transform.SetParent(cameraRig.transform);

        // ตั้งค่าตำแหน่งและมุมกล้อง
        cameraRig.transform.rotation = Quaternion.Euler(cameraRotation);
        playerCamera.transform.localPosition = new Vector3(0, 0, -10);

        // เพิ่มและตั้งค่า CameraControl
        var cameraControl = cameraRig.AddComponent<Complete.CameraControl>();
        cameraControl.m_DampTime = dampTime;
        cameraControl.m_ScreenEdgeBuffer = screenEdgeBuffer;
        cameraControl.m_MinSize = minSize;
        
        // ตั้งค่า targets สำหรับกล้อง
        cameraControl.m_Targets = new Transform[] { transform };
        
        // ตั้งค่าตำแหน่งเริ่มต้น
        cameraControl.SetStartPositionAndSize();
    }

    private void Update()
    {
        if (!IsOwner) return;

        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ = 1f;
        if (Input.GetKey(KeyCode.S)) moveZ = -1f;
        if (Input.GetKey(KeyCode.A)) moveX = -1f;
        if (Input.GetKey(KeyCode.D)) moveX = 1f;

        Vector3 movement = new Vector3(moveX, 0f, moveZ);
        if (movement != Vector3.zero)
        {
            movement.Normalize();
            transform.Translate(movement * moveSpeed * Time.deltaTime);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (cameraRig != null)
        {
            Destroy(cameraRig);
        }
    }
}