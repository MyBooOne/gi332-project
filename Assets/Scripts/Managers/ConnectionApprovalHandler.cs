using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    [SerializeField] private int maxPlayers = 4; // เพิ่มเป็น 4 คน
    
    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        }
    }
    
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // ตรวจสอบว่ามีผู้เล่นเกินจำนวนสูงสุดหรือไม่
        if (NetworkManager.Singleton.ConnectedClientsList.Count < maxPlayers)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
        }
        else
        {
            response.Approved = false;
            response.Reason = "เซิร์ฟเวอร์เต็มแล้ว";
        }
    }
}