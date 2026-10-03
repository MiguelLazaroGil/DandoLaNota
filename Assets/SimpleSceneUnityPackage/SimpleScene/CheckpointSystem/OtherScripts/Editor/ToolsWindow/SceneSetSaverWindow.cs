#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MLG.SimpleSceneTool.CheckpointSystem
{
    public class SceneSetSaverWindow : EditorWindow
    {
        private enum Mode
        {
            CreateNew,
            Overwrite
        }

        private Mode mode = Mode.CreateNew;
        private List<SimpleScene> openScenes = new List<SimpleScene>();

        // Create New mode
        private string newSceneSetName = "New Scene Set";
        private SceneSetDatabase targetDatabase;
        private bool excludeFromBuild = false;

        // Overwrite mode
        private SceneSet sceneSet;
        private bool sceneSetExpanded = true;

        [MenuItem("Tools/SimpleScene/Scene Set Saver")]
        public static void ShowWindow()
        {
            GetWindow<SceneSetSaverWindow>("Scene Set Saver");
        }

        private void OnGUI()
        {
            openScenes = GetOpenedScenes();

            mode = (Mode)GUILayout.Toolbar((int)mode, new[] { "Create New", "Overwrite Existing" });
            EditorGUILayout.Space();

            if (mode == Mode.CreateNew)
            {
                DrawCreateNewMode();
            }
            else
            {
                DrawOverwriteMode();
            }

            EditorGUILayout.Space();
            DrawScenes("Currently Open Scenes", openScenes);
        }

        private void DrawCreateNewMode()
        {
            EditorGUILayout.HelpBox("Creates a new Scene Set asset from the currently open scenes.", MessageType.Info);

            newSceneSetName = EditorGUILayout.TextField("Name", newSceneSetName);

            excludeFromBuild = EditorGUILayout.Toggle("Exclude From Build", excludeFromBuild);
            targetDatabase = (SceneSetDatabase)EditorGUILayout.ObjectField(
                "Database (optional)", targetDatabase, typeof(SceneSetDatabase), false);

            if (targetDatabase != null)
            {
                string databasePath = AssetDatabase.GetAssetPath(targetDatabase);
                string destinationFolder = Path.GetDirectoryName(databasePath).Replace('\\', '/')
                    + "/" + Path.GetFileNameWithoutExtension(databasePath);
                EditorGUILayout.HelpBox(
                    "Will be added to '" + targetDatabase.name + "' and saved under '" + destinationFolder + "'.",
                    MessageType.None);
            }

            using (new EditorGUI.DisabledScope(openScenes.Count == 0))
            {
                if (GUILayout.Button("Create Scene Set From Open Scenes"))
                {
                    CreateNewSceneSet();
                }
            }
        }

        private void DrawOverwriteMode()
        {
            EditorGUILayout.HelpBox("Replaces the scenes of an existing Scene Set with the currently open scenes.", MessageType.Info);

            sceneSet = SceneSetEditorGUI.DrawSceneSetPicker(
                new GUIContent("Scene Set"),
                sceneSet,
                ref sceneSetExpanded,
                true,
                false,
                selected => sceneSet = selected);

            if (sceneSet == null)
            {
                EditorGUILayout.HelpBox("Pick a Scene Set asset to overwrite.", MessageType.Info);
                return;
            }
            if (sceneSet.ExcludeFromBuild)
            {
                EditorGUILayout.HelpBox("This scene set is excluded from player builds. It can still be saved and opened for testing.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(openScenes.Count == 0))
            {
                if (GUILayout.Button("Save Open Scenes to Scene Set"))
                {
                    SaveCurrentScenes();
                }
            }
        }

        private static void DrawScenes(string title, IList<SimpleScene> scenes)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (scenes == null || scenes.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes.", MessageType.Info);
                return;
            }
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (SimpleScene scene in scenes)
                {
                    EditorGUILayout.ObjectField(scene == null ? null : scene.sceneAsset, typeof(SceneAsset), false);
                }
            }
        }

        private static List<SimpleScene> GetOpenedScenes()
        {
            List<SimpleScene> scenes = new List<SimpleScene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }
                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                if (asset != null)
                {
                    scenes.Add(new SimpleScene(asset));
                }
            }
            return scenes;
        }

        private void SaveCurrentScenes()
        {
            if (openScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("No Scenes Found", "There are no open scenes to save.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "Replace Scene Set",
                    "Replace the scenes in '" + sceneSet.DisplayName + "' with the currently open scenes?",
                    "Replace", "Cancel"))
            {
                return;
            }
            Undo.RecordObject(sceneSet, "Update Scene Set Scenes");
            sceneSet.ReplaceScenes(openScenes);
            AssetDatabase.SaveAssets();
        }

        private void CreateNewSceneSet()
        {
            if (openScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("No Scenes Found", "There are no open scenes to save.", "OK");
                return;
            }

            string assetName = string.IsNullOrWhiteSpace(newSceneSetName) ? "New Scene Set" : newSceneSetName.Trim();
            string folder = "Assets/Resources/SceneSets";

            if (targetDatabase != null)
            {
                string databasePath = AssetDatabase.GetAssetPath(targetDatabase);
                string databaseDir = Path.GetDirectoryName(databasePath).Replace('\\', '/');
                string databaseName = Path.GetFileNameWithoutExtension(databasePath);
                folder = databaseDir + "/" + databaseName;
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(databaseDir, databaseName);
                }
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + assetName + ".asset");

            SceneSet newSceneSet = ScriptableObject.CreateInstance<SceneSet>();
            newSceneSet.ReplaceScenes(openScenes);
            newSceneSet.SetDisplayName(assetName);
            newSceneSet.SetExcludeFromBuild(excludeFromBuild);
            EditorUtility.SetDirty(newSceneSet);
            AssetDatabase.CreateAsset(newSceneSet, assetPath);



            if (targetDatabase != null)
            {
                Undo.RecordObject(targetDatabase, "Add Scene Set to Database");
                targetDatabase.AddSceneSet(newSceneSet);
                EditorUtility.SetDirty(targetDatabase);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(newSceneSet);
            Selection.activeObject = newSceneSet;
        }
    }
#endif
}