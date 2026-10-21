using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static void LoadGameScene(string sceneName)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // El servidor/host inicia la carga. NGO sincroniza automáticamente a todos los clientes.
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}