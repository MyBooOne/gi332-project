using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace Complete
{
    public class PlayerRespawnManager : NetworkBehaviour
    {
        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 3f;
        
        // Reference to the player's tank health component
        private TankHealth tankHealth;
        
        private void Awake()
        {
            // หาคอมโพเนนท์ TankHealth
            tankHealth = GetComponent<TankHealth>();
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
            Vector3 respawnPosition = GetRespawnPosition();
            
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

        private Vector3 GetRespawnPosition()
        {
            // หาผู้เล่นทั้งหมด
            var players = FindObjectsOfType<NetworkObject>();
            
            // กำหนดระยะห่างขั้นต่ำและสูงสุด
            float minDistance = 10f;
            float maxDistance = 30f;
            
            // ลองหลายตำแหน่งเพื่อหาที่ที่เหมาะสม
            for (int i = 0; i < 10; i++)
            {
                // สร้างตำแหน่งสุ่ม
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minDistance, maxDistance);
                
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 potentialPosition = Vector3.zero + direction * distance;
                
                // ตรวจสอบว่าตำแหน่งนี้ห่างจากผู้เล่นอื่นเพียงพอหรือไม่
                bool isFarEnough = true;
                
                foreach (var player in players)
                {
                    // ข้ามตัวเอง
                    if (player.gameObject == gameObject)
                        continue;
                    
                    // ข้ามถ้าไม่ใช่รถถัง
                    if (player.GetComponent<TankHealth>() == null)
                        continue;
                    
                    float distToPlayer = Vector3.Distance(potentialPosition, player.transform.position);
                    if (distToPlayer < minDistance)
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
            float fallbackDistance = Random.Range(minDistance, maxDistance);
            Vector3 fallbackDirection = Quaternion.Euler(0, fallbackAngle, 0) * Vector3.forward;
            
            return Vector3.zero + fallbackDirection * fallbackDistance;
        }
    }
}