#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MLG.SimpleSceneTool.GraphWindow
{
    /// <summary>
    /// Resolves a stored (gameObjectPath, componentType) route - the same identity scheme
    /// SimpleSceneGraphCollector already records - back into a live Component instance, but only
    /// against scenes that are already loaded. Never loads, unloads, or otherwise mutates
    /// anything; opening a scene on demand is a separate, later step built on top of this.
    /// </summary>
    internal static class SimpleSceneGraphSceneObjectResolver
{
    public enum ResolveStatus
    {
        Resolved,
        SceneNotLoaded,
        RouteNotFound,
        ComponentNotFound
    }

    public readonly struct ResolveResult
    {
        public readonly ResolveStatus Status;
        public readonly Component Component;

        private ResolveResult(ResolveStatus status, Component component)
        {
            Status = status;
            Component = component;
        }

        public static ResolveResult Success(Component component) => new ResolveResult(ResolveStatus.Resolved, component);
        public static ResolveResult Failure(ResolveStatus status) => new ResolveResult(status, null);
    }

    /// <summary>
    /// Attempts to resolve gameObjectPath/componentType against whichever loaded scene matches
    /// sceneAsset. Returns SceneNotLoaded without touching anything if that scene isn't open.
    /// </summary>
    public static ResolveResult Resolve(SceneAsset sceneAsset, string gameObjectPath, string componentType)
    {
        if (!TryGetLoadedScene(sceneAsset, out Scene scene))
        {
            return ResolveResult.Failure(ResolveStatus.SceneNotLoaded);
        }

        return ResolveInLoadedScene(scene, gameObjectPath, componentType);
    }

    private static bool TryGetLoadedScene(SceneAsset sceneAsset, out Scene loadedScene)
    {
        loadedScene = default;
        if (sceneAsset == null)
        {
            return false;
        }

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        if (string.IsNullOrEmpty(scenePath))
        {
            return false;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (candidate.IsValid() && candidate.isLoaded && candidate.path == scenePath)
            {
                loadedScene = candidate;
                return true;
            }
        }
        return false;
    }

    private static ResolveResult ResolveInLoadedScene(Scene scene, string gameObjectPath, string componentType)
    {
        if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(componentType))
        {
            return ResolveResult.Failure(ResolveStatus.RouteNotFound);
        }

        string[] segments = gameObjectPath.Split('/');
        GameObject current = null;

        for (int i = 0; i < segments.Length; i++)
        {
            if (!TryParseSegment(segments[i], out string expectedName, out int siblingIndex))
            {
                return ResolveResult.Failure(ResolveStatus.RouteNotFound);
            }

            GameObject candidate = FindSiblingAt(scene, current, siblingIndex);
            if (candidate == null || candidate.name != expectedName)
            {
                // The hierarchy has likely changed since the graph was last refreshed - stop
                // rather than risk following an index to an unrelated GameObject.
                return ResolveResult.Failure(ResolveStatus.RouteNotFound);
            }

            current = candidate;
        }

        if (current == null)
        {
            return ResolveResult.Failure(ResolveStatus.RouteNotFound);
        }

        foreach (Component component in current.GetComponents<Component>())
        {
            if (component != null && component.GetType().FullName == componentType)
            {
                return ResolveResult.Success(component);
            }
        }

        return ResolveResult.Failure(ResolveStatus.ComponentNotFound);
    }

    private static bool TryParseSegment(string segment, out string name, out int siblingIndex)
    {
        name = null;
        siblingIndex = -1;

        int openBracket = segment.LastIndexOf('[');
        int closeBracket = segment.LastIndexOf(']');
        if (openBracket < 0 || closeBracket <= openBracket)
        {
            return false;
        }

        name = segment.Substring(0, openBracket);
        string indexText = segment.Substring(openBracket + 1, closeBracket - openBracket - 1);
        return int.TryParse(indexText, out siblingIndex);
    }

    private static GameObject FindSiblingAt(Scene scene, GameObject parent, int siblingIndex)
    {
        if (parent == null)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            return siblingIndex >= 0 && siblingIndex < roots.Length ? roots[siblingIndex] : null;
        }

        Transform parentTransform = parent.transform;
        return siblingIndex >= 0 && siblingIndex < parentTransform.childCount
            ? parentTransform.GetChild(siblingIndex).gameObject
            : null;
    }
}
#endif
}
