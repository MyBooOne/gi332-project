using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace Complete
{
    public class TankHealth : NetworkBehaviour
    {
        [SerializeField] private float m_StartingHealth = 100f;
        [SerializeField] private Slider m_Slider;
        [SerializeField] private Image m_FillImage;
        [SerializeField] private Color m_FullHealthColor = Color.green;
        [SerializeField] private Color m_ZeroHealthColor = Color.red;
        [SerializeField] private GameObject m_ExplosionPrefab;

        private AudioSource m_ExplosionAudio;
        private ParticleSystem m_ExplosionParticles;
        public NetworkVariable<float> m_CurrentHealth = new NetworkVariable<float>();
        public NetworkVariable<bool> m_Dead = new NetworkVariable<bool>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_CurrentHealth.Value = m_StartingHealth;
                m_Dead.Value = false;
            }

            // ติดตามการเปลี่ยนแปลงของ health
            m_CurrentHealth.OnValueChanged += OnHealthChanged;
            
            SetupExplosion();
            SetHealthUI();
        }

        private void SetupExplosion()
        {
            var explosionInstance = Instantiate(m_ExplosionPrefab);
            m_ExplosionParticles = explosionInstance.GetComponent<ParticleSystem>();
            m_ExplosionAudio = explosionInstance.GetComponent<AudioSource>();
            explosionInstance.SetActive(false);
        }

        private void OnHealthChanged(float previousValue, float newValue)
        {
            SetHealthUI();
            
            if (newValue <= 0f && !m_Dead.Value)
            {
                OnDeathServerRpc();
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer) return;
            
            m_CurrentHealth.Value -= amount;
        }

        private void SetHealthUI()
        {
            m_Slider.value = m_CurrentHealth.Value;
            m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, m_CurrentHealth.Value / m_StartingHealth);
        }

        [ServerRpc]
        private void OnDeathServerRpc()
        {
            if (m_Dead.Value) return;
            
            m_Dead.Value = true;
            OnDeathClientRpc();
        }

        [ClientRpc]
        private void OnDeathClientRpc()
        {
            m_ExplosionParticles.transform.position = transform.position;
            m_ExplosionParticles.gameObject.SetActive(true);
            m_ExplosionParticles.Play();
            m_ExplosionAudio.Play();
            
            // เปลี่ยนจากการปิดใช้งานวัตถุเป็นแค่การปิดใช้งานบางส่วน
            // gameObject.SetActive(false);
            // แทนที่จะปิดวัตถุทั้งหมด เราจะซ่อนแค่ส่วนที่มองเห็นได้
            
            // ซ่อนโมเดล 3D ของรถถังแทน (ถ้ามี)
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
            
            // ปิดการใช้งานเสียง
            foreach (var audioSource in GetComponentsInChildren<AudioSource>())
            {
                audioSource.enabled = false;
            }
            
            // ปิดการใช้งานคอลไลเดอร์
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        // เพิ่มเมธอดนี้เพื่อการฟื้นฟูรถถัง
        public void ResetTank()
        {
            // เปิดใช้งานโมเดล
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = true;
            }
            
            // เปิดใช้งานเสียง
            foreach (var audioSource in GetComponentsInChildren<AudioSource>())
            {
                audioSource.enabled = true;
            }
            
            // เปิดใช้งานคอลไลเดอร์
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = true;
            }
        }
        
        // ฟังก์ชันสำหรับคืนค่า m_StartingHealth
        public float GetStartingHealth()
        {
            return m_StartingHealth;
        }
    }
}