using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RandomHairStyle : NetworkBehaviour
{
    [SerializeField] bool isDirector = false;
    [SerializeField]
    List<GameObject> HairStyles;
    [SerializeField]
    GameObject DirectorHairStyle;
    [SerializeField]
    List<Material> HairColors;

    private void OnEnable()
    {
        // 1. MODO OFFLINE: Si no hay red o no se est� escuchando, ejecuta el c�digo original intacto
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            ExecuteOriginalHairLogic();
        }
    }

    public override void OnNetworkSpawn()
    {
        // 2. MODO MULTIJUGADOR: Se ejecuta al nacer en la red usando la semilla sincronizada
        if (IsSpawned)
        {
            Random.State previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NetworkObjectId);

            ExecuteOriginalHairLogic();

            UnityEngine.Random.state = previousState;
        }
    }

    // C�digo original aislado en una funci�n limpia
    private void ExecuteOriginalHairLogic()
    {
        DeactivateHairStyles();
        if (isDirector)
        {
            DirectorHairStyle.SetActive(true);
            int randomMatStyleIdx = UnityEngine.Random.Range(0, HairColors.Count);
            DirectorHairStyle.gameObject.GetComponent<MeshRenderer>().material = HairColors[randomMatStyleIdx];
            return;
        }
        int randomHairStyleIdx = UnityEngine.Random.Range(0, HairStyles.Count + 1);
        if (randomHairStyleIdx < HairStyles.Count)
        {
            HairStyles[randomHairStyleIdx].gameObject.SetActive(true);
            int randomMatStyleIdx = UnityEngine.Random.Range(0, HairColors.Count);
            HairStyles[randomHairStyleIdx].gameObject.GetComponent<MeshRenderer>().material = HairColors[randomMatStyleIdx];
        }
    }

    private void DeactivateHairStyles()
    {
        foreach (var hair in HairStyles)
        {
            if (hair != null)
                hair.gameObject.SetActive(false);
        }
        DirectorHairStyle.SetActive(false);
    }
}