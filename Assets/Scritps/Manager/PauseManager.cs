// PauseManager.cs - แบบย่อแต่คงฟังก์ชันการทำงานเดิม
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PauseManager : NetworkBehaviour
{
    [SerializeField] private GameObject pausePanel, playerListPanel;
    [SerializeField] private Button continueButton;
    
    private NetworkVariable<bool> isGamePaused = new NetworkVariable<bool>(true);
    
    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnStartGameClicked);
        
        if (pausePanel != null) pausePanel.SetActive(true);
        if (playerListPanel != null) playerListPanel.SetActive(true);
        
        Time.timeScale = 0f;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isGamePaused.OnValueChanged += (_, newValue) => UpdatePauseUI(newValue);
        if (IsServer) isGamePaused.Value = true;
        UpdatePauseUI(isGamePaused.Value);
    }
    
    private void UpdatePauseUI(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        if (playerListPanel != null) playerListPanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }
    
    public void OnStartGameClicked()
    {
        if (IsServer)
            isGamePaused.Value = false;
        else
            SetPausedStateServerRpc(false);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SetPausedStateServerRpc(bool isPaused)
    {
        isGamePaused.Value = isPaused;
    }
}