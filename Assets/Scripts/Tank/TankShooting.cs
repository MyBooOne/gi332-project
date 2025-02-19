using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;  // เพิ่ม Netcode for GameObjects

namespace Complete
{
    public class TankShooting : NetworkBehaviour  // เปลี่ยนจาก MonoBehaviour เป็น NetworkBehaviour
    {
        // คงตัวแปรเดิมไว้
        [SerializeField] public int m_PlayerNumber = 1;
        [SerializeField] private Rigidbody m_Shell;
        [SerializeField] private Transform m_FireTransform;
        [SerializeField] private Slider m_AimSlider;
        [SerializeField] private AudioSource m_ShootingAudio;
        [SerializeField] private AudioClip m_ChargingClip;
        [SerializeField] private AudioClip m_FireClip;
        [SerializeField] private float m_MinLaunchForce = 15f;
        [SerializeField] private float m_MaxLaunchForce = 30f;
        [SerializeField] private float m_MaxChargeTime = 0.75f;

        private string m_FireButton;
        private float m_CurrentLaunchForce;
        private float m_ChargeSpeed;
        private bool m_Fired;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return; // ถ้าไม่ใช่เจ้าของ object ไม่ต้องทำอะไร
            
            m_CurrentLaunchForce = m_MinLaunchForce;
            m_FireButton = "Fire" + m_PlayerNumber;
            m_ChargeSpeed = (m_MaxLaunchForce - m_MinLaunchForce) / m_MaxChargeTime;
        }

        private void Update()
        {
            if (!IsOwner) return; // ตรวจสอบว่าเป็นเจ้าของหรือไม่

            HandleFiring();
        }

        private void HandleFiring()
        {
            m_AimSlider.value = m_MinLaunchForce;

            if (m_CurrentLaunchForce >= m_MaxLaunchForce && !m_Fired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;
                FireServerRpc();
            }
            else if (Input.GetButtonDown(m_FireButton))
            {
                m_Fired = false;
                m_CurrentLaunchForce = m_MinLaunchForce;
                PlayChargingAudioClientRpc();
            }
            else if (Input.GetButton(m_FireButton) && !m_Fired)
            {
                m_CurrentLaunchForce += m_ChargeSpeed * Time.deltaTime;
                m_AimSlider.value = m_CurrentLaunchForce;
            }
            else if (Input.GetButtonUp(m_FireButton) && !m_Fired)
            {
                FireServerRpc();
            }
        }

        [ServerRpc]
        private void FireServerRpc()
        {
            m_Fired = true;
            
            // สร้างกระสุนบน Server
            Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation);
            shellInstance.velocity = m_CurrentLaunchForce * m_FireTransform.forward;
            
            // Spawn กระสุนให้ทุก Client เห็น
            shellInstance.GetComponent<NetworkObject>().Spawn();
            
            // เล่นเสียงบนทุก Client
            PlayFireAudioClientRpc();
            
            m_CurrentLaunchForce = m_MinLaunchForce;
        }

        [ClientRpc]
        private void PlayChargingAudioClientRpc()
        {
            m_ShootingAudio.clip = m_ChargingClip;
            m_ShootingAudio.Play();
        }

        [ClientRpc]
        private void PlayFireAudioClientRpc()
        {
            m_ShootingAudio.clip = m_FireClip;
            m_ShootingAudio.Play();
        }
    }
}