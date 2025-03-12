using UnityEngine;
using Unity.Netcode;

namespace Complete
{
    public class CameraFixOnDeath : NetworkBehaviour
    {
        private Camera mainCamera;
        private Transform cameraTransform;
        private Vector3 deathPosition;
        private bool isDead = false;
        private TankHealth tankHealth;

        private void Awake()
        {
            tankHealth = GetComponent<TankHealth>();
            mainCamera = Camera.main;
            
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner && tankHealth != null)
            {
                // ติดตามการเปลี่ยนแปลงสถานะการตาย
                tankHealth.m_Dead.OnValueChanged += OnDeadValueChanged;
            }
        }

        // เมื่อสถานะการตายเปลี่ยน
        private void OnDeadValueChanged(bool oldValue, bool newValue)
        {
            if (IsOwner && newValue == true) // เมื่อตาย
            {
                // บันทึกตำแหน่งตาย
                deathPosition = transform.position;
                isDead = true;
            }
            else if (IsOwner && newValue == false && oldValue == true) // เมื่อเกิดใหม่
            {
                isDead = false;
            }
        }

        private void LateUpdate()
        {
            // ถ้าเป็นผู้เล่นท้องถิ่นและกำลังตาย
            if (IsOwner && isDead && mainCamera != null)
            {
                // ล็อคตำแหน่งกล้องให้อยู่เหนือจุดที่ตาย แต่ยังคงความสูงเดิมไว้
                Vector3 targetPosition = deathPosition;
                targetPosition.y = cameraTransform.position.y; // คงความสูงเดิมไว้
                
                // ตั้งตำแหน่งกล้องไปที่จุดตาย
                cameraTransform.position = new Vector3(
                    targetPosition.x,
                    cameraTransform.position.y,
                    targetPosition.z - 10 // ถอยกล้องออกมาเล็กน้อยเพื่อให้เห็นตัวละคร
                );
                
                // ให้กล้องมองไปที่จุดตาย
                cameraTransform.LookAt(new Vector3(
                    targetPosition.x,
                    0, // มองที่ระดับพื้น
                    targetPosition.z
                ));
            }
        }
    }
}