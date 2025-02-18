using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private const int MAX_PLAYERS = 4;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        }
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.Count > MAX_PLAYERS)
        {
            NetworkManager.Singleton.DisconnectClient(clientId);
            Debug.Log($"Kicked client {clientId} - Max players reached");
            return;
        }

        Debug.Log($"Client {clientId} connected");
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
        }
    }
}