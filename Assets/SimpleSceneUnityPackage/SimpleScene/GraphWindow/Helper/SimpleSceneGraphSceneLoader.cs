#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MLG.SimpleSceneTool.GraphWindow
{
    /// <summary>
    /// The one place in this tool that opens a scene. Kept separate from
    /// SimpleSceneGraphSceneObjectResolver, which stays a pure, side-effect-free reader - this
    /// class is what the "Load Scene &amp; Select" button calls.
    /// </summary>
    internal static class SimpleSceneGraphSceneLoader
    {
        /// <summary>
        /// Resolves gameObjectPath/componentType against sceneAsset, opening it additively first
        /// if (and only if) it isn't already loaded. Never uses Single mode, so nothing the user
        /// currently has open is touched or replaced.
        /// </summary>
        public static SimpleSceneGraphSceneObjectResolver.ResolveResult LoadAndResolve(
            SceneAsset sceneAsset, string gameObjectPath, string componentType)
        {
            SimpleSceneGraphSceneObjectResolver.ResolveResult initial =
                SimpleSceneGraphSceneObjectResolver.Resolve(sceneAsset, gameObjectPath, componentType);

            if (initial.Status != SimpleSceneGraphSceneObjectResolver.ResolveStatus.SceneNotLoaded)
            {
                return initial; // Already loaded (resolved, or failed for a different reason).
            }

            string scenePath = sceneAsset != null ? AssetDatabase.GetAssetPath(sceneAsset) : null;
            if (string.IsNullOrEmpty(scenePath))
            {
                return initial;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            return SimpleSceneGraphSceneObjectResolver.Resolve(sceneAsset, gameObjectPath, componentType);
        }
    }
#endif
}