using Unity.Netcode;
using UnityEngine;

public class TankColor : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] tankMeshes;

    // เปลี่ยนเป็นใช้ NetworkVariable แบบแยกค่าสี RGBA
    private NetworkVariable<float> redValue;
    private NetworkVariable<float> greenValue;
    private NetworkVariable<float> blueValue;
    private NetworkVariable<float> alphaValue;

    private void Awake()
    {
        // สร้าง NetworkVariable สำหรับแต่ละค่าสี
        redValue = new NetworkVariable<float>(0f);
        greenValue = new NetworkVariable<float>(0f);
        blueValue = new NetworkVariable<float>(0f);
        alphaValue = new NetworkVariable<float>(1f);

        // ค้นหา MeshRenderers ถ้ายังไม่ได้กำหนด
        if (tankMeshes == null || tankMeshes.Length == 0)
        {
            tankMeshes = GetComponentsInChildren<MeshRenderer>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            Color selectedColor = ColorSelector.GetSelectedColor();
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] Initializing with color: {selectedColor}");
            SetColorServerRpc(selectedColor.r, selectedColor.g, selectedColor.b, selectedColor.a);
        }

        // รับฟังการเปลี่ยนแปลงของแต่ละค่าสี
        redValue.OnValueChanged += (_, _) => UpdateMeshColors();
        greenValue.OnValueChanged += (_, _) => UpdateMeshColors();
        blueValue.OnValueChanged += (_, _) => UpdateMeshColors();
        alphaValue.OnValueChanged += (_, _) => UpdateMeshColors();

        // อัพเดทสีเริ่มต้น
        UpdateMeshColors();
    }

    private void UpdateMeshColors()
    {
        Color newColor = new Color(redValue.Value, greenValue.Value, blueValue.Value, alphaValue.Value);
        Debug.Log($"Updating mesh colors to: {newColor}");

        foreach (var mesh in tankMeshes)
        {
            if (mesh != null && mesh.material != null)
            {
                mesh.material.color = newColor;
                Debug.Log($"Applied color to mesh: {mesh.name}");
            }
        }
    }

    [ServerRpc]
    private void SetColorServerRpc(float r, float g, float b, float a)
    {
        Debug.Log($"Server received color update: R:{r} G:{g} B:{b} A:{a}");
        redValue.Value = r;
        greenValue.Value = g;
        blueValue.Value = b;
        alphaValue.Value = a;
    }
    
    // เพิ่มใน TankColor.cs
    public void ApplyColor(Color color)
    {
        if (IsOwner) {
            Debug.Log($"Applying color: {color}");
            SetColorServerRpc(color.r, color.g, color.b, color.a);
        }
    }

    public override void OnNetworkDespawn()
    {
        // ไม่จำเป็นต้อง unsubscribe เพราะ NetworkVariable จัดการให้อัตโนมัติ
        base.OnNetworkDespawn();
    }
}