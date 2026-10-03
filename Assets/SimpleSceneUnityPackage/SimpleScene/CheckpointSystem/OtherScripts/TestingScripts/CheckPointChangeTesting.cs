using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MLG.SimpleSceneTool.CheckpointSystem
{

    public class CheckPointChangeTesting : MonoBehaviour
    {
        [SerializeField]
        private SceneSet currentSceneSet;

        public void ChangeToSceneSet()
        {
            SceneSetLoader.Load(currentSceneSet);
        }
        public void ChangeToSceneSetWhenReady()
        {
            SceneSetLoader.Load(currentSceneSet, true);
        }
        public void ActivatePreloadedScenes()
        {
            SceneSetLoader.ActivatePendingPreload();

        }

    }
}