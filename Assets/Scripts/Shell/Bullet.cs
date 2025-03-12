using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float bulletLifetime = 3f;
    
    private Rigidbody rb;
    private ulong shooterClientId;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    public override void OnNetworkSpawn()
    {
        // ทำลายกระสุนหลังจากเวลาที่กำหนด
        Destroy(gameObject, bulletLifetime);
    }
    
    // เรียกโดย TankShooting เพื่อตั้งค่ากระสุน
    public void Initialize(ulong clientId, Vector3 initialVelocity)
    {
        shooterClientId = clientId;
        
        // กำหนดความเร็วเริ่มต้น
        if (rb != null)
        {
            rb.velocity = initialVelocity;
            
            // เพิ่มการตั้งค่า Rigidbody
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            // สำคัญมาก: เพิ่มแรงเล็กน้อยเพื่อให้แน่ใจว่ากระสุนเคลื่อนที่
            rb.AddForce(initialVelocity.normalized * 0.1f, ForceMode.Impulse);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ตรวจสอบว่าเป็นเซิร์ฟเวอร์เท่านั้น (เซิร์ฟเวอร์จัดการความเสียหาย)
        if (!IsServer) return;
        
        // ตรวจสอบว่าชนกับรถถังหรือไม่
        Complete.TankHealth tankHealth = collision.gameObject.GetComponent<Complete.TankHealth>();
        if (tankHealth != null)
        {
            // ตรวจสอบว่าไม่ใช่การชนกับรถถังของตัวเอง
            NetworkObject targetNetObj = collision.gameObject.GetComponent<NetworkObject>();
            if (targetNetObj != null && targetNetObj.OwnerClientId != shooterClientId)
            {
                // ทำให้รถถังเสียหาย
                tankHealth.TakeDamage(damage);
                Debug.Log("กระสุนทำดาเมจ: " + damage);
            }
        }
        
        // แจ้งไคลเอนต์ทั้งหมดเรื่องการชน
        BulletHitClientRpc();
        
        // ทำลายกระสุน
        Destroy(gameObject);
    }
    
    [ClientRpc]
    private void BulletHitClientRpc()
    {
        // เล่นเสียงหรือเอฟเฟกต์การชน (ถ้ามี)
    }
}