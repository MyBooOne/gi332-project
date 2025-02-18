using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    private NetworkVariable<int> nextSpawnPointIndex = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayerForClient;
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        Vector3 spawnPosition = GetNextSpawnPoint();
        
        GameObject playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab.gameObject;
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId);
    }

    private Vector3 GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return Vector3.zero;
        }

        Vector3 position = spawnPoints[nextSpawnPointIndex.Value].position;
        nextSpawnPointIndex.Value = (nextSpawnPointIndex.Value + 1) % spawnPoints.Length;
        return position;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayerForClient;
        }
    }
}