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
        
        // Track the last player who damaged this tank
        public NetworkVariable<ulong> m_LastAttackerId = new NetworkVariable<ulong>(ulong.MaxValue);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_CurrentHealth.Value = m_StartingHealth;
                m_Dead.Value = false;
            }

            // Subscribe to health changes
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

        public void TakeDamage(float amount, ulong attackerId)
        {
            try
            {
                if (!IsServer) return;
        
                // Record the attacker ID
                m_LastAttackerId.Value = attackerId;
                Debug.Log($"[TankHealth] Taking damage: {amount} from attacker: {attackerId}, Current health: {m_CurrentHealth.Value}");
        
                m_CurrentHealth.Value -= amount;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TankHealth] Error in TakeDamage: {e.Message}");
            }
        }

        private void SetHealthUI()
        {
            m_Slider.value = m_CurrentHealth.Value;
            m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, m_CurrentHealth.Value / m_StartingHealth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnDeathServerRpc()
        {
            try
            {
                if (m_Dead.Value) return;
        
                Debug.Log($"[TankHealth] Tank died, last attacker: {m_LastAttackerId.Value}");
                m_Dead.Value = true;
        
                // Add score to killer if TeamScoreManager exists
                if (TeamScoreManager.Instance != null && m_LastAttackerId.Value != ulong.MaxValue)
                {
                    Debug.Log($"[TankHealth] Adding score to killer: {m_LastAttackerId.Value}");
                    TeamScoreManager.Instance.AddScore(m_LastAttackerId.Value);
                }
        
                OnDeathClientRpc();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TankHealth] Error in OnDeathServerRpc: {e.Message}");
            }
        }

        [ClientRpc]
        private void OnDeathClientRpc()
        {
            try
            {
                m_ExplosionParticles.transform.position = transform.position;
                m_ExplosionParticles.gameObject.SetActive(true);
                m_ExplosionParticles.Play();
                m_ExplosionAudio.Play();
        
                // Hide the tank visually instead of disabling the entire object
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = false;
                }
        
                // Disable audio
                foreach (var audioSource in GetComponentsInChildren<AudioSource>())
                {
                    audioSource.enabled = false;
                }
        
                // Disable colliders
                foreach (var collider in GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TankHealth] Error in OnDeathClientRpc: {e.Message}");
            }
        }

        // Method to restore the tank
        public void ResetTank()
        {
            // Enable renderers
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = true;
            }
            
            // Enable audio
            foreach (var audioSource in GetComponentsInChildren<AudioSource>())
            {
                audioSource.enabled = true;
            }
            
            // Enable colliders
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = true;
            }
        }
        
        // Get starting health value
        public float GetStartingHealth()
        {
            return m_StartingHealth;
        }
    }
}