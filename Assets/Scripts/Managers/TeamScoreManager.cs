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
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button exitButton;
    
    // Team scores - we'll fix them to always increment by exactly 1
    private readonly NetworkVariable<int> team1Score = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> team2Score = new NetworkVariable<int>(0);
    
    // Store player information
    private readonly Dictionary<ulong, int> playerTeams = new Dictionary<ulong, int>(); // 1 = team1, 2 = team2
    
    // Team colors
    private readonly NetworkVariable<Color32> team1Color = new NetworkVariable<Color32>(new Color32(255, 0, 0, 255)); // Default: Red
    private readonly NetworkVariable<Color32> team2Color = new NetworkVariable<Color32>(new Color32(0, 0, 255, 255)); // Default: Blue
    
    // Team names (to be properly synced)
    private readonly NetworkVariable<NetworkString> team1Name = new NetworkVariable<NetworkString>(new NetworkString("Red Team"));
    private readonly NetworkVariable<NetworkString> team2Name = new NetworkVariable<NetworkString>(new NetworkString("Blue Team"));
    
    // Game state
    private readonly NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> winningTeamId = new NetworkVariable<int>(0); // 1 = team1, 2 = team2
    private readonly NetworkVariable<NetworkString> winningTeamName = new NetworkVariable<NetworkString>(new NetworkString(""));
    
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
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to score and color changes
        team1Score.OnValueChanged += (_, _) => UpdateScoreUI();
        team2Score.OnValueChanged += (_, _) => UpdateScoreUI();
        team1Color.OnValueChanged += (_, _) => UpdateScoreUI();
        team2Color.OnValueChanged += (_, _) => UpdateScoreUI();
        team1Name.OnValueChanged += (_, _) => UpdateScoreUI();
        team2Name.OnValueChanged += (_, _) => UpdateScoreUI();
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
        
        Debug.Log($"[TeamScoreManager] OnNetworkSpawn - IsServer: {IsServer}, IsOwner: {IsOwner}, IsClient: {IsClient}");
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
                    
                    // Assign teams based on server/client status
                    if (client.ClientId == NetworkManager.ServerClientId)
                    {
                        // Server/Host is team 1
                        playerTeams[client.ClientId] = 1;
                        
                        // Update color if changed
                        if (ColorIsDifferent(team1Color.Value, playerColor))
                        {
                            team1Color.Value = playerColor;
                            team1Name.Value = GetTeamNameFromColor(playerColor);
                            colorChanged = true;
                            Debug.Log($"[TeamScoreManager] Updated Team 1 color to {playerColor}, name: {team1Name.Value}");
                        }
                    }
                    else
                    {
                        // Client is team 2
                        playerTeams[client.ClientId] = 2;
                        
                        // Update color if changed
                        if (ColorIsDifferent(team2Color.Value, playerColor))
                        {
                            team2Color.Value = playerColor;
                            team2Name.Value = GetTeamNameFromColor(playerColor);
                            colorChanged = true;
                            Debug.Log($"[TeamScoreManager] Updated Team 2 color to {playerColor}, name: {team2Name.Value}");
                        }
                    }
                }
            }
        }
        
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
    
    private void UpdateScoreUI()
    {
        // Update team 1 (Host) score text
        if (team1ScoreText != null)
        {
            team1ScoreText.text = $"{team1Name.Value}: {team1Score.Value}";
            team1ScoreText.color = team1Color.Value;
            Debug.Log($"[TeamScoreManager] Updated Team 1 UI: {team1Name.Value}, Score: {team1Score.Value}, Color: {team1Color.Value}");
        }
        
        // Update team 2 (Join) score text
        if (team2ScoreText != null)
        {
            team2ScoreText.text = $"{team2Name.Value}: {team2Score.Value}";
            team2ScoreText.color = team2Color.Value;
            Debug.Log($"[TeamScoreManager] Updated Team 2 UI: {team2Name.Value}, Score: {team2Score.Value}, Color: {team2Color.Value}");
        }
        
        // Check win condition (server only)
        if (IsServer)
        {
            CheckWinCondition();
        }
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
        winningTeamId.Value = winningTeam;
        winningTeamName.Value = winningName;
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
        if (playerTeams.TryGetValue(killerClientId, out int teamId))
        {
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
        }
        else
        {
            Debug.LogWarning($"[TeamScoreManager] No team info for player ClientId: {killerClientId}");
            
            // Fallback method - determine team by client ID
            if (killerClientId == NetworkManager.ServerClientId)
            {
                // FORCE exactly 1 point increment
                int currentScore = team1Score.Value;
                team1Score.Value = currentScore + 1;
                Debug.Log($"[TeamScoreManager] (Fallback) Added 1 score to Team 1 (Host): {currentScore} -> {team1Score.Value}");
                
                // Store team for future reference
                playerTeams[killerClientId] = 1;
            }
            else
            {
                // FORCE exactly 1 point increment
                int currentScore = team2Score.Value;
                team2Score.Value = currentScore + 1;
                Debug.Log($"[TeamScoreManager] (Fallback) Added 1 score to Team 2 (Client): {currentScore} -> {team2Score.Value}");
                
                // Store team for future reference
                playerTeams[killerClientId] = 2;
            }
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
        
        Debug.Log("UI Components:");
        Debug.Log($"  team1ScoreText: {(team1ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  team2ScoreText: {(team2ScoreText != null ? "Found" : "Missing")}");
        Debug.Log($"  gameOverPanel: {(gameOverPanel != null ? "Found" : "Missing")}");
        Debug.Log($"  winnerText: {(winnerText != null ? "Found" : "Missing")}");
        Debug.Log($"  exitButton: {(exitButton != null ? "Found" : "Missing")}");
        Debug.Log("===========================================");
    }
}