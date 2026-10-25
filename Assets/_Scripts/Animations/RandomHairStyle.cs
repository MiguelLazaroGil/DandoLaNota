using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RandomHairStyle : NetworkBehaviour
{
    [SerializeField]
    List<GameObject> HairStyles;
    [SerializeField]
    List<Material> HairColors;

    public override void OnNetworkSpawn()
    {
        // En multijugador se aplica cuando el objeto nace en la red
        ApplyRandomHair();
    }

    private void Start()
    {
        // En offline (o si no hay red activa) se aplica normalmente en Start
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            ApplyRandomHair();
        }
    }

    private void ApplyRandomHair()
    {
        DeactivateHairStyles();

        // Guardamos el estado actual del Random para no alterar otros scripts del juego
        Random.State previousState = UnityEngine.Random.state;

        // Si el objeto está en red, usamos su NetworkObjectId (idéntica en todos los clientes) como semilla
        if (IsSpawned)
        {
            UnityEngine.Random.InitState((int)NetworkObjectId);
        }

        // --- CÓDIGO ORIGINAL SIN CAMBIOS ---
        int randomHairStyleIdx = UnityEngine.Random.Range(0, HairStyles.Count + 1);
        if (randomHairStyleIdx < HairStyles.Count)
        {
            HairStyles[randomHairStyleIdx].gameObject.SetActive(true);
            int randomMatStyleIdx = UnityEngine.Random.Range(0, HairColors.Count);
            HairStyles[randomHairStyleIdx].gameObject.GetComponent<MeshRenderer>().material = HairColors[randomMatStyleIdx];
        }

        // Restauramos el estado del Random
        UnityEngine.Random.state = previousState;
    }

    private void DeactivateHairStyles()
    {
        foreach (var hair in HairStyles)
            hair.gameObject.SetActive(false);
    }
}