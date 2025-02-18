using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public class PlayerNetwork : NetworkBehaviour
{
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    [SerializeField] private float moveSpeed = 5f;
    
    private void Start()
    {
        if (IsOwner)
        {
            SetPlayerNameServerRpc($"Player {NetworkManager.Singleton.LocalClientId}");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector3 moveDir = new Vector3(0, 0, 0);

        if (Input.GetKey(KeyCode.W)) moveDir.z = 1f;
        if (Input.GetKey(KeyCode.S)) moveDir.z = -1f;
        if (Input.GetKey(KeyCode.A)) moveDir.x = -1f;
        if (Input.GetKey(KeyCode.D)) moveDir.x = 1f;

        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string name)
    {
        playerName.Value = new FixedString32Bytes(name);
    }

    private void OnGUI()
    {
        Vector3 worldPosition = transform.position + Vector3.up * 2f;
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        if (screenPosition.z > 0)
        {
            GUI.Label(
                new Rect(screenPosition.x - 50f, Screen.height - screenPosition.y, 100f, 30f),
                playerName.Value.ToString(),
                new GUIStyle() { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState() { textColor = Color.white } }
            );
        }
    }
}