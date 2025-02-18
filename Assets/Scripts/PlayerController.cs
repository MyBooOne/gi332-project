using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        // เช็คว่าเป็นตัวละครของเราหรือไม่
        if (!IsOwner) return;

        // รับ Input
        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ = 1f;
        if (Input.GetKey(KeyCode.S)) moveZ = -1f;
        if (Input.GetKey(KeyCode.A)) moveX = -1f;
        if (Input.GetKey(KeyCode.D)) moveX = 1f;

        Vector3 movement = new Vector3(moveX, 0f, moveZ);
        
        // Normalize vector เพื่อให้เคลื่อนที่เร็วเท่ากันทุกทิศทาง
        if (movement != Vector3.zero)
        {
            movement.Normalize();
            transform.Translate(movement * moveSpeed * Time.deltaTime);
        }
    }

    public override void OnNetworkSpawn()
    {
        // เปลี่ยนสีตัวละครตาม Owner
        if (IsOwner)
        {
            GetComponent<MeshRenderer>().material.color = Color.blue;
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Color.red;
        }
    }
}