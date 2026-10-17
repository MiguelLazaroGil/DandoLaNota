using System.Collections.Generic;
using MLG.SimpleSceneTool;
using UnityEngine;
using UnityEngine.SceneManagement;


public class SimpleSceneArrayLoader : MonoBehaviour
{
    [SerializeField]
    List<SimpleScene> simpleScenes;
    public void LoadSceneAt(int idx)
    {
        SceneManager.LoadScene(simpleScenes[idx].Index);
    }
}
