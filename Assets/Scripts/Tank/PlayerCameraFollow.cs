using UnityEngine;
using System.Collections;

public class PlayerCameraFollow : MonoBehaviour
{
    // ตัวแปรสำหรับระบุรถถัง (ทั้งการ Host และการ Join)
    public string localPlayerTag = "LocalPlayer";
    
    // Transform ของรถถังที่จะตาม
    private Transform target;
    
    // ระยะห่างของกล้องจากรถถัง (ด้านหลังและด้านบน)
    public Vector3 positionOffset = new Vector3(0, 7, -7);
    
    // ความเร็วในการเคลื่อนที่ตามรถถัง
    public float moveSpeed = 5f;
    
    // ใช้เก็บ rotation ของกล้อง
    private Quaternion initialRotation;
    
    void Start()
    {
        // บันทึก rotation เริ่มต้นของกล้อง
        initialRotation = transform.rotation;
        
        // ค้นหารถถังของผู้เล่นท้องถิ่น
        StartCoroutine(FindLocalPlayerTank());
    }
    
    IEnumerator FindLocalPlayerTank()
    {
        // รอสักครู่ให้ระบบมัลติเพลเยอร์พร้อม
        yield return new WaitForSeconds(0.5f);
        
        // ค้นหารถถังที่มี Tag เป็น "LocalPlayer"
        GameObject playerTank = GameObject.FindGameObjectWithTag(localPlayerTag);
        
        if (playerTank != null)
        {
            target = playerTank.transform;
            Debug.Log("พบรถถังของผู้เล่นท้องถิ่น: " + playerTank.name);
            
            // ตั้งตำแหน่งเริ่มต้นของกล้อง
            UpdateCameraPosition();
        }
        else
        {
            Debug.LogWarning("ไม่พบรถถังที่มี Tag: " + localPlayerTag + " จะลองค้นหาใหม่ในอีก 1 วินาที");
            yield return new WaitForSeconds(1.0f);
            StartCoroutine(FindLocalPlayerTank());
        }
    }
    
    void LateUpdate()
    {
        // ถ้ายังไม่มีเป้าหมาย ให้ข้าม
        if (target == null)
        {
            return;
        }
        
        // อัปเดตตำแหน่งของกล้อง
        UpdateCameraPosition();
    }
    
    void UpdateCameraPosition()
    {
        // คำนวณตำแหน่งที่ต้องการ (คงที่เทียบกับตำแหน่งของรถถัง)
        Vector3 desiredPosition = target.position + positionOffset;
        
        // เคลื่อนที่กล้องไปยังตำแหน่งอย่างนุ่มนวล
        transform.position = Vector3.Lerp(transform.position, desiredPosition, moveSpeed * Time.deltaTime);
        
        // รักษา rotation ของกล้องให้คงที่
        transform.rotation = initialRotation;
    }
    
    // เมธอดสำหรับตั้งค่าเป้าหมายใหม่ (เรียกเมื่อผู้เล่นเกิดใหม่)
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    // สำหรับเรียกจากสคริปต์อื่นเพื่อตั้งค่าให้ตามรถถังนี้
    public void FollowThisTank(GameObject tank)
    {
        if (tank != null)
        {
            target = tank.transform;
            Debug.Log("กล้องกำลังตามรถถัง: " + tank.name);
        }
    }
}