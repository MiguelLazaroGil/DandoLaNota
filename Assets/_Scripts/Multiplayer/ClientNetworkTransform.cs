using Unity.Netcode.Components;
using UnityEngine;


[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false; // El cliente local calcula su propia posición e informa al servidor
    }
}