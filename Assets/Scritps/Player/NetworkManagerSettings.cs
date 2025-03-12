using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkManagerSettings : MonoBehaviour
{
    void Start()
    {
        // ตั้งค่า NetworkManager
        NetworkManager netManager = NetworkManager.Singleton;
        if (netManager != null)
        {
            // เพิ่ม Tick Rate เพื่อลดความล่าช้า
            netManager.NetworkConfig.TickRate = 60; // เพิ่มเป็น 60 Hz จากค่าเริ่มต้น 30 Hz
            
            // ปรับการตั้งค่าอื่นๆ ถ้าจำเป็น
            var utpTransport = netManager.GetComponent<UnityTransport>();
            if (utpTransport != null)
            {
                // ตั้งค่าโดยตรงที่ Inspector properties ที่เห็นในภาพ
                utpTransport.MaxPayloadSize = 6144;
                utpTransport.MaxPacketQueueSize = 512; // เพิ่มจากค่าเดิม 128
                utpTransport.HeartbeatTimeoutMS = 500;
                utpTransport.ConnectTimeoutMS = 1000;
                utpTransport.MaxConnectAttempts = 60;
                utpTransport.DisconnectTimeoutMS = 10000; // ลดลงจากค่าเดิม 30000 เพื่อตรวจจับการตัดการเชื่อมต่อเร็วขึ้น
                
                Debug.Log("ตั้งค่า NetworkManager และ UnityTransport สำเร็จ");
            }
        }
    }
}