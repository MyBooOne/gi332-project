// PlayerLobbyManager.cs - แก้ไขปัญหาชื่อไม่แสดงและไม่ส่งต่อไปยัง TeamScoreManager
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class PlayerLobbyManager : NetworkBehaviour
{
    [SerializeField] private GameObject mainMenuPanel, lobbyPanel;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button hostButton, joinButton, readyButton, startGameButton;
    [SerializeField] private List<GameObject> playerPanels = new List<GameObject>();
    [SerializeField] private TMP_Text readyButtonText;
    
    // เพิ่มส่วนอ้างอิงไปยัง TeamScoreManager (ตั้งใน inspector)
    [SerializeField] private TeamScoreManager teamScoreManager;
    
    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    private Dictionary<ulong, PlayerInfo> playerInfoDict = new Dictionary<ulong, PlayerInfo>();
    private Dictionary<ulong, int> playerToPanelIndex = new Dictionary<ulong, int>();
    private int nextAvailablePanelIndex = 0;
    private bool isNameSyncing = false;
    
    private class PlayerInfo { public string Name; public bool IsReady; }
    
    private void Awake()
    {
        // ถ้าไม่ได้กำหนด TeamScoreManager ในอินสเปกเตอร์ ให้ลองหาในฉาก
        if (teamScoreManager == null)
        {
            teamScoreManager = FindObjectOfType<TeamScoreManager>();
        }
        
        hostButton.onClick.AddListener(() => {
            string name = string.IsNullOrEmpty(playerNameInput.text) ? "Host" : playerNameInput.text;
            playerNameInput.text = name;
            NetworkManager.Singleton.StartHost();
        });
        
        joinButton.onClick.AddListener(() => {
            string name = string.IsNullOrEmpty(playerNameInput.text) ? $"Player {Random.Range(100, 999)}" : playerNameInput.text;
            playerNameInput.text = name;
            NetworkManager.Singleton.StartClient();
        });
        
        readyButton.onClick.AddListener(() => {
            if (playerInfoDict.TryGetValue(NetworkManager.Singleton.LocalClientId, out PlayerInfo myInfo)) {
                bool newState = !myInfo.IsReady;
                UpdateReadyButtonText(newState);
                SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId, newState);
            }
        });
        
        startGameButton.onClick.AddListener(() => {
            if (!IsServer) return;
            bool allReady = true;
            foreach (var info in playerInfoDict.Values) 
                if (!info.IsReady) { allReady = false; break; }
            
            if (allReady) {
                RegisterAllPlayerNamesWithTeamManager();
                isGameStarted.Value = true;
                StartGameClientRpc();
            }
        });
        
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        
        // ซ่อน player panels ทั้งหมดตอนเริ่มต้น
        foreach (var panel in playerPanels) {
            panel.SetActive(false);
        }
    }
    
    // เพิ่มเมธอดใหม่เพื่อลงทะเบียนชื่อผู้เล่นทั้งหมดกับ TeamScoreManager ก่อนเริ่มเกม
    private void RegisterAllPlayerNamesWithTeamManager()
    {
        if (!IsServer) return;
        
        foreach (var playerEntry in playerInfoDict)
        {
            // ลงทะเบียนชื่อผู้เล่นกับทีม
            if (teamScoreManager != null)
            {
                teamScoreManager.RegisterPlayerName(playerEntry.Key, playerEntry.Value.Name);
                Debug.Log($"[PlayerLobbyManager] Registered player with TeamScoreManager - ClientId: {playerEntry.Key}, Name: {playerEntry.Value.Name}");
            }
        }
        
        // ส่ง RPC เพื่อซิงค์ข้อมูลทีมไปยังทุกไคลเอนต์
        SyncTeamNamesClientRpc();
        
        // เพิ่มการทำซ้ำเพื่อให้แน่ใจว่าข้อมูลถูกส่ง
        StartCoroutine(ForceTeamNameSync());
    }
    
    private IEnumerator ForceTeamNameSync()
    {
        for (int i = 0; i < 5; i++) // ทำซ้ำ 5 ครั้ง
        {
            yield return new WaitForSeconds(0.5f);
            if (teamScoreManager != null)
            {
                SyncTeamNamesClientRpc();
            }
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        
        // รีเซ็ต panel counter และซ่อน panels ทั้งหมด
        nextAvailablePanelIndex = 0;
        playerInfoDict.Clear();
        playerToPanelIndex.Clear();
        foreach (var panel in playerPanels) {
            panel.SetActive(false);
        }
        
        if (IsServer) {
            startGameButton.gameObject.SetActive(true);
            string name = string.IsNullOrEmpty(playerNameInput.text) ? "Host" : playerNameInput.text;
            AddPlayerInfo(NetworkManager.Singleton.LocalClientId, name, false);
            
            // แจ้งทุกเครื่องเกี่ยวกับโฮสต์
            SyncAllPlayersClientRpc();
        }
        
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        
        // สำหรับไคลเอนต์ (ไม่ใช่โฮสต์) ขอข้อมูลผู้เล่นทั้งหมดจากเซิร์ฟเวอร์
        if (IsClient && !IsServer) {
            RequestAllPlayersServerRpc(NetworkManager.Singleton.LocalClientId, playerNameInput.text);
        }
        
        // เริ่ม coroutine เพื่อซิงค์ชื่ออย่างต่อเนื่อง
        StartCoroutine(ContinuousNameSync());
        
        // Log เพื่อตรวจสอบ
        Debug.Log($"[PlayerLobbyManager] OnNetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
    }
    
    // เพิ่ม coroutine เพื่อซิงค์ชื่ออย่างต่อเนื่อง
    private IEnumerator ContinuousNameSync()
    {
        isNameSyncing = true;
        
        while (isNameSyncing)
        {
            yield return new WaitForSeconds(1.0f);
            
            if (IsSpawned)
            {
                // ซิงค์ชื่อผู้เล่นท้องถิ่นไปยังเซิร์ฟเวอร์
                string localName = playerNameInput != null ? playerNameInput.text : "Player";
                if (!string.IsNullOrEmpty(localName))
                {
                    if (IsServer)
                    {
                        // ถ้าเป็นเซิร์ฟเวอร์
                        AddPlayerInfo(NetworkManager.Singleton.LocalClientId, localName, 
                            playerInfoDict.TryGetValue(NetworkManager.Singleton.LocalClientId, out PlayerInfo info) && info.IsReady);
                        SyncPlayerClientRpc(NetworkManager.Singleton.LocalClientId, localName, 
                            playerInfoDict.TryGetValue(NetworkManager.Singleton.LocalClientId, out PlayerInfo i) && i.IsReady);
                    }
                    else
                    {
                        // ถ้าเป็นไคลเอนต์
                        RegisterPlayerNameServerRpc(NetworkManager.Singleton.LocalClientId, localName);
                    }
                    
                    // ตรวจสอบและแก้ไขชื่อใน UI โดยตรง
                    FixLocalDisplayNames();
                }
                
                // ร้องขอข้อมูลทีมจากเซิร์ฟเวอร์
                if (!IsServer && teamScoreManager != null)
                {
                    RequestTeamNamesFromServerServerRpc(NetworkManager.Singleton.LocalClientId);
                }
            }
        }
    }
    
    // เพิ่มเมธอดแก้ไขชื่อใน UI โดยตรง
    private void FixLocalDisplayNames()
    {
        string localName = playerNameInput.text;
        
        // แก้ไขชื่อใน UI panels
        foreach (var entry in playerToPanelIndex)
        {
            ulong clientId = entry.Key;
            int panelIndex = entry.Value;
            
            if (clientId == NetworkManager.Singleton.LocalClientId && panelIndex < playerPanels.Count)
            {
                GameObject panel = playerPanels[panelIndex];
                TMP_Text nameText = panel.transform.Find("NameText")?.GetComponent<TMP_Text>();
                
                if (nameText != null && (nameText.text == "New Text" || string.IsNullOrEmpty(nameText.text) || nameText.text != localName))
                {
                    nameText.text = localName;
                    Debug.Log($"Fixed local UI name to: {localName}");
                }
            }
        }
        
        // แก้ไขชื่อทีมโดยตรง
        if (teamScoreManager != null)
        {
            teamScoreManager.ForceUpdateName(NetworkManager.Singleton.LocalClientId, localName);
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null) {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        isNameSyncing = false;
        base.OnNetworkDespawn();
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[PlayerLobbyManager] Client connected: {clientId}");
        
        if (IsServer) {
            // ถ้าเป็นเซิร์ฟเวอร์ ขอชื่อจากไคลเอนต์
            RequestPlayerNameClientRpc(clientId);
            
            // ส่งข้อมูลผู้เล่นที่มีอยู่แล้วไปให้ไคลเอนต์ใหม่
            foreach (var playerEntry in playerInfoDict) {
                if (playerEntry.Key != clientId) { // ไม่ต้องส่งข้อมูลตัวเองกลับไป
                    SendPlayerInfoToClientRpc(clientId, playerEntry.Key, playerEntry.Value.Name, playerEntry.Value.IsReady);
                }
            }
        }
        else if (IsClient && clientId == NetworkManager.Singleton.LocalClientId) {
            // ถ้าเป็นไคลเอนต์ที่เพิ่งเชื่อมต่อ ส่งชื่อไปให้เซิร์ฟเวอร์
            RegisterPlayerNameServerRpc(clientId, playerNameInput.text);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[PlayerLobbyManager] Client disconnected: {clientId}");
        RemovePlayerInfo(clientId);
        if (IsServer) RemovePlayerClientRpc(clientId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestAllPlayersServerRpc(ulong clientId, string playerName)
    {
        // ลงทะเบียนผู้เล่นใหม่ก่อน
        if (string.IsNullOrEmpty(playerName)) playerName = "Player " + clientId;
        AddPlayerInfo(clientId, playerName, false);
        
        // จากนั้นส่งข้อมูลผู้เล่นทั้งหมดไปให้ทุกไคลเอนต์
        SyncAllPlayersClientRpc();
    }
    
    [ClientRpc]
    private void SyncAllPlayersClientRpc()
    {
        if (IsServer) return; // เซิร์ฟเวอร์ไม่ต้องทำอะไร เพราะมีข้อมูลครบแล้ว
        
        Debug.Log("[PlayerLobbyManager] Received SyncAllPlayersClientRpc");
        
        // โฮสต์จะส่งข้อมูลผู้เล่นแต่ละคนแยกกัน
        if (!IsServer) {
            // ไคลเอนต์ส่งข้อมูลตัวเองไปให้เซิร์ฟเวอร์อีกครั้ง
            RegisterPlayerNameServerRpc(NetworkManager.Singleton.LocalClientId, playerNameInput.text);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerNameServerRpc(ulong clientId, string playerName)
    {
        Debug.Log($"[PlayerLobbyManager] RegisterPlayerNameServerRpc - ClientId: {clientId}, Name: {playerName}");
        
        if (string.IsNullOrEmpty(playerName)) playerName = "Player " + clientId;
        AddPlayerInfo(clientId, playerName, false);
        
        // แจ้งทุกไคลเอนต์ให้อัปเดตข้อมูลผู้เล่นนี้
        SyncPlayerClientRpc(clientId, playerName, false);
        
        // ลงทะเบียนกับ TeamScoreManager ทันที
        if (teamScoreManager != null)
        {
            teamScoreManager.RegisterPlayerName(clientId, playerName);
        }
    }
    
    [ClientRpc] 
    private void RequestPlayerNameClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId) {
            Debug.Log($"[PlayerLobbyManager] RequestPlayerNameClientRpc - Sending name '{playerNameInput.text}' to server");
            RegisterPlayerNameServerRpc(targetClientId, playerNameInput.text);
        }
    }
    
    [ClientRpc] 
    private void SyncPlayerClientRpc(ulong clientId, string playerName, bool isReady)
    {
        Debug.Log($"[PlayerLobbyManager] SyncPlayerClientRpc - ClientId: {clientId}, Name: {playerName}, Ready: {isReady}");
        AddPlayerInfo(clientId, playerName, isReady);
        
        // ถ้าเป็นผู้เล่นท้องถิ่น ตรวจสอบและเพิ่ม force update สำหรับชื่อทีม
        if (clientId == NetworkManager.Singleton.LocalClientId && teamScoreManager != null)
        {
            teamScoreManager.ForceUpdateName(clientId, playerName);
        }
    }
    
    [ClientRpc]
    private void SendPlayerInfoToClientRpc(ulong targetClientId, ulong playerId, string playerName, bool isReady)
    {
        // ส่งเฉพาะไปยังไคลเอนต์เป้าหมาย
        if (NetworkManager.Singleton.LocalClientId == targetClientId) {
            Debug.Log($"[PlayerLobbyManager] SendPlayerInfoToClientRpc - Received player info: {playerId}, {playerName}, {isReady}");
            AddPlayerInfo(playerId, playerName, isReady);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ulong clientId, bool isReady)
    {
        if (playerInfoDict.TryGetValue(clientId, out PlayerInfo info)) {
            info.IsReady = isReady;
            UpdatePlayerReadyClientRpc(clientId, isReady);
        }
    }
    
    [ClientRpc] 
    private void UpdatePlayerReadyClientRpc(ulong clientId, bool isReady)
    {
        Debug.Log($"[PlayerLobbyManager] UpdatePlayerReadyClientRpc - ClientId: {clientId}, Ready: {isReady}");
        
        if (playerInfoDict.TryGetValue(clientId, out PlayerInfo info)) {
            info.IsReady = isReady;
            
            // อัปเดต UI
            if (playerToPanelIndex.TryGetValue(clientId, out int panelIndex)) {
                UpdatePlayerPanel(panelIndex, info.Name, isReady);
            }
            
            if (clientId == NetworkManager.Singleton.LocalClientId)
                UpdateReadyButtonText(isReady);
        }
    }
    
    [ClientRpc] 
    private void RemovePlayerClientRpc(ulong clientId)
    {
        Debug.Log($"[PlayerLobbyManager] RemovePlayerClientRpc - ClientId: {clientId}");
        RemovePlayerInfo(clientId);
    }
    
    [ClientRpc]
    private void SyncTeamNamesClientRpc()
    {
        Debug.Log("[PlayerLobbyManager] SyncTeamNamesClientRpc - Syncing team names with all clients");
        
        if (teamScoreManager != null && !IsServer)
        {
            // ถ้าเป็นไคลเอนต์ ให้ขอข้อมูลทีมจากเซิร์ฟเวอร์
            RequestTeamNamesFromServerServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestTeamNamesFromServerServerRpc(ulong clientId)
    {
        if (teamScoreManager != null)
        {
            // ส่งข้อมูลทีมไปให้ไคลเอนต์ที่ขอ (แก้ไขให้ส่งชื่อทั้ง 4 ทีม)
            SendTeamNamesToClientClientRpc(
                clientId, 
                teamScoreManager.GetTeam1Name(), 
                teamScoreManager.GetTeam2Name(),
                teamScoreManager.GetTeam3Name(),
                teamScoreManager.GetTeam4Name()
            );
        }
    }
    
    [ClientRpc]
    private void SendTeamNamesToClientClientRpc(ulong targetClientId, string team1Name, string team2Name, string team3Name, string team4Name)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId && teamScoreManager != null)
        {
            // อัปเดตชื่อทีมในไคลเอนต์ (แก้ไขให้ส่งชื่อทั้ง 4 ทีม)
            teamScoreManager.SetTeamNames(team1Name, team2Name, team3Name, team4Name);
            Debug.Log($"[PlayerLobbyManager] Updated team names - Team1: {team1Name}, Team2: {team2Name}, Team3: {team3Name}, Team4: {team4Name}");
        }
    }
    
    [ClientRpc]
    private void StartGameClientRpc()
    {
        // ซ่อน lobby UI
        lobbyPanel.SetActive(false);
    
        // ส่งชื่อทั้งหมดไปยัง TeamScoreManager อีกครั้ง
        if (teamScoreManager != null && playerInfoDict.TryGetValue(NetworkManager.Singleton.LocalClientId, out PlayerInfo localInfo))
        {
            teamScoreManager.ForceUpdateName(NetworkManager.Singleton.LocalClientId, localInfo.Name);
        }
    
        // เรียกใช้ PauseManager เพื่อเริ่มเกม
        PauseManager pauseManager = FindObjectOfType<PauseManager>();
        if (pauseManager != null) {
            pauseManager.OnStartGameClicked();
        }
    
        // รีเซ็ตคะแนนและสถานะเกม
        if (teamScoreManager != null) {
            teamScoreManager.ResetGameState();
        }
    
        Debug.Log("เกมเริ่มแล้ว!");
    }
    
    private void AddPlayerInfo(ulong clientId, string playerName, bool isReady)
    {
        Debug.Log($"[PlayerLobbyManager] AddPlayerInfo - ClientId: {clientId}, Name: {playerName}, Ready: {isReady}");
        
        if (playerInfoDict.TryGetValue(clientId, out PlayerInfo info)) {
            // อัปเดตข้อมูลเดิม
            info.Name = playerName;
            info.IsReady = isReady;
            
            // อัปเดต UI
            if (playerToPanelIndex.TryGetValue(clientId, out int panelIndex)) {
                UpdatePlayerPanel(panelIndex, playerName, isReady);
            }
        } else {
            // ตรวจสอบว่ามี panel ว่างหรือไม่
            if (nextAvailablePanelIndex < playerPanels.Count) {
                // เพิ่มข้อมูลใหม่
                playerInfoDict[clientId] = new PlayerInfo {
                    Name = playerName,
                    IsReady = isReady
                };
                
                // จับคู่กับ panel
                playerToPanelIndex[clientId] = nextAvailablePanelIndex;
                
                // อัปเดต UI
                UpdatePlayerPanel(nextAvailablePanelIndex, playerName, isReady);
                
                // เพิ่ม counter
                nextAvailablePanelIndex++;
            } else {
                Debug.LogError($"[PlayerLobbyManager] ไม่มี panel เพียงพอสำหรับผู้เล่น! (ต้องการ panel ที่ {nextAvailablePanelIndex})");
            }
        }
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
            UpdateReadyButtonText(isReady);
    }
    
    private void RemovePlayerInfo(ulong clientId)
    {
        if (playerToPanelIndex.TryGetValue(clientId, out int panelIndex)) {
            // ซ่อน panel
            if (panelIndex >= 0 && panelIndex < playerPanels.Count) {
                playerPanels[panelIndex].SetActive(false);
            }
            
            // ลบข้อมูล
            playerInfoDict.Remove(clientId);
            playerToPanelIndex.Remove(clientId);
            
            // ถ้าเป็น panel สุดท้าย ปรับ counter
            if (panelIndex == nextAvailablePanelIndex - 1) {
                nextAvailablePanelIndex--;
            }
            
            Debug.Log($"[PlayerLobbyManager] RemovePlayerInfo - ClientId: {clientId}, PanelIndex: {panelIndex}");
        }
    }
    
    private void UpdatePlayerPanel(int panelIndex, string playerName, bool isReady)
    {
        if (panelIndex >= 0 && panelIndex < playerPanels.Count) {
            GameObject panel = playerPanels[panelIndex];
            panel.SetActive(true);
            
            // ใช้ GetComponentInChildren แทน Find ที่อาจล้มเหลว
            TMP_Text[] allTexts = panel.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text nameText = null;
            TMP_Text statusText = null;
            
            if (allTexts.Length > 0) {
                // กำหนดให้ Text แรกเป็นชื่อ
                nameText = allTexts[0];
                
                if (allTexts.Length > 1) {
                    // กำหนดให้ Text ที่สองเป็นสถานะ
                    statusText = allTexts[1];
                }
            }
            
            // หาก Find ล้มเหลว ลองหาแบบประเพณี
            if (nameText == null) {
                nameText = panel.transform.Find("NameText")?.GetComponent<TMP_Text>();
            }
            
            if (statusText == null) {
                statusText = panel.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            }
            
            if (nameText != null) {
                // ตรวจสอบว่ามีข้อความอยู่แล้วหรือไม่
                if (nameText.text == "New Text" || string.IsNullOrEmpty(nameText.text)) {
                    nameText.text = playerName;
                } else if (nameText.text != playerName) {
                    // อัปเดตเฉพาะเมื่อชื่อไม่ตรงกัน
                    nameText.text = playerName;
                }
                
                Debug.Log($"Updated name text to: {playerName} (was: {nameText.text})");
            } else {
                Debug.LogError($"Name text component not found in player panel {panelIndex}");
            }
            
            if (statusText != null) {
                statusText.text = isReady ? "Ready" : "Not Ready";
                statusText.color = isReady ? Color.green : Color.red;
            } else {
                Debug.LogError($"Status text component not found in player panel {panelIndex}");
            }
            
            Debug.Log($"[PlayerLobbyManager] UpdatePlayerPanel - Panel: {panelIndex}, Name: {playerName}, Ready: {isReady}");
        }
    }
    
    private void UpdateReadyButtonText(bool isReady)
    {
        if (readyButtonText != null)
            readyButtonText.text = isReady ? "Unready" : "Ready";
    }
    
    // เพิ่มเมธอด Update เพื่อตรวจสอบและแก้ไขชื่อต่อเนื่อง
    private void Update()
    {
        // ตรวจสอบและแก้ไขชื่อทุกๆ เฟรม
        if (IsSpawned && playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
        {
            string localName = playerNameInput.text;
            
            // แก้ไขชื่อใน Panels
            foreach (var entry in playerToPanelIndex)
            {
                if (entry.Key == NetworkManager.Singleton.LocalClientId)
                {
                    int panelIndex = entry.Value;
                    if (panelIndex >= 0 && panelIndex < playerPanels.Count)
                    {
                        GameObject panel = playerPanels[panelIndex];
                        TMP_Text nameText = panel.transform.Find("NameText")?.GetComponent<TMP_Text>();
                        
                        if (nameText != null && (nameText.text == "New Text" || nameText.text != localName))
                        {
                            nameText.text = localName;
                        }
                    }
                }
            }
        }
    }
}