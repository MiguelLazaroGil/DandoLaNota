using Unity.Netcode;
using UnityEngine;

public class NetworkMusicianSync : NetworkBehaviour
{
    public static NetworkMusicianSync Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Solicitud de un Fantasma para sabotear un músico
    [Rpc(SendTo.Server)]
    public void RequestSabotageRpc(int musicianIndex, string actionId, float penalty)
    {
        ApplySabotageClientRpc(musicianIndex, actionId, penalty);
    }

    // Solicitud para reparar/resolver un sabotaje
    [Rpc(SendTo.Server)]
    public void RequestResolveSabotageRpc(int musicianIndex, string actionId)
    {
        ResolveSabotageClientRpc(musicianIndex, actionId);
    }
    //Esto mejor con una lista arrastrarla y ya
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplySabotageClientRpc(int musicianIndex, string actionId, float penalty)
    {
        var musicians = FindObjectsOfType<MusicianQuality>();
        if (musicianIndex >= 0 && musicianIndex < musicians.Length)
        {
            musicians[musicianIndex].StartNegativeAction(actionId, penalty);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ResolveSabotageClientRpc(int musicianIndex, string actionId)
    {
        var musicians = FindObjectsOfType<MusicianQuality>();
        if (musicianIndex >= 0 && musicianIndex < musicians.Length)
        {
            musicians[musicianIndex].ResolveNegativeAction(actionId);
        }
    }
}