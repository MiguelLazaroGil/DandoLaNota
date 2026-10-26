using Unity.Netcode;
using UnityEngine;

public class NetworkOrchestraSync : NetworkBehaviour
{
    [Rpc(SendTo.ClientsAndHost)]
    public void StartOrchestraClientRpc(double networkStartTime)
    {
        // Iniciar la orquesta localmente cuando el Host lo ordene
        if (OrchestraAudioManager.Instance != null)
        {
            OrchestraAudioManager.Instance.StartOrchestra();
        }
    }
    public void SendStart()
    {
        StartOrchestraClientRpc(NetworkManager.Singleton.ServerTime.Time+ 0.5);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void StopOrchestraClientRpc()
    {
        if (OrchestraAudioManager.Instance != null)
        {
            OrchestraAudioManager.Instance.StopOrchestra();
        }
    }
}