using UnityEngine;
using Unity.Netcode;

namespace Complete
{
    public class TankShooting : NetworkBehaviour
    {
        [SerializeField] private GameObject m_ShellPrefab;
        [SerializeField] private Transform m_FireTransform;
        [SerializeField] private float m_LaunchForce = 20f;
        [SerializeField] private float m_CooldownTime = 0.5f;
        
        private float m_NextFireTime;
        private AudioSource m_ShootingAudio;

        private void Start()
        {
            m_ShootingAudio = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.Space) && Time.time >= m_NextFireTime)
            {
                FireServerRpc();
                m_NextFireTime = Time.time + m_CooldownTime;
            }
        }

        [ServerRpc]
        private void FireServerRpc()
        {
            // สร้างกระสุน
            GameObject shellInstance = Instantiate(
                m_ShellPrefab,
                m_FireTransform.position,
                m_FireTransform.rotation
            );

            // ตั้งค่า NetworkObject
            NetworkObject networkObject = shellInstance.GetComponent<NetworkObject>();
            
            // ตั้งค่า Rigidbody
            Rigidbody shellRigidbody = shellInstance.GetComponent<Rigidbody>();
            if (shellRigidbody != null)
            {
                // สำคัญ: ต้องตั้ง properties ของ Rigidbody ก่อน Spawn
                shellRigidbody.isKinematic = false;
                shellRigidbody.useGravity = true;
                shellRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                
                // Spawn บนเน็ตเวิร์ค
                networkObject.Spawn();
                
                // ใส่แรงให้กระสุนหลังจาก Spawn
                shellRigidbody.AddForce(m_FireTransform.forward * m_LaunchForce, ForceMode.Impulse);
            }

            // เล่นเสียงยิง
            FireClientRpc();

            // ทำลายกระสุนหลังจาก 3 วินาที
            Destroy(shellInstance, 3f);
        }

        [ClientRpc]
        private void FireClientRpc()
        {
            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.Play();
            }
        }
    }
}