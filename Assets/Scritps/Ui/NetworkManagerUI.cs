using Unity.Netcode;
using UnityEngine;

public class NetworkManagerUI : MonoBehaviour
{
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Started Host");
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Started Client");
    }

    public void DisconnectGame()
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("Disconnected");
    }
}