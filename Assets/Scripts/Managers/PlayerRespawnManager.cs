using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace Complete
{
    public class PlayerRespawnManager : NetworkBehaviour
    {
        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 3f;
        
        [Header("Map Boundaries")]
        [SerializeField] private float mapSizeX = 40f; // ความกว้างของแผนที่ (ครึ่งหนึ่งของความกว้างทั้งหมด)
        [SerializeField] private float mapSizeZ = 40f; // ความยาวของแผนที่ (ครึ่งหนึ่งของความยาวทั้งหมด)
        [SerializeField] private float safetyMargin = 5f; // ระยะห่างจากขอบแผนที่
        
        [Header("Respawn Points")]
        [SerializeField] private Transform[] specificRespawnPoints; // จุดเกิดที่กำหนดไว้ล่วงหน้า
        
        // Reference to the player's tank health component
        private TankHealth tankHealth;
        
        private void Awake()
        {
            // หาคอมโพเนนท์ TankHealth
            tankHealth = GetComponent<TankHealth>();
            
            // ตรวจสอบว่ามีการกำหนดจุดเกิดไว้ล่วงหน้าหรือไม่
            if (specificRespawnPoints == null || specificRespawnPoints.Length == 0)
            {
                // พยายามค้นหาจุดเกิดในฉาก
                GameObject[] respawnPointObjects = GameObject.FindGameObjectsWithTag("RespawnPoint");
                if (respawnPointObjects.Length > 0)
                {
                    specificRespawnPoints = new Transform[respawnPointObjects.Length];
                    for (int i = 0; i < respawnPointObjects.Length; i++)
                    {
                        specificRespawnPoints[i] = respawnPointObjects[i].transform;
                    }
                    Debug.Log($"Found {specificRespawnPoints.Length} respawn points in the scene");
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // ติดตามการเปลี่ยนแปลงสถานะการตาย
            tankHealth.m_Dead.OnValueChanged += OnDeadValueChanged;
        }

        // เมื่อสถานะการตายเปลี่ยน
        private void OnDeadValueChanged(bool oldValue, bool newValue)
        {
            if (newValue && IsOwner)
            {
                // แสดง UI เฉพาะเมื่อเป็นผู้เล่นท้องถิ่น (IsOwner)
                StartCoroutine(HandleDeath());
            }
        }

        // จัดการการตายและการเกิดใหม่
        private IEnumerator HandleDeath()
        {
            // แสดงหน้าจอการตายผ่าน UIManager
            if (DeathUIManager.Instance != null)
            {
                DeathUIManager.Instance.ShowDeathScreen();
            }
            else
            {
                Debug.LogError("PlayerRespawnManager: ไม่พบ DeathUIManager ในฉาก กรุณาเพิ่ม DeathUIManager ในฉาก");
            }
            
            // รอจนกระทั่งเวลาเกิดใหม่ผ่านไป
            int countdown = (int)respawnDelay;
            
            while (countdown > 0)
            {
                // อัปเดตข้อความนับถอยหลังผ่าน UIManager
                if (DeathUIManager.Instance != null)
                {
                    DeathUIManager.Instance.UpdateRespawnCountdown(countdown);
                }
                
                yield return new WaitForSeconds(1f);
                countdown--;
            }
            
            // เมื่อนับถอยหลังเสร็จ ขอให้เกิดใหม่
            RespawnPlayerServerRpc();
            
            // ซ่อนหน้าจอการตายผ่าน UIManager
            if (DeathUIManager.Instance != null)
            {
                DeathUIManager.Instance.HideDeathScreen();
            }
        }

        [ServerRpc]
        private void RespawnPlayerServerRpc()
        {
            // หาตำแหน่งเกิดใหม่
            Vector3 respawnPosition = GetSafeRespawnPosition();
            Debug.Log($"Respawning player at position: {respawnPosition}");
            
            // ทำให้เกิดใหม่บนเครื่องของทุกคน
            RespawnPlayerClientRpc(respawnPosition);
        }

        [ClientRpc]
        private void RespawnPlayerClientRpc(Vector3 respawnPosition)
        {
            // รีเซ็ตตำแหน่ง
            transform.position = respawnPosition;
            
            // เปิดใช้งานวัตถุ
            gameObject.SetActive(true);
            
            // รีเซ็ตสุขภาพ (เฉพาะบนเซิร์ฟเวอร์)
            if (IsServer)
            {
                float startingHealth = tankHealth.GetStartingHealth();
                tankHealth.m_CurrentHealth.Value = startingHealth;
                tankHealth.m_Dead.Value = false;
            }
            
            // รีเซ็ตภาพที่มองเห็นได้
            tankHealth.ResetTank();
        }

        private Vector3 GetSafeRespawnPosition()
        {
            // ลองใช้จุดเกิดที่กำหนดไว้ล่วงหน้าก่อน
            if (specificRespawnPoints != null && specificRespawnPoints.Length > 0)
            {
                for (int attempt = 0; attempt < specificRespawnPoints.Length; attempt++)
                {
                    // สุ่มจุดเกิด
                    int pointIndex = Random.Range(0, specificRespawnPoints.Length);
                    Vector3 respawnPoint = specificRespawnPoints[pointIndex].position;
                    
                    // ตรวจสอบความปลอดภัย (ห่างจากผู้เล่นอื่นพอสมควร)
                    if (IsSafePosition(respawnPoint))
                    {
                        return respawnPoint;
                    }
                }
                
                // ถ้าไม่มีจุดปลอดภัย ใช้จุดใดจุดหนึ่ง
                int fallbackIndex = Random.Range(0, specificRespawnPoints.Length);
                return specificRespawnPoints[fallbackIndex].position;
            }
            
            // ถ้าไม่มีจุดเกิดที่กำหนดไว้ล่วงหน้า ให้สร้างตำแหน่งใหม่ที่อยู่ในขอบเขตแผนที่
            return CreateSafeRandomPosition();
        }
        
        private bool IsSafePosition(Vector3 position)
        {
            // กำหนดระยะห่างที่ปลอดภัยจากผู้เล่นอื่น
            float minDistanceFromPlayers = 10f;
            
            // หาผู้เล่นทั้งหมด
            var players = FindObjectsOfType<NetworkObject>();
            
            foreach (var player in players)
            {
                // ข้ามตัวเอง
                if (player.gameObject == gameObject)
                    continue;
                
                // ตรวจสอบเฉพาะผู้เล่นที่มี TankHealth (รถถัง)
                TankHealth otherTank = player.GetComponent<TankHealth>();
                if (otherTank != null && !otherTank.m_Dead.Value)
                {
                    // ตรวจสอบระยะห่าง
                    float distToPlayer = Vector3.Distance(position, player.transform.position);
                    if (distToPlayer < minDistanceFromPlayers)
                    {
                        return false; // ตำแหน่งไม่ปลอดภัย
                    }
                }
            }
            
            return true; // ตำแหน่งปลอดภัย
        }
        
        private Vector3 CreateSafeRandomPosition()
        {
            // ลองสร้างตำแหน่งสุ่มหลายครั้ง
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // สร้างตำแหน่งสุ่มในขอบเขตแผนที่
                float x = Random.Range(-mapSizeX + safetyMargin, mapSizeX - safetyMargin);
                float z = Random.Range(-mapSizeZ + safetyMargin, mapSizeZ - safetyMargin);
                
                // Y ค่าคงที่สำหรับแผนที่แบบแบน หรือใช้ Raycast เพื่อหาความสูงที่เหมาะสม
                float y = 0f;
                
                // ถ้าต้องการหาความสูงที่เหมาะสม
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out hit, 200f))
                {
                    y = hit.point.y + 0.5f; // +0.5 เพื่อให้สูงจากพื้นเล็กน้อย
                }
                else
                {
                    // ใช้ความสูงปัจจุบันของผู้เล่น
                    y = transform.position.y;
                }
                
                Vector3 randomPos = new Vector3(x, y, z);
                
                // ตรวจสอบว่าตำแหน่งนี้ปลอดภัยหรือไม่
                if (IsSafePosition(randomPos))
                {
                    return randomPos;
                }
            }
            
            // ถ้าไม่สามารถหาตำแหน่งที่ปลอดภัยได้ ให้ใช้ตำแหน่งสุ่มที่อยู่ในขอบเขตแผนที่
            float fallbackX = Random.Range(-mapSizeX + safetyMargin, mapSizeX - safetyMargin);
            float fallbackZ = Random.Range(-mapSizeZ + safetyMargin, mapSizeZ - safetyMargin);
            
            return new Vector3(fallbackX, transform.position.y, fallbackZ);
        }
        
        // เพิ่มเมธอดนี้เพื่อช่วยในการดีบัก
        private void OnDrawGizmosSelected()
        {
            // แสดงขอบเขตแผนที่
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(mapSizeX * 2, 1, mapSizeZ * 2));
            
            // แสดงขอบเขตปลอดภัย
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(
                (mapSizeX - safetyMargin) * 2, 
                1, 
                (mapSizeZ - safetyMargin) * 2));
            
            // แสดงจุดเกิดที่กำหนดไว้
            if (specificRespawnPoints != null)
            {
                Gizmos.color = Color.blue;
                foreach (var point in specificRespawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawSphere(point.position, 1f);
                    }
                }
            }
        }
    }
}