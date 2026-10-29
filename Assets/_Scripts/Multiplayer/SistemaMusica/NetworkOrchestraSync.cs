using Unity.Netcode;
using UnityEngine;

public class NetworkOrchestraSync : NetworkBehaviour
{
    public float initialDelay=5f;
    [Rpc(SendTo.ClientsAndHost)]
    public void StartOrchestraClientRpc(double networkStartTime)
    {
        // Iniciar la orquesta localmente cuando el Host lo ordene
        if (OrchestraAudioManager.Instance != null)
        {
            OrchestraAudioManager.Instance.StartOrchestra();
        }
    }
    void Start(){
    
    	if(IsServer || IsHost){
    		SendStart();
    
    	}
    }
    public void SendStart()
    {
        StartOrchestraClientRpc(NetworkManager.Singleton.ServerTime.Time+ initialDelay);
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
