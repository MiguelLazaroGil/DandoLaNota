#if UNITY_EDITOR
using MLG.SimpleSceneTool.CheckpointSystem;
using MLG.SimpleSceneTool;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens a Scene Set's scenes directly in the editor (not via the runtime SceneSetLoader -
/// this is for edit-time work, jumping between Scene Sets while building levels, not gameplay
/// scene transitions) and keeps a persisted "recent" list. Shared by SceneSetLoaderWindow's own
/// Recent section and the Tools/SimpleScene/Recent Scene Sets quick-access menu, so opening a
/// Scene Set from either one keeps both in sync.
/// </summary>
internal static class SceneSetEditorLoader
{
    private const int MaxRecentCount = 8;

    private static string RecentEditorPrefsKey => "SimpleScene.SceneSetEditorLoader.Recent." + Application.dataPath.GetHashCode();
    /// <summary>Fires whenever the recent list changes (an entry added or cleared) - lets editor tooling like the native Recent Scene Sets menu stay in sync without this class needing to know that tooling exists.</summary>
    public static event Action OnRecentChanged;

    /// <summary>
    /// Opens sceneSet's scenes in the editor (first Single, the rest Additive) and records it
    /// as the most recent entry. Prompts to save modified scenes first, same as File > Open
    /// Scene would - returns false without opening anything if that's declined.
    /// </summary>
    public static bool Open(SceneSet sceneSet)
    {
        if (sceneSet == null || sceneSet.Scenes == null || sceneSet.Scenes.Count == 0)
        {
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        Scene firstLoadedScene = default;
        int openedCount = 0;
        foreach (SimpleScene simpleScene in sceneSet.Scenes)
        {
            if (simpleScene == null || simpleScene.sceneAsset == null)
            {
                continue;
            }
            string path = AssetDatabase.GetAssetPath(simpleScene.sceneAsset);
            Scene loadedScene = EditorSceneManager.OpenScene(
                path,
                openedCount == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            if (openedCount == 0)
            {
                firstLoadedScene = loadedScene;
            }
            openedCount++;
        }
        if (firstLoadedScene.IsValid())
        {
            SceneManager.SetActiveScene(firstLoadedScene);
        }

        AddRecent(sceneSet);
        return true;
    }

    /// <summary>Recently-opened Scene Sets, newest first. Deleted/moved ones are silently pruned (and the prune persisted).</summary>
    public static List<SceneSet> GetRecent()
    {
        List<string> guids = LoadGuids();
        List<SceneSet> resolved = new List<SceneSet>();
        bool prunedAny = false;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SceneSet sceneSet = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneSet>(path);
            if (sceneSet == null)
            {
                prunedAny = true;
                continue;
            }
            resolved.Add(sceneSet);
        }

        if (prunedAny)
        {
            SaveGuids(resolved.ConvertAll(s => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s))));
        }

        return resolved;
    }

    public static void ClearRecent()
    {
        EditorPrefs.DeleteKey(RecentEditorPrefsKey);
        OnRecentChanged?.Invoke();
    }

    private static void AddRecent(SceneSet sceneSet)
    {
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneSet));
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        List<string> guids = LoadGuids();
        guids.Remove(guid);
        guids.Insert(0, guid);
        if (guids.Count > MaxRecentCount)
        {
            guids.RemoveRange(MaxRecentCount, guids.Count - MaxRecentCount);
        }
        SaveGuids(guids);
        OnRecentChanged?.Invoke();
    }

    private static List<string> LoadGuids()
    {
        string raw = EditorPrefs.GetString(RecentEditorPrefsKey, string.Empty);
        return string.IsNullOrEmpty(raw) ? new List<string>() : new List<string>(raw.Split('|'));
    }

    private static void SaveGuids(List<string> guids)
    {
        EditorPrefs.SetString(RecentEditorPrefsKey, string.Join("|", guids));
    }
}
#endif