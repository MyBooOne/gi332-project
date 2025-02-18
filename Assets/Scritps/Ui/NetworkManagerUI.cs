using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;

    private void Awake()
    {
        hostButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
        });

        joinButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
        });
    }
}