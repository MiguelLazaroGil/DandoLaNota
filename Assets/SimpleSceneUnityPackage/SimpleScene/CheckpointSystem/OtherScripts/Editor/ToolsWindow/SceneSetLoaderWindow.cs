#if UNITY_EDITOR
using MLG.SimpleSceneTool.CheckpointSystem;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SceneSetLoaderWindow : EditorWindow
{
    private SceneSet sceneSet;
    private bool sceneSetExpanded = true;

    private SceneSetDatabase database;
    private bool databaseFoldout = true;

    [MenuItem("Tools/SimpleScene/Scene Set Loader")]
    public static void ShowWindow()
    {
        GetWindow<SceneSetLoaderWindow>("Scene Set Loader");
    }

    private void OnGUI()
    {
        sceneSet = SceneSetEditorGUI.DrawSceneSetPicker(
            new GUIContent("Scene Set"),
            sceneSet,
            ref sceneSetExpanded,
            true,
            false,
            selected => sceneSet = selected);

        if (sceneSet == null)
        {
            EditorGUILayout.HelpBox("Pick a Scene Set asset to open its scenes in the editor.", MessageType.Info);
        }
        else
        {
            if (sceneSet.ExcludeFromBuild)
            {
                EditorGUILayout.HelpBox("This scene set is excluded from player builds.", MessageType.Warning);
            }
            if (GUILayout.Button("Open Scene Set"))
            {
                SceneSetEditorLoader.Open(sceneSet);
            }
        }

        DrawDatabaseSection();
        DrawRecentSceneSets();
    }

    private void DrawDatabaseSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Load From Database", EditorStyles.boldLabel);
        database = (SceneSetDatabase)EditorGUILayout.ObjectField("Database", database, typeof(SceneSetDatabase), false);
        if (database == null)
        {
            return;
        }

        databaseFoldout = EditorGUILayout.Foldout(databaseFoldout, "Scene Sets in Database", true);
        if (!databaseFoldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        foreach (SceneSet entry in database.SceneSets)
        {
            if (entry != null)
            {
                DrawDatabaseEntryRow(entry, null);
            }
        }
        EditorGUI.indentLevel--;
    }

    private void DrawDatabaseEntryRow(SceneSet entry, string tag)
    {
        EditorGUILayout.BeginHorizontal();
        string label = string.IsNullOrEmpty(tag) ? entry.DisplayName : entry.DisplayName + "  [" + tag + "]";
        EditorGUILayout.LabelField(label);
        if (GUILayout.Button("Load", GUILayout.Width(60f)))
        {
            SceneSetEditorLoader.Open(entry);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRecentSceneSets()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recent", EditorStyles.boldLabel);

        List<SceneSet> recent = SceneSetEditorLoader.GetRecent();
        if (recent.Count == 0)
        {
            EditorGUILayout.HelpBox("Scene sets you open will appear here for quick access.", MessageType.None);
            return;
        }

        foreach (SceneSet recentSet in recent)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(recentSet.DisplayName);
            if (GUILayout.Button("Load", GUILayout.Width(60f)))
            {
                SceneSetEditorLoader.Open(recentSet);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Clear Recent"))
        {
            SceneSetEditorLoader.ClearRecent();
        }
    }
}
#endif