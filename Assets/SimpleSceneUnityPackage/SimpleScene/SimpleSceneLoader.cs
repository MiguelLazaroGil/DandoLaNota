using UnityEngine;
using UnityEngine.SceneManagement;
namespace MLG.SimpleSceneTool
{
    public class SimpleSceneLoader : MonoBehaviour
    {

        [SerializeField]
        SimpleScene scene;
        /// <summary>
        /// Loads the scene associated with this component.
        /// </summary>
        public void LoadScene()
        {
            SceneManager.LoadScene(scene.Index);
        }
    }
}