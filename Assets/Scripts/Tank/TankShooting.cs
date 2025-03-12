using UnityEngine;
using Unity.Netcode;

public class TankShooting : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private AudioSource shootAudio;
    
    private float nextFireTime = 0f;
    
    void Update()
    {
        // ตรวจสอบเฉพาะเจ้าของเท่านั้น
        if (!IsOwner) return;
        
        // ตรวจสอบการกดปุ่มยิง
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            FireServerRpc();
        }
    }
    
    [ServerRpc]
    private void FireServerRpc()
    {
        // สร้างกระสุน
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        
        // ตั้งค่าความเร็วกระสุน
        Rigidbody bulletRb = bulletObj.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.velocity = firePoint.forward * bulletSpeed;
        }
        
        // ตั้งค่าเจ้าของกระสุน
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetShooterInfo(OwnerClientId);
            Debug.Log($"[TankShooting] ยิงกระสุนโดย ClientId: {OwnerClientId}");
        }
        
        // Spawn กระสุนบนเครือข่าย
        NetworkObject bulletNetObj = bulletObj.GetComponent<NetworkObject>();
        if (bulletNetObj != null)
        {
            bulletNetObj.Spawn();
        }
        
        // เล่นเสียงยิง
        PlayShootSoundClientRpc();
    }
    
    [ClientRpc]
    private void PlayShootSoundClientRpc()
    {
        if (shootAudio != null)
        {
            shootAudio.Play();
        }
    }
}