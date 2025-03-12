using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float damage = 20f;
    [SerializeField] private float bulletLifetime = 3f;
    [SerializeField] private GameObject hitEffectPrefab;
    
    // Store shooter's client ID
    private ulong shooterClientId;
    
    public void SetShooterInfo(ulong clientId)
    {
        shooterClientId = clientId;
        Debug.Log($"[Bullet] Set shooter ClientId: {clientId}");
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Destroy bullet after set time (server only)
        if (IsServer)
        {
            Destroy(gameObject, bulletLifetime);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Server-only logic
        if (!IsServer) return;
        
        // Show hit effect
        if (hitEffectPrefab != null && collision.contacts.Length > 0)
        {
            GameObject hitEffect = Instantiate(
                hitEffectPrefab, 
                collision.contacts[0].point, 
                Quaternion.LookRotation(collision.contacts[0].normal)
            );
            
            // Destroy effect after 2 seconds
            Destroy(hitEffect, 2f);
        }
        
        // Check if we hit a tank
        Complete.TankHealth tankHealth = collision.gameObject.GetComponent<Complete.TankHealth>();
        if (tankHealth != null)
        {
            NetworkObject targetNetObj = collision.gameObject.GetComponent<NetworkObject>();
            
            // Make sure we're not hitting our own tank
            if (targetNetObj != null && targetNetObj.OwnerClientId != shooterClientId)
            {
                Debug.Log($"[Bullet] Hit tank - Shooter: {shooterClientId}, Target: {targetNetObj.OwnerClientId}");
                
                // Deal damage
                tankHealth.TakeDamage(damage, shooterClientId);
                
                // Check if tank died
                if (tankHealth.m_CurrentHealth.Value <= 0 && !tankHealth.m_Dead.Value)
                {
                    // Add score to shooter
                    if (TeamScoreManager.Instance != null)
                    {
                        Debug.Log($"[Bullet] Adding score to shooter ClientId: {shooterClientId}");
                        TeamScoreManager.Instance.AddScore(shooterClientId);
                    }
                    else
                    {
                        Debug.LogError("[Bullet] TeamScoreManager.Instance not found");
                    }
                }
            }
            else
            {
                Debug.Log("[Bullet] Hit own tank or NetworkObject not found - no damage applied");
            }
        }
        
        // Destroy bullet
        NetworkObject netObj = gameObject.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        Destroy(gameObject);
    }
}