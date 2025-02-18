using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHealth.Value -= damage;
        if (currentHealth.Value <= 0)
        {
            // Handle tank destruction
            NetworkObject.Despawn();
        }
    }
}
