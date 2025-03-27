using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

// ไฟล์ใหม่ - NameSyncFixer.cs
public class NameSyncFixer : NetworkBehaviour
{
    [SerializeField] private TMP_InputField nameInput; // อ้างอิงไปยัง input field ที่ผู้เล่นกรอกชื่อ
    
    // GameObject ที่มี TextMeshPro สำหรับชื่อในหน้า Lobby
    [SerializeField] private GameObject[] playerNameTexts;
    
    // อ้างอิงไปยัง TeamScoreManager
    private TeamScoreManager scoreManager;

    // เก็บค่าชื่อท้องถิ่น
    private string localPlayerName = "";
    
    void Start()
    {
        // หา TeamScoreManager ในฉาก
        scoreManager = FindObjectOfType<TeamScoreManager>();
        
        // ถ้าไม่มี nameInput ให้ลองหาใน scene
        if (nameInput == null)
        {
            nameInput = FindObjectOfType<TMP_InputField>();
        }
        
        // เริ่ม coroutine เพื่อซิงค์ชื่ออย่างต่อเนื่อง
        StartCoroutine(SyncNamesContinuously());
    }
    
    IEnumerator SyncNamesContinuously()
    {
        // รอให้ network เริ่มต้นก่อน
        yield return new WaitForSeconds(1f);
        
        while (true)
        {
            // ถ้า network spawn แล้ว
            if (IsSpawned)
            {
                // อ่านชื่อจาก input field
                if (nameInput != null && !string.IsNullOrEmpty(nameInput.text))
                {
                    localPlayerName = nameInput.text;
                }
                else if (string.IsNullOrEmpty(localPlayerName))
                {
                    // ถ้าไม่มีชื่อ ใช้ค่าเริ่มต้น
                    localPlayerName = "Player" + Random.Range(100, 999);
                }
                
                // ส่งชื่อไปยังเซิร์ฟเวอร์
                SendNameToServerRpc(NetworkManager.Singleton.LocalClientId, localPlayerName);
                
                // แก้ไขชื่อใน UI โดยตรง
                FixLocalUINames();
            }
            
            // รอก่อนทำงานรอบถัดไป
            yield return new WaitForSeconds(1f);
        }
    }
    
    // แก้ไขชื่อใน UI โดยตรง
    private void FixLocalUINames()
    {
        // แก้ไขชื่อ Text ในหน้า Lobby
        foreach (var textObj in playerNameTexts)
        {
            if (textObj != null)
            {
                TMP_Text tmpText = textObj.GetComponent<TMP_Text>();
                if (tmpText != null && tmpText.text == "New Text")
                {
                    tmpText.text = localPlayerName;
                }
            }
        }
        
        // ถ้ามี TMP_Text ที่ชื่อ "NameText" ที่มีเนื้อหาเป็น "New Text" ให้เปลี่ยนเป็นชื่อผู้เล่น
        TMP_Text[] allTexts = FindObjectsOfType<TMP_Text>();
        foreach (TMP_Text text in allTexts)
        {
            if (text.gameObject.name == "NameText" && text.text == "New Text")
            {
                text.text = localPlayerName;
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SendNameToServerRpc(ulong clientId, string playerName)
    {
        Debug.Log($"Server received name for client {clientId}: {playerName}");
        
        // เซิร์ฟเวอร์บันทึกชื่อและส่งไปให้ทุกไคลเอนต์
        BroadcastNameClientRpc(clientId, playerName);
        
        // อัปเดต TeamScoreManager โดยตรง
        if (scoreManager != null)
        {
            scoreManager.RegisterPlayerName(clientId, playerName);
            Debug.Log($"Updated score manager with name {playerName} for client {clientId}");
        }
    }
    
    [ClientRpc]
    private void BroadcastNameClientRpc(ulong clientId, string playerName)
    {
        Debug.Log($"Client received name broadcast for {clientId}: {playerName}");
        
        // อัปเดต TeamScoreManager บนทุกเครื่อง
        if (scoreManager != null)
        {
            scoreManager.RegisterPlayerName(clientId, playerName);
        }
        
        // ถ้าเป็นชื่อของเราเอง ให้อัปเดต UI โดยตรง
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            localPlayerName = playerName;
            FixLocalUINames();
        }
    }
}