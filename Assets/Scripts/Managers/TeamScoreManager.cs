using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TeamScoreManager : NetworkBehaviour
{
    // Singleton instance
    public static TeamScoreManager Instance { get; private set; }
    
    [Header("Game Settings")]
    [SerializeField] private int scoreToWin = 5;
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI team1ScoreText;
    [SerializeField] private TextMeshProUGUI team2ScoreText;
    [SerializeField] private TextMeshProUGUI team3ScoreText; // เพิ่มอ้างอิงถึง UI ของทีม 3
    [SerializeField] private TextMeshProUGUI team4ScoreText; // เพิ่มอ้างอิงถึง UI ของทีม 4
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button exitButton;
    
    // Team scores - we'll fix them to always increment by exactly 1
    private readonly NetworkVariable<int> team1Score = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
        
    private readonly NetworkVariable<int> team2Score = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Store player information
    private readonly Dictionary<ulong, int> playerTeams = new Dictionary<ulong, int>(); // 1 = team1, 2 = team2
    private readonly Dictionary<ulong, string> playerNames = new Dictionary<ulong, string>(); // เก็บชื่อผู้เล่น
    
    // Team colors
    private readonly NetworkVariable<Color32> team1Color = new NetworkVariable<Color32>(new Color32(255, 0, 0, 255),
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
        
    private readonly NetworkVariable<Color32> team2Color = new NetworkVariable<Color32>(new Color32(0, 0, 255, 255),
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Team names (to be properly synced)
    private readonly NetworkVariable<NetworkString> team1Name = new NetworkVariable<NetworkString>(new NetworkString("Red Team"),
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
        
    private readonly NetworkVariable<NetworkString> team2Name = new NetworkVariable<NetworkString>(new NetworkString("Blue Team"),
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // เพิ่มตัวแปรเพื่อเก็บรายชื่อผู้เล่นในแต่ละทีม
    private readonly Dictionary<int, List<ulong>> teamPlayers = new Dictionary<int, List<ulong>>(); 
    
    // Game state
    private readonly NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
        
    private readonly NetworkVariable<int> winningTeamId = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
        
    private readonly NetworkVariable<NetworkString> winningTeamName = new NetworkVariable<NetworkString>(new NetworkString(""),
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Flag to prevent double scoring
    private bool processingScore = false;
    
    // Structure to hold NetworkString for Unity Netcode
    public struct NetworkString : INetworkSerializable
    {
        private string value;
        
        public NetworkString(string value)
        {
            this.value = value;
        }
        
        public string Value
        {
            get { return value ?? string.Empty; }
            set { this.value = value; }
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref value);
        }
        
        public override string ToString()
        {
            return Value;
        }
        
        public static implicit operator string(NetworkString s) => s.Value;
        public static implicit operator NetworkString(string s) => new NetworkString(s);
    }
    
    private void Awake()
    {
        // Setup singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Hide game over panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // เตรียมรายชื่อผู้เล่นสำหรับแต่ละทีม
        teamPlayers[1] = new List<ulong>();
        teamPlayers[2] = new List<ulong>();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to score and color changes
        team1Score.OnValueChanged += (_, _) => UpdateScoreUI();
        team2Score.OnValueChanged += (_, _) => UpdateScoreUI();
        team1Color.OnValueChanged += (_, _) => UpdateScoreUI();
        team2Color.OnValueChanged += (_, _) => UpdateScoreUI();
        team1Name.OnValueChanged += (prev, current) => {
            Debug.Log($"Team1Name changed from {prev} to {current}");
            UpdateScoreUI();
        };
        team2Name.OnValueChanged += (prev, current) => {
            Debug.Log($"Team2Name changed from {prev} to {current}");
            UpdateScoreUI();
        };
        winningTeamName.OnValueChanged += (_, _) => UpdateWinnerText();
        isGameOver.OnValueChanged += OnGameOverChanged;
        
        // Setup exit button
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitGame);
        }
        
        // Force an immediate UI update regardless of who we are
        InitializeUI();
        
        // Setup teams if we're the server
        if (IsServer)
        {
            // Immediate setup
            SetupTeamColors();
            
            // Then check again a few times
            StartCoroutine(CheckTeamColorsMultipleTimes());
        }
        
        // เริ่ม coroutine เพื่อซิงค์ชื่อทีมอย่างต่อเนื่อง
        StartCoroutine(ContinuousNameSync());
        
        Debug.Log($"[TeamScoreManager] OnNetworkSpawn - IsServer: {IsServer}, IsOwner: {IsOwner}, IsClient: {IsClient}");
    }
    
    // เพิ่ม coroutine เพื่อซิงค์ชื่อทีมอย่างต่อเนื่อง
    private IEnumerator ContinuousNameSync()
    {
        while (IsSpawned)
        {
            // ซิงค์ข้อมูลทุกๆ 2 วินาที
            yield return new WaitForSeconds(2.0f);
            
            if (IsServer)
            {
                SyncTeamInfoToClients();
            }
            
            // อัปเดต UI ทุกเครื่อง
            UpdateScoreUI();
        }
    }
    
    // แก้ไขการจัดการชื่อผู้เล่นเพื่อสนับสนุนหลายผู้เล่นต่อทีม
    public void ForceUpdateName(ulong clientId, string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        
        Debug.Log($"[TeamScoreManager] ForceUpdateName - ClientId: {clientId}, Name: {playerName}");
        
        // เก็บชื่อผู้เล่นไว้ใน Dictionary
        playerNames[clientId] = playerName;
        
        // กำหนดทีม (ถ้ายังไม่มี)
        if (!playerTeams.ContainsKey(clientId))
        {
            // กำหนดทีมตามรูปแบบ: clientId คู่ = ทีม 1, clientId คี่ = ทีม 2
            int teamId = (clientId % 2 == 0) ? 1 : 2;
            AssignPlayerToTeam(clientId, teamId);
        }
        
        // อัปเดตชื่อทีม
        if (IsServer)
        {
            UpdateTeamNames();
        }
        
        // ถ้าเป็นเซิร์ฟเวอร์ ให้ส่งข้อมูลไปยังทุกไคลเอนต์
        if (IsServer)
        {
            SyncTeamInfoToClients();
        }
        else
        {
            // หากเป็นไคลเอนต์ ขอให้เซิร์ฟเวอร์อัปเดตชื่อ
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                RequestNameUpdateServerRpc(clientId, playerName);
            }
        }
    }
    
    // เพิ่มเมธอดสำหรับจัดการผู้เล่นเข้าทีม
    private void AssignPlayerToTeam(ulong clientId, int teamId)
    {
        // เก็บข้อมูลทีมของผู้เล่น
        playerTeams[clientId] = teamId;
        
        // เพิ่มผู้เล่นเข้าในรายชื่อของทีม
        if (!teamPlayers.ContainsKey(teamId))
        {
            teamPlayers[teamId] = new List<ulong>();
        }
        
        if (!teamPlayers[teamId].Contains(clientId))
        {
            teamPlayers[teamId].Add(clientId);
        }
        
        Debug.Log($"[TeamScoreManager] Assigned player {clientId} to team {teamId}");
    }
    
    // เพิ่มเมธอดสำหรับอัปเดตชื่อทีมจากผู้เล่นทั้งหมด
    private void UpdateTeamNames()
    {
        if (!IsServer) return;
        
        // อัปเดตชื่อทีม 1
        if (teamPlayers.ContainsKey(1) && teamPlayers[1].Count > 0)
        {
            string teamName = "Team 1";
            
            // ตั้งชื่อตามผู้เล่นคนแรก
            if (teamPlayers[1].Count > 0 && playerNames.TryGetValue(teamPlayers[1][0], out string playerName))
            {
                teamName = playerName + "'s Team";
            }
            
            team1Name.Value = teamName;
        }
        
        // อัปเดตชื่อทีม 2
        if (teamPlayers.ContainsKey(2) && teamPlayers[2].Count > 0)
        {
            string teamName = "Team 2";
            
            // ตั้งชื่อตามผู้เล่นคนแรก
            if (teamPlayers[2].Count > 0 && playerNames.TryGetValue(teamPlayers[2][0], out string playerName))
            {
                teamName = playerName + "'s Team";
            }
            
            team2Name.Value = teamName;
        }
        
        Debug.Log($"[TeamScoreManager] Updated team names - Team1: {team1Name.Value}, Team2: {team2Name.Value}");
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestNameUpdateServerRpc(ulong clientId, string playerName)
    {
        // ให้เซิร์ฟเวอร์ทำการอัปเดตและซิงค์ชื่อผู้เล่น
        ForceUpdateName(clientId, playerName);
    }
    
    private void InitializeUI()
    {
        // Set initial UI based on current values
        if (team1ScoreText != null)
        {
            team1ScoreText.text = $"{team1Name.Value}: {team1Score.Value}";
            team1ScoreText.color = team1Color.Value;
        }
        
        if (team2ScoreText != null)
        {
            team2ScoreText.text = $"{team2Name.Value}: {team2Score.Value}";
            team2ScoreText.color = team2Color.Value;
        }
        
        // อัปเดต UI สำหรับทีม 3 และ 4 (ซึ่งจะแสดงข้อมูลเหมือนทีม 1 และ 2)
        if (team3ScoreText != null)
        {
            team3ScoreText.text = $"{team1Name.Value}: {team1Score.Value}";
            team3ScoreText.color = team1Color.Value;
        }
        
        if (team4ScoreText != null)
        {
            team4ScoreText.text = $"{team2Name.Value}: {team2Score.Value}";
            team4ScoreText.color = team2Color.Value;
        }
    }
    
    private IEnumerator CheckTeamColorsMultipleTimes()
    {
        // Check several times with increasing intervals
        yield return new WaitForSeconds(0.5f);
        SetupTeamColors();
        
        yield return new WaitForSeconds(1f);
        SetupTeamColors();
        
        yield return new WaitForSeconds(1.5f);
        SetupTeamColors();
        
        // Then keep checking regularly
        while (true)
        {
            yield return new WaitForSeconds(3f);
            SetupTeamColors();
        }
    }
    
    // แก้ไขเมธอดการตั้งค่าทีมให้รองรับหลายผู้เล่นต่อทีม
    private void SetupTeamColors()
    {
        Debug.Log("[TeamScoreManager] Setting up team colors");
        
        bool colorChanged = false;
        
        // Find all players
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log($"[TeamScoreManager] Checking player ClientId: {client.ClientId}");
            
            if (client.PlayerObject != null)
            {
                // Get renderers and check for color
                var renderers = client.PlayerObject.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0 && renderers[0].material != null)
                {
                    Color playerColor = renderers[0].material.color;
                    Debug.Log($"[TeamScoreManager] Found player color - ClientId: {client.ClientId}, Color: {playerColor}");
                    
                    // กำหนดทีมตามรูปแบบ: clientId คู่ = ทีม 1, clientId คี่ = ทีม 2
                    int teamId = (client.ClientId % 2 == 0) ? 1 : 2;
                    AssignPlayerToTeam(client.ClientId, teamId);
                    
                    // อัปเดตสีทีมถ้าเป็นผู้เล่นคนแรกในทีม
                    if (teamPlayers.ContainsKey(teamId) && teamPlayers[teamId].Count > 0 && teamPlayers[teamId][0] == client.ClientId)
                    {
                        if (teamId == 1 && ColorIsDifferent(team1Color.Value, playerColor))
                        {
                            team1Color.Value = playerColor;
                            colorChanged = true;
                            Debug.Log($"[TeamScoreManager] Updated Team 1 color to {playerColor}");
                        }
                        else if (teamId == 2 && ColorIsDifferent(team2Color.Value, playerColor))
                        {
                            team2Color.Value = playerColor;
                            colorChanged = true;
                            Debug.Log($"[TeamScoreManager] Updated Team 2 color to {playerColor}");
                        }
                    }
                }
            }
        }
        
        // อัปเดตชื่อทีม
        UpdateTeamNames();
        
        // Only sync colors if they changed
        if (colorChanged)
        {
            SyncTeamInfoToClients();
        }
    }
    
    private bool ColorIsDifferent(Color a, Color b)
    {
        // Check if colors are significantly different
        return Mathf.Abs(a.r - b.r) > 0.1f || 
               Mathf.Abs(a.g - b.g) > 0.1f || 
               Mathf.Abs(a.b - b.b) > 0.1f;
    }
    
    public string GetTeam1Name()
    {
        return team1Name.Value;
    }

    public string GetTeam2Name()
    {
        return team2Name.Value;
    }

    // เมธอดสำหรับตั้งค่าชื่อทีมโดยตรง (สำหรับไคลเอนต์)
    public void SetTeamNames(string team1NameStr, string team2NameStr)
    {
        Debug.Log($"[TeamScoreManager] SetTeamNames called - Team1: {team1NameStr}, Team2: {team2NameStr}");
        
        // ไคลเอนต์ไม่สามารถแก้ไข NetworkVariable ได้โดยตรง
        // แต่สามารถอัปเดต UI ท้องถิ่นได้
        if (team1ScoreText != null)
        {
            team1ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
        }
        
        if (team2ScoreText != null)
        {
            team2ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
        }
        
        // อัปเดต UI สำหรับทีม 3 และ 4 (ซึ่งจะแสดงข้อมูลเหมือนทีม 1 และ 2)
        if (team3ScoreText != null)
        {
            team3ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
        }
        
        if (team4ScoreText != null)
        {
            team4ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
        }
        
        // อัปเดต NetworkVariable บนเซิร์ฟเวอร์
        if (IsServer)
        {
            team1Name.Value = team1NameStr;
            team2Name.Value = team2NameStr;
            
            // ซิงค์ข้อมูลไปยังทุกไคลเอนต์
            SyncTeamInfoToClients();
        }
    }
    
    private void SyncTeamInfoToClients()
    {
        Color32 t1 = team1Color.Value;
        Color32 t2 = team2Color.Value;
        string t1Name = team1Name.Value;
        string t2Name = team2Name.Value;
        
        Debug.Log($"[TeamScoreManager] Syncing team info - Team1: {t1Name} {t1}, Team2: {t2Name} {t2}");
        
        SyncTeamInfoClientRpc(
            t1.r, t1.g, t1.b, 
            t2.r, t2.g, t2.b,
            t1Name, 
            t2Name
        );
    }
    
    [ClientRpc]
    private void SyncTeamInfoClientRpc(
        byte team1R, byte team1G, byte team1B,
        byte team2R, byte team2G, byte team2B,
        string team1NameStr, 
        string team2NameStr)
    {
        Color32 t1Color = new Color32(team1R, team1G, team1B, 255);
        Color32 t2Color = new Color32(team2R, team2G, team2B, 255);
        
        Debug.Log($"[TeamScoreManager] Received team info from server - " +
                  $"Team1: {team1NameStr} RGB({team1R},{team1G},{team1B}), " +
                  $"Team2: {team2NameStr} RGB({team2R},{team2G},{team2B})");
        
        // Update UI with new info
        if (team1ScoreText != null)
        {
            team1ScoreText.color = t1Color;
            team1ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
        }
        
        if (team2ScoreText != null)
        {
            team2ScoreText.color = t2Color;
            team2ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
        }
        
        // อัปเดต UI สำหรับทีม 3 และ 4 (ซึ่งจะแสดงข้อมูลเหมือนทีม 1 และ 2)
        if (team3ScoreText != null)
        {
            team3ScoreText.color = t1Color;
            team3ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
        }
        
        if (team4ScoreText != null)
        {
            team4ScoreText.color = t2Color;
            team4ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
        }
    }
    
    private string GetTeamNameFromColor(Color color)
    {
        // Simple color detection based on main component
        if (color.r > 0.7f && color.g < 0.3f && color.b < 0.3f)
        {
            return "Red Team";
        }
        else if (color.r < 0.3f && color.g < 0.3f && color.b > 0.7f)
        {
            return "Blue Team";
        }
        else if (color.r < 0.3f && color.g > 0.7f && color.b < 0.3f)
        {
            return "Green Team";
        }
        else if (color.r > 0.7f && color.g > 0.7f && color.b < 0.3f)
        {
            return "Yellow Team";
        }
        else if (color.r > 0.7f && color.g < 0.3f && color.b > 0.7f)
        {
            return "Magenta Team";
        }
        
        // If no specific color matches, check which component is dominant
        if (color.r > color.g && color.r > color.b)
        {
            return "Red Team";
        }
        else if (color.g > color.r && color.g > color.b)
        {
            return "Green Team";
        }
        else if (color.b > color.r && color.b > color.g)
        {
            return "Blue Team";
        }
        else if (color.r > 0.5f && color.g > 0.5f && color.b < 0.5f)
        {
            return "Yellow Team";
        }
        
        // Default fallback
        return "Unknown Team";
    }
    
    private void OnGameOverChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            Debug.Log("[TeamScoreManager] Game over - showing winner screen");
            ShowGameOver();
        }
    }
    
    private void UpdateWinnerText()
    {
        if (!isGameOver.Value || winnerText == null) return;
        
        winnerText.text = $"{winningTeamName.Value} Wins!";
        
        // Set appropriate color
        if (winningTeamId.Value == 1)
        {
            winnerText.color = team1Color.Value;
        }
        else if (winningTeamId.Value == 2)
        {
            winnerText.color = team2Color.Value;
        }
        
        Debug.Log($"[TeamScoreManager] Updated winner text to: {winnerText.text} with color {winnerText.color}");
    }
    
    // แก้ไขเมธอดอัปเดต UI ให้รองรับทีม 3 และ 4
    private void UpdateScoreUI()
    {
        // Update team 1 (Host) score text
        if (team1ScoreText != null)
        {
            team1ScoreText.text = $"{team1Name.Value}: {team1Score.Value}";
            team1ScoreText.color = team1Color.Value;
        }
        
        // Update team 2 (Join) score text
        if (team2ScoreText != null)
        {
            team2ScoreText.text = $"{team2Name.Value}: {team2Score.Value}";
            team2ScoreText.color = team2Color.Value;
        }
        
        // อัปเดต UI สำหรับทีม 3 และ 4 (ซึ่งจะแสดงข้อมูลเหมือนทีม 1 และ 2)
        if (team3ScoreText != null)
        {
            team3ScoreText.text = $"{team1Name.Value}: {team1Score.Value}";
            team3ScoreText.color = team1Color.Value;
        }
        
        if (team4ScoreText != null)
        {
            team4ScoreText.text = $"{team2Name.Value}: {team2Score.Value}";
            team4ScoreText.color = team2Color.Value;
        }
        
        // Check win condition (server only)
        if (IsServer)
        {
            CheckWinCondition();
        }
        
        Debug.Log($"[TeamScoreManager] Updated UI - Team1: {team1Name.Value} ({team1Score.Value}), Team2: {team2Name.Value} ({team2Score.Value})");
    }
    
    private void CheckWinCondition()
    {
        if (isGameOver.Value) return;
        
        // Check if either team reached the winning score
        if (team1Score.Value >= scoreToWin)
        {
            Debug.Log("[TeamScoreManager] Team 1 has won!");
            winningTeamId.Value = 1;
            winningTeamName.Value = team1Name.Value;
            isGameOver.Value = true;
            ShowGameOverClientRpc(1, team1Name.Value);
        }
        else if (team2Score.Value >= scoreToWin)
        {
            Debug.Log("[TeamScoreManager] Team 2 has won!");
            winningTeamId.Value = 2;
            winningTeamName.Value = team2Name.Value;
            isGameOver.Value = true;
            ShowGameOverClientRpc(2, team2Name.Value);
        }
    }
    
    [ClientRpc]
    private void ShowGameOverClientRpc(int winningTeam, string winningName)
    {
        // ไคลเอนต์ไม่สามารถเปลี่ยนค่า NetworkVariable ได้โดยตรง
        // จึงไม่ต้องมีบรรทัดนี้: winningTeamId.Value = winningTeam;
        // และไม่ต้องมีบรรทัดนี้: winningTeamName.Value = winningName;
        
        // แต่เก็บค่าไว้ใช้ท้องถิ่น
        Debug.Log($"[TeamScoreManager] Showing game over for team {winningTeam}: {winningName}");
        ShowGameOver();
    }
    
    private void ShowGameOver()
    {
        Debug.Log("[TeamScoreManager] Showing game over screen");
        
        if (gameOverPanel == null)
        {
            Debug.LogError("[TeamScoreManager] gameOverPanel not found - cannot show winner screen");
            return;
        }
        
        gameOverPanel.SetActive(true);
        
        if (winnerText != null)
        {
            Color winnerColor = winningTeamId.Value == 1 ? team1Color.Value : team2Color.Value;
            string teamName = winningTeamName.Value;
            
            winnerText.text = $"{teamName} Wins!";
            winnerText.color = winnerColor;
            Debug.Log($"[TeamScoreManager] Showing winner: {teamName} with color {winnerColor}");
        }
        else
        {
            Debug.LogError("[TeamScoreManager] winnerText not found - cannot show winner message");
        }
    }
    
    // Call this from other scripts to add score when enemy is killed
    public void AddScore(ulong killerClientId)
    {
        Debug.Log($"[TeamScoreManager] AddScore called for player ClientId: {killerClientId}");
        
        if (!IsServer)
        {
            // Send request to server
            AddScoreServerRpc(killerClientId);
            return;
        }
        
        // Prevent double scoring with a flag
        if (processingScore)
        {
            Debug.Log("[TeamScoreManager] Already processing a score, skipping");
            return;
        }
        
        processingScore = true;
        
        // Look up the team of the killer - ALWAYS increase score by exactly 1
        if (!playerTeams.TryGetValue(killerClientId, out int teamId))
        {
            // ถ้ายังไม่มีทีม ให้กำหนดตามรูปแบบ: clientId คู่ = ทีม 1, clientId คี่ = ทีม 2
            teamId = (killerClientId % 2 == 0) ? 1 : 2;
            AssignPlayerToTeam(killerClientId, teamId);
        }
        
        Debug.Log($"[TeamScoreManager] Player {killerClientId} is in Team {teamId}");
        
        if (teamId == 1)
        {
            // FORCE exactly 1 point increment
            int currentScore = team1Score.Value;
            team1Score.Value = currentScore + 1;
            Debug.Log($"[TeamScoreManager] Added 1 score to Team 1: {currentScore} -> {team1Score.Value}");
        }
        else if (teamId == 2)
        {
            // FORCE exactly 1 point increment
            int currentScore = team2Score.Value;
            team2Score.Value = currentScore + 1;
            Debug.Log($"[TeamScoreManager] Added 1 score to Team 2: {currentScore} -> {team2Score.Value}");
        }
        
        // Reset the flag after a short delay
        StartCoroutine(ResetProcessingFlag());
    }
    
    private IEnumerator ResetProcessingFlag()
    {
        yield return new WaitForSeconds(0.5f);
        processingScore = false;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void AddScoreServerRpc(ulong killerClientId)
    {
        AddScore(killerClientId);
    }
    
    public void ExitGame()
    {
        Debug.Log("[TeamScoreManager] Exiting game");
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Return to main menu
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    
    // แก้ไขเมธอดลงทะเบียนชื่อผู้เล่นให้รองรับหลายผู้เล่นต่อทีม
    public void RegisterPlayerName(ulong clientId, string playerName)
    {
        Debug.Log($"[TeamScoreManager] RegisterPlayerName - ClientId: {clientId}, Name: {playerName}");
        
        // เก็บชื่อผู้เล่นไว้
        playerNames[clientId] = playerName;
    
        // ถ้ายังไม่มีทีม ให้กำหนดตามรูปแบบ: clientId คู่ = ทีม 1, clientId คี่ = ทีม 2
        if (!playerTeams.ContainsKey(clientId))
        {
            int teamId = (clientId % 2 == 0) ? 1 : 2;
            AssignPlayerToTeam(clientId, teamId);
        }
    
        // อัปเดตชื่อทีม (เฉพาะเซิร์ฟเวอร์)
        if (IsServer)
        {
            UpdateTeamNames();
        }
    
        // อัปเดต UI
        UpdateScoreUI();
    
        // ถ้าเป็นเซิร์ฟเวอร์ ให้ซิงค์ข้อมูลไปยังทุกไคลเอนต์
        if (IsServer)
        {
            SyncTeamInfoToClients();
        }
    }

    public void ResetGameState()
    {
        if (IsServer)
        {
            team1Score.Value = 0;
            team2Score.Value = 0;
            isGameOver.Value = false;
            winningTeamId.Value = 0;
            winningTeamName.Value = "";
        }

        if (gameOverPanel != null) 
        {
            gameOverPanel.SetActive(false);
        }
        
        UpdateScoreUI();
        
        // ส่งข้อมูลทีมอีกครั้งเมื่อเริ่มเกมใหม่
        if (IsServer)
        {
            SyncTeamInfoToClients();
        }
    }
    
    // For debugging
    public void DebugInfo()
    {
        Debug.Log("======= TeamScoreManager Debug Info =======");
        Debug.Log($"Team1 Score: {team1Score.Value}, Name: {team1Name.Value}, Color: {team1Color.Value}");
        Debug.Log($"Team2 Score: {team2Score.Value}, Name: {team2Name.Value}, Color: {team2Color.Value}");
        Debug.Log($"GameOver: {isGameOver.Value}, WinningTeam: {winningTeamId.Value}, WinningName: {winningTeamName.Value}");
        Debug.Log($"Score To Win: {scoreToWin}");
        
        Debug.Log("Player Teams:");
        foreach (var pair in playerTeams)
        {
            Debug.Log($"  Player {pair.Key}: Team {pair.Value}");
        }
        
        Debug.Log("Team Players:");
        foreach (var pair in teamPlayers)
        {
            string playerList = string.Join(", ", pair.Value);
            Debug.Log($"  Team {pair.Key}: Players [{playerList}]");
        }
        
        Debug.Log("Player Names:");
        foreach (var pair in playerNames)
        {
            Debug.Log($"  Player {pair.Key}: Name {pair.Value}");
        }
        
        Debug.Log("UI Components:");
        Debug.Log($"  team1ScoreText: {(team1ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  team2ScoreText: {(team2ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  team3ScoreText: {(team3ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  team4ScoreText: {(team4ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  gameOverPanel: {(gameOverPanel != null ? "Found" : "Missing")}");
        Debug.Log($"  winnerText: {(winnerText != null ? "Found" : "Missing")}");
        Debug.Log($"  exitButton: {(exitButton != null ? "Found" : "Missing")}");
        Debug.Log("===========================================");
    }
    
    // เพิ่มเมธอดสำหรับการซิงค์ข้อมูลชื่อทีมแบบทันที
    public void SyncTeamNamesNow()
    {
        if (IsServer)
        {
            SyncTeamInfoToClients();
        }
        else
        {
            // ขอข้อมูลทีมจากเซิร์ฟเวอร์
            RequestTeamInfoServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestTeamInfoServerRpc(ulong clientId)
    {
        // ส่งข้อมูลทีมไปให้ไคลเอนต์ที่ขอ
        SendTeamInfoToClientRpc(clientId, team1Name.Value, team2Name.Value);
    }
    
    [ClientRpc]
    private void SendTeamInfoToClientRpc(ulong targetClientId, string team1NameStr, string team2NameStr)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            // อัปเดต UI โดยตรง
            if (team1ScoreText != null)
            {
                team1ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
            }
            
            if (team2ScoreText != null)
            {
                team2ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
            }
            
            // อัปเดต UI สำหรับทีม 3 และ 4
            if (team3ScoreText != null)
            {
                team3ScoreText.text = $"{team1NameStr}: {team1Score.Value}";
            }
            
            if (team4ScoreText != null)
            {
                team4ScoreText.text = $"{team2NameStr}: {team2Score.Value}";
            }
            
            Debug.Log($"[TeamScoreManager] Received direct team info - Team1: {team1NameStr}, Team2: {team2NameStr}");
        }
    }
    
    // เมธอดสำหรับบังคับอัปเดต UI ทันที
    public void ForceUpdateUI()
    {
        UpdateScoreUI();
    }
    
    // เมธอดสำหรับทดสอบการเชื่อมต่อ
    public void TestConnection()
    {
        Debug.Log("[TeamScoreManager] Testing connection...");
        
        if (IsServer)
        {
            TestConnectionClientRpc();
        }
        else
        {
            TestConnectionServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void TestConnectionServerRpc(ulong clientId)
    {
        Debug.Log($"[TeamScoreManager] Server received test from client: {clientId}");
        TestConnectionResponseClientRpc(clientId);
    }
    
    [ClientRpc]
    private void TestConnectionClientRpc()
    {
        Debug.Log("[TeamScoreManager] Client received test from server");
    }
    
    [ClientRpc]
    private void TestConnectionResponseClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log("[TeamScoreManager] Client received test response from server");
        }
    }
}