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
            
            // สำคัญ: ลองตรวจสอบโดย Log ว่ากระสุนได้รับความเร็วจริงๆ
            Debug.Log("กำหนดความเร็วกระสุน: " + initialVelocity.magnitude);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ตรวจสอบว่าเป็นเซิร์ฟเวอร์เท่านั้น
        if (!IsServer) return;
        
        // บันทึก log ว่าชนกับอะไร
        Debug.Log("กระสุนชนกับ: " + collision.gameObject.name);
        
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
        
        // ทำลายกระสุน
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // เพิ่ม Update เพื่อตรวจสอบความเร็วของกระสุน
    private void Update()
    {
        if (rb != null && rb.velocity.magnitude < 0.1f)
        {
            // ถ้ากระสุนไม่เคลื่อนที่ บันทึก log
            Debug.LogWarning("กระสุนไม่เคลื่อนที่! ความเร็ว = " + rb.velocity.magnitude);
        }
    }
}