using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Complete
{
    public class CustomNetworkSpawnManager : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private float minPlayerDistance = 10f;
        [SerializeField] private float maxPlayerDistance = 30f;
        
        // รายการเพื่อเก็บตำแหน่งการเกิดของผู้เล่น
        private List<Vector3> playerSpawnPositions = new List<Vector3>();
        
        // ตัวแปรเครือข่ายสำหรับจำนวนผู้เล่น
        private NetworkVariable<int> playerCount = new NetworkVariable<int>(0);
        
        // เริ่มต้นเมื่อเกมเริ่ม
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // จัดการการตรวจจับการเชื่อมต่อของผู้เล่นเฉพาะบนเซิร์ฟเวอร์
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            }
        }
        
        // จัดการการเชื่อมต่อของผู้เล่นใหม่
        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            
            // เพิ่มจำนวนผู้เล่น
            playerCount.Value++;
            
            // หาตำแหน่งเกิดใหม่
            Vector3 spawnPosition = GetCustomSpawnPosition();
            
            // เพิ่มไปยังรายการตำแหน่งการเกิด
            playerSpawnPositions.Add(spawnPosition);
            
            // ส่งตำแหน่งไปยังผู้เล่นที่เพิ่งเชื่อมต่อ
            SetPlayerSpawnPositionClientRpc(spawnPosition, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }
        
        // ส่งตำแหน่งการเกิดไปยังผู้เล่น
        [ClientRpc]
        private void SetPlayerSpawnPositionClientRpc(Vector3 spawnPosition, ClientRpcParams clientRpcParams)
        {
            // หาผู้เล่นท้องถิ่น
            NetworkObject localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            
            if (localPlayer != null)
            {
                // ย้ายผู้เล่นไปยังตำแหน่งการเกิด
                localPlayer.transform.position = spawnPosition;
            }
        }
        
        // วิธีคำนวณตำแหน่งการเกิดตามผู้เล่นอื่น
        private Vector3 GetCustomSpawnPosition()
        {
            if (playerCount.Value <= 1)
            {
                // ผู้เล่นแรก หรือการเกิดแบบสุ่มถูกปิดใช้งาน เกิดที่จุดศูนย์กลาง
                return Vector3.zero;
            }
            
            // ลองหลายตำแหน่งเพื่อหาที่ที่เหมาะสม
            for (int i = 0; i < 10; i++)
            {
                // สร้างตำแหน่งสุ่ม
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minPlayerDistance, maxPlayerDistance);
                
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 potentialPosition = Vector3.zero + direction * distance;
                
                // ตรวจสอบว่าตำแหน่งนี้ห่างจากตำแหน่งการเกิดอื่นเพียงพอหรือไม่
                bool isFarEnough = true;
                
                foreach (Vector3 existingPosition in playerSpawnPositions)
                {
                    float distToPosition = Vector3.Distance(potentialPosition, existingPosition);
                    if (distToPosition < minPlayerDistance)
                    {
                        isFarEnough = false;
                        break;
                    }
                }
                
                if (isFarEnough)
                {
                    return potentialPosition;
                }
            }
            
            // ถ้าหาตำแหน่งที่เหมาะสมไม่ได้ ใช้ตำแหน่งสุ่ม
            float fallbackAngle = Random.Range(0f, 360f);
            float fallbackDistance = Random.Range(minPlayerDistance, maxPlayerDistance);
            Vector3 fallbackDirection = Quaternion.Euler(0, fallbackAngle, 0) * Vector3.forward;
            
            return Vector3.zero + fallbackDirection * fallbackDistance;
        }
        
        // เมื่อวัตถุถูกทำลาย
        public override void OnDestroy()
        {
            base.OnDestroy();
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }
    }
}