using Unity.Netcode;
using UnityEngine;

public class NetworkButtons : MonoBehaviour
{
    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void Join()
    {
        NetworkManager.Singleton.StartClient();
    }
}