using UnityEngine;
using System.Collections;

public class PlayerCameraFollow : MonoBehaviour
{
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
        
        // พยายามค้นหารถถังที่ควรติดตาม
        StartCoroutine(FindPlayerTank());
    }
    
    IEnumerator FindPlayerTank()
    {
        // รอให้เกมเริ่มต้นและรถถังถูกสร้าง
        yield return new WaitForSeconds(0.5f);
        
        // ถ้ายังไม่มีเป้าหมาย ให้ค้นหารถถังที่มี tag "isLocalPlayer"
        if (target == null)
        {
            GameObject localPlayerTank = GameObject.FindGameObjectWithTag("isLocalPlayer");
            if (localPlayerTank != null)
            {
                target = localPlayerTank.transform;
                Debug.Log("กล้องพบรถถังของผู้เล่นท้องถิ่นด้วย tag: " + localPlayerTank.name);
                
                // ตั้งตำแหน่งเริ่มต้นของกล้อง
                UpdateCameraPosition();
            }
            else
            {
                Debug.LogWarning("ไม่พบรถถังที่มี tag isLocalPlayer จะลองอีกครั้งใน 1 วินาที");
                yield return new WaitForSeconds(1.0f);
                StartCoroutine(FindPlayerTank());
            }
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
    
    // เมธอดสำหรับตั้งค่าเป้าหมายโดยตรงจากภายนอก
    public void SetTarget(GameObject tankObject)
    {
        if (tankObject != null)
        {
            target = tankObject.transform;
            Debug.Log("กล้องตั้งค่าให้ติดตาม: " + tankObject.name);
        }
    }
}