using MLG.SimpleSceneTool;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneMultiplayer : MonoBehaviour
{
    [SerializeField] SimpleScene sceneToLoad;

    public void LoadScene()
    {
        int sceneIndex = sceneToLoad.Index;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Solo el Host o Servidor puede solicitar un cambio de escena en red.");
            return;
        }

        if (sceneIndex < 0 || sceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"El índice de escena {sceneIndex} no está incluido en Build Settings.");
            return;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError($"No se pudo obtener la ruta para la escena en el índice {sceneIndex}.");
            return;
        }

        LobbyPlayerState.Instance.ResolveRolesBeforeStart();
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);

        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

}
