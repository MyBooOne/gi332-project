using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    
    // เพิ่ม UI สำหรับแสดงเลือด
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    
    // เพิ่ม VFX สำหรับเมื่อโดนโจมตี
    [SerializeField] private GameObject hitEffect;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
        
        // ตั้งค่าเริ่มต้นสำหรับ UI
        currentHealth.OnValueChanged += UpdateHealthUI;
        UpdateHealthUI(0, currentHealth.Value);
    }
    
    private void UpdateHealthUI(int oldValue, int newValue)
    {
        if (healthSlider != null)
        {
            healthSlider.value = (float)newValue / maxHealth;
        }
        
        if (healthText != null)
        {
            healthText.text = newValue.ToString() + " / " + maxHealth.ToString();
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        // แปลง float เป็น int
        int damageInt = Mathf.RoundToInt(damage);
        
        currentHealth.Value -= damageInt;
        
        // แสดงเอฟเฟคเมื่อโดนโจมตี
        if (hitEffect != null)
        {
            ShowHitEffectClientRpc();
        }
        
        if (currentHealth.Value <= 0)
        {
            // แสดงเอฟเฟคการระเบิด/ตาย ก่อนลบออก
            DestroyTankClientRpc();
            
            // รอสักครู่ก่อนลบออก
            Invoke(nameof(DespawnTank), 1.5f);
        }
    }
    
    private void DespawnTank()
    {
        NetworkObject.Despawn();
    }
    
    [ClientRpc]
    private void ShowHitEffectClientRpc()
    {
        if (hitEffect != null)
        {
            hitEffect.SetActive(true);
            Invoke(nameof(HideHitEffect), 0.5f);
        }
    }
    
    private void HideHitEffect()
    {
        if (hitEffect != null)
        {
            hitEffect.SetActive(false);
        }
    }
    
    [ClientRpc]
    private void DestroyTankClientRpc()
    {
        // เล่นแอนิเมชั่นหรือ VFX การระเบิด
        // ตัวอย่าง: Instantiate(explosionPrefab, transform.position, Quaternion.identity);
    }
}