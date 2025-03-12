using UnityEngine;
using Unity.Netcode;

namespace Complete
{
    public class Shell : NetworkBehaviour
    {
        [SerializeField] private float m_MaxDamage = 20f;           // ค่าดาเมจสูงสุด
        [SerializeField] private float m_ExplosionRadius = 2f;     // รัศมีการระเบิด
        [SerializeField] private ParticleSystem m_ExplosionParticles;
        [SerializeField] private AudioSource m_ExplosionAudio;
        
        private Rigidbody m_Rigidbody;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;

            // หาตำแหน่งที่ชน
            Vector3 explosionPosition = transform.position;

            // หารถถังทั้งหมดที่อยู่ในรัศมีการระเบิด
            Collider[] colliders = Physics.OverlapSphere(explosionPosition, m_ExplosionRadius);

            foreach (Collider collider in colliders)
            {
                // ตรวจสอบว่าเป็นรถถังหรือไม่
                TankHealth targetHealth = collider.GetComponent<TankHealth>();
                if (targetHealth != null)
                {
                    // คำนวณระยะห่างจากจุดระเบิด
                    float damage = CalculateDamage(collider.transform.position, explosionPosition);
                    
                    // ทำดาเมจกับรถถัง
                   // targetHealth.TakeDamage(damage);
                }
            }

            // เรียก effect ที่ client ทุกตัวก่อนทำลาย
            OnHitClientRpc(explosionPosition);
            
            // ทำลายกระสุน
            NetworkObject.Despawn(true);
        }

        private float CalculateDamage(Vector3 targetPosition, Vector3 explosionPosition)
        {
            // คำนวณระยะห่างจากจุดระเบิด
            float explosionDistance = Vector3.Distance(targetPosition, explosionPosition);
            
            // คำนวณดาเมจตามระยะห่าง (ยิ่งไกลยิ่งโดนน้อย)
            float relativeDistance = (m_ExplosionRadius - explosionDistance) / m_ExplosionRadius;
            
            // ทำให้ดาเมจอยู่ระหว่าง 0 ถึงค่าดาเมจสูงสุด
            float damage = relativeDistance * m_MaxDamage;
            
            return Mathf.Max(0f, damage);
        }

        [ClientRpc]
        private void OnHitClientRpc(Vector3 explosionPosition)
        {
            // แสดง particle effect
            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.position = explosionPosition;
                m_ExplosionParticles.Play();
            }

            // เล่นเสียงระเบิด
            if (m_ExplosionAudio != null)
            {
                m_ExplosionAudio.Play();
            }

            // ปิดการมองเห็นของกระสุน
            gameObject.SetActive(false);
        }
    }
}