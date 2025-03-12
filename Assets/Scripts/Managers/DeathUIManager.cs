using UnityEngine;
using TMPro;

namespace Complete
{
    // สคริปต์นี้จะเป็น Singleton ที่จัดการ UI สำหรับการตาย
    public class DeathUIManager : MonoBehaviour
    {
        // Singleton instance
        public static DeathUIManager Instance { get; private set; }
        
        [Header("UI References")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private TextMeshProUGUI deathText;
        [SerializeField] private TextMeshProUGUI respawnCountdownText;
        
        // Flag เพื่อตรวจสอบว่าสร้าง UI แล้วหรือยัง
        private bool isUICreated = false;
        
        [Header("UI Settings")]
        [SerializeField] private Canvas gameCanvas;
        
        private void Awake()
        {
            // ตั้งค่า Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // ตรวจสอบว่า UI มีอยู่แล้วหรือไม่
            if (!isUICreated && gameCanvas != null)
            {
                CreateDeathUI();
            }
            
            // ซ่อนหน้าจอการตายตอนเริ่มเกม
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
        }
        
        // สร้าง UI elements หากไม่มีการกำหนดใน Inspector
        private void CreateDeathUI()
        {
            if (deathPanel == null)
            {
                // สร้าง Panel หลัก
                deathPanel = new GameObject("DeathPanel");
                deathPanel.transform.SetParent(gameCanvas.transform, false);
                
                // ตั้งค่า RectTransform
                RectTransform rectTransform = deathPanel.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                
                // เพิ่มภาพพื้นหลังสีดำ
                UnityEngine.UI.Image panelImage = deathPanel.AddComponent<UnityEngine.UI.Image>();
                panelImage.color = new Color(0, 0, 0, 0.7f); // สีดำโปร่งใส
                
                // สร้างข้อความ "You Died"
                GameObject deathTextObj = new GameObject("DeathText");
                deathTextObj.transform.SetParent(deathPanel.transform, false);
                
                RectTransform deathTextRect = deathTextObj.AddComponent<RectTransform>();
                deathTextRect.anchorMin = new Vector2(0.5f, 0.6f);
                deathTextRect.anchorMax = new Vector2(0.5f, 0.6f);
                deathTextRect.sizeDelta = new Vector2(300, 80);
                deathTextRect.anchoredPosition = Vector2.zero;
                
                deathText = deathTextObj.AddComponent<TextMeshProUGUI>();
                deathText.text = "You Died";
                deathText.fontSize = 48;
                deathText.color = Color.red;
                deathText.alignment = TextAlignmentOptions.Center;
                deathText.fontStyle = FontStyles.Bold;
                
                // สร้างข้อความนับถอยหลัง
                GameObject countdownTextObj = new GameObject("CountdownText");
                countdownTextObj.transform.SetParent(deathPanel.transform, false);
                
                RectTransform countdownTextRect = countdownTextObj.AddComponent<RectTransform>();
                countdownTextRect.anchorMin = new Vector2(0.5f, 0.4f);
                countdownTextRect.anchorMax = new Vector2(0.5f, 0.4f);
                countdownTextRect.sizeDelta = new Vector2(300, 60);
                countdownTextRect.anchoredPosition = Vector2.zero;
                
                respawnCountdownText = countdownTextObj.AddComponent<TextMeshProUGUI>();
                respawnCountdownText.text = "Respawning in 3";
                respawnCountdownText.fontSize = 36;
                respawnCountdownText.color = Color.white;
                respawnCountdownText.alignment = TextAlignmentOptions.Center;
            }
            
            // ซ่อนเริ่มต้น
            deathPanel.SetActive(false);
            isUICreated = true;
        }
        
        // แสดงหน้าจอการตาย
        public void ShowDeathScreen()
        {
            if (deathPanel != null)
            {
                deathPanel.SetActive(true);
                
                if (deathText != null)
                {
                    deathText.text = "You Died";
                }
            }
            else if (gameCanvas != null)
            {
                // สร้าง UI หากยังไม่มี
                CreateDeathUI();
                deathPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("DeathUIManager: ไม่พบ deathPanel และไม่มี gameCanvas สำหรับสร้างใหม่");
            }
        }
        
        // อัปเดตข้อความนับถอยหลัง
        public void UpdateRespawnCountdown(int seconds)
        {
            if (respawnCountdownText != null)
            {
                respawnCountdownText.text = $"Respawning in {seconds}";
            }
        }
        
        // ซ่อนหน้าจอการตาย
        public void HideDeathScreen()
        {
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
        }
    }
}