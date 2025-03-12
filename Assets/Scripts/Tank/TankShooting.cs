using Unity.Netcode;
using UnityEngine;

public class TankShooting : NetworkBehaviour
{
    [Header("กระสุน")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireRate = 0.5f;
    
    [Header("เสียง")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    
    private float nextFireTime = 0f;
    
    void Update()
    {
        // เฉพาะเจ้าของรถถังเท่านั้นที่ควบคุมการยิง
        if (!IsOwner) return;
        
        // ตรวจสอบการกดปุ่มยิง
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextFireTime)
        {
            // ตั้งเวลายิงครั้งต่อไป
            nextFireTime = Time.time + fireRate;
            
            // ส่งคำขอให้เซิร์ฟเวอร์สร้างกระสุน
            FireServerRpc();
        }
    }
    
    [ServerRpc]
    private void FireServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // คำนวณความเร็วของกระสุน
        Vector3 bulletVelocity = firePoint.forward * bulletSpeed;
        
        // สร้างกระสุนบนเซิร์ฟเวอร์
        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        
        // ตั้งค่ากระสุนก่อน Spawn
        Rigidbody bulletRb = bulletObject.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            // กำหนดความเร็วเริ่มต้น
            bulletRb.velocity = bulletVelocity;
        }
        
        // Spawn กระสุนบนเครือข่าย
        NetworkObject bulletNetObj = bulletObject.GetComponent<NetworkObject>();
        if (bulletNetObj != null)
        {
            bulletNetObj.Spawn();
            
            // ตั้งค่าข้อมูลกระสุน
            Bullet bullet = bulletObject.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Initialize(serverRpcParams.Receive.SenderClientId, bulletVelocity);
            }
        }
        
        // เล่นเสียงยิงบนทุกไคลเอนต์
        PlayShootSoundClientRpc();
    }
    
    [ClientRpc]
    private void PlayShootSoundClientRpc()
    {
        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }
    }
}