using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class TankSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Server spawns a tank for each connected player
        SpawnTankServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnTankServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Get random spawn point
        int spawnIndex = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[spawnIndex];

        // Spawn tank and set ownership
        GameObject tank = Instantiate(tankPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject networkObject = tank.GetComponent<NetworkObject>();
        networkObject.Spawn();

        // Set ownership to the client who requested the spawn
        networkObject.ChangeOwnership(serverRpcParams.Receive.SenderClientId);
    }
}
