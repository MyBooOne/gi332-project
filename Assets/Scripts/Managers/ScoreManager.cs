using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ScoreManager : NetworkBehaviour
{
    // Singleton instance
    public static ScoreManager Instance { get; private set; }
    
    [Header("ตั้งค่าเกม")]
    [SerializeField] private int scoreToWin = 5;
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI redTeamScoreText;
    [SerializeField] private TextMeshProUGUI blueTeamScoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button exitButton;
    
    // คะแนนทีม - ใช้ dictionary เพื่อรองรับหลายสี
    private readonly NetworkVariable<int> redTeamScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> blueTeamScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> greenTeamScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> yellowTeamScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> magentaTeamScore = new NetworkVariable<int>(0);
    
    // สถานะเกม
    private readonly NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<Color> winningTeamColor = new NetworkVariable<Color>(Color.white);
    
    // Dictionary สำหรับแปลงสีเป็นข้อความ
    private readonly Dictionary<Color, string> colorToNameThai = new Dictionary<Color, string>
    {
        { Color.red, "ทีมแดง" },
        { Color.blue, "ทีมน้ำเงิน" },
        { Color.green, "ทีมเขียว" },
        { Color.yellow, "ทีมเหลือง" },
        { Color.magenta, "ทีมม่วง" }
    };
    
    private void Awake()
    {
        // ตั้งค่า singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // ซ่อนพาเนลเกมโอเวอร์เริ่มต้น
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // สมัครสมาชิกเพื่อรับการเปลี่ยนแปลงคะแนน
        redTeamScore.OnValueChanged += (_, _) => UpdateScoreUI();
        blueTeamScore.OnValueChanged += (_, _) => UpdateScoreUI();
        greenTeamScore.OnValueChanged += (_, _) => UpdateScoreUI();
        yellowTeamScore.OnValueChanged += (_, _) => UpdateScoreUI();
        magentaTeamScore.OnValueChanged += (_, _) => UpdateScoreUI();
        
        // สมัครสมาชิกเพื่อรับการเปลี่ยนแปลงสถานะเกม
        isGameOver.OnValueChanged += OnGameOverChanged;
        
        // ตั้งค่าปุ่มออก
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitGame);
        }
        
        // อัพเดท UI เริ่มต้น
        UpdateScoreUI();
    }
    
    private void OnGameOverChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            ShowGameOver();
        }
    }
    
    private void UpdateScoreUI()
    {
        if (redTeamScoreText != null)
        {
            redTeamScoreText.text = $"ทีมแดง: {redTeamScore.Value}";
        }
        
        if (blueTeamScoreText != null)
        {
            blueTeamScoreText.text = $"ทีมน้ำเงิน: {blueTeamScore.Value}";
        }
        
        // อัพเดทคะแนนเพิ่มเติมได้ตามต้องการ
        
        // ตรวจสอบเงื่อนไขชนะ (เฉพาะบนเซิร์ฟเวอร์)
        if (IsServer)
        {
            CheckWinCondition();
        }
    }
    
    private void CheckWinCondition()
    {
        if (isGameOver.Value) return;
        
        // ตรวจสอบคะแนนแต่ละทีม
        if (redTeamScore.Value >= scoreToWin)
        {
            winningTeamColor.Value = Color.red;
            isGameOver.Value = true;
        }
        else if (blueTeamScore.Value >= scoreToWin)
        {
            winningTeamColor.Value = Color.blue;
            isGameOver.Value = true;
        }
        else if (greenTeamScore.Value >= scoreToWin)
        {
            winningTeamColor.Value = Color.green;
            isGameOver.Value = true;
        }
        else if (yellowTeamScore.Value >= scoreToWin)
        {
            winningTeamColor.Value = Color.yellow;
            isGameOver.Value = true;
        }
        else if (magentaTeamScore.Value >= scoreToWin)
        {
            winningTeamColor.Value = Color.magenta;
            isGameOver.Value = true;
        }
    }
    
    private void ShowGameOver()
    {
        if (gameOverPanel == null) return;
        
        gameOverPanel.SetActive(true);
        
        if (winnerText != null)
        {
            string teamName = "ทีม";
            if (colorToNameThai.TryGetValue(winningTeamColor.Value, out string name))
            {
                teamName = name;
            }
            
            winnerText.text = $"{teamName} ชนะ!";
        }
    }
    
    // เรียกจากที่อื่นเพื่อเพิ่มคะแนน (เช่น เมื่อฆ่าศัตรู)
    public void AddScore(Color teamColor)
    {
        if (!IsServer)
        {
            AddScoreServerRpc(
                teamColor.r,
                teamColor.g,
                teamColor.b,
                teamColor.a
            );
            return;
        }
        
        // เพิ่มคะแนนตามสี
        if (teamColor == Color.red)
        {
            redTeamScore.Value++;
        }
        else if (teamColor == Color.blue)
        {
            blueTeamScore.Value++;
        }
        else if (teamColor == Color.green)
        {
            greenTeamScore.Value++;
        }
        else if (teamColor == Color.yellow)
        {
            yellowTeamScore.Value++;
        }
        else if (teamColor == Color.magenta)
        {
            magentaTeamScore.Value++;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void AddScoreServerRpc(float r, float g, float b, float a)
    {
        AddScore(new Color(r, g, b, a));
    }
    
    public void ExitGame()
    {
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // กลับไปที่ฉากเมนูหลัก
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    
    // คืนค่าคะแนนปัจจุบันสำหรับสีที่ระบุ
    public int GetScore(Color teamColor)
    {
        if (teamColor == Color.red)
        {
            return redTeamScore.Value;
        }
        else if (teamColor == Color.blue)
        {
            return blueTeamScore.Value;
        }
        else if (teamColor == Color.green)
        {
            return greenTeamScore.Value;
        }
        else if (teamColor == Color.yellow)
        {
            return yellowTeamScore.Value;
        }
        else if (teamColor == Color.magenta)
        {
            return magentaTeamScore.Value;
        }
        
        return 0;
    }
}