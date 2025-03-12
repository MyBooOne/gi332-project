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
            
            // เล่นเสียงยิงทันที
            if (audioSource != null && shootSound != null)
            {
                audioSource.PlayOneShot(shootSound);
            }
            
            // ส่งคำขอให้ server สร้างกระสุน
            FireServerRpc();
            
            // บันทึก log เพื่อตรวจสอบ
            Debug.Log("ส่งคำขอยิง!");
        }
    }
    
    [ServerRpc]
    private void FireServerRpc(ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("Server ได้รับคำขอยิง");
        
        // คำนวณความเร็วของกระสุน
        Vector3 bulletVelocity = firePoint.forward * bulletSpeed;
        
        // สร้างกระสุนบนเซิร์ฟเวอร์
        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        
        // ตั้งค่า Rigidbody ของกระสุน
        Rigidbody bulletRb = bulletObject.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            // กำหนดความเร็วเริ่มต้น
            bulletRb.velocity = bulletVelocity;
            
            // ให้แน่ใจว่ากระสุนไม่ใช้ gravity และไม่เป็น kinematic
            bulletRb.useGravity = false;
            bulletRb.isKinematic = false;
            
            // ลด mass ลงเพื่อป้องกัน knockback
            bulletRb.mass = 0.01f;
            
            Debug.Log("ตั้งค่าความเร็วกระสุน: " + bulletVelocity.magnitude);
        }
        else
        {
            Debug.LogError("ไม่พบ Rigidbody บนกระสุน!");
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
            else
            {
                Debug.LogError("ไม่พบ Bullet script บนกระสุน!");
            }
        }
        else
        {
            Debug.LogError("ไม่พบ NetworkObject บนกระสุน!");
        }
        
        // เล่นเสียงยิงบนทุกไคลเอนต์
        PlayShootSoundClientRpc(serverRpcParams.Receive.SenderClientId);
    }
    
    [ClientRpc]
    private void PlayShootSoundClientRpc(ulong shooterClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != shooterClientId && audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }
    }
}