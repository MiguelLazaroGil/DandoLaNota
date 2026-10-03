#region imports
using UnityEngine;
using System.Linq;





#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;


#endif
#endregion
namespace MLG.SimpleSceneTool
{
    /// <summary>
    /// Lets an optional package contribute scenes to the project scene graph without
    /// making <see cref="SimpleScene"/> depend on that package. Implement this on a
    /// ScriptableObject when its scene references need custom filtering.
    /// </summary>
    public interface ISimpleSceneGraphRoot
    {
        System.Collections.Generic.IEnumerable<SimpleScene> GetSimpleScenesForBuild();
    }

    /// <summary>
    /// Marks a graph root that is discovered even when nothing in the base scene
    /// references it. This is intended for bootstrapped assets such as databases
    /// loaded through Resources or Addressables.
    /// </summary>
    public interface ISimpleSceneAutoGraphRoot
    {
        bool IncludeInSimpleSceneBuild { get; }
    }

    /// <summary>
    /// Simple serializable class to reference scenes by index in the build but with easy drag and drop of UnistyAssets in the field.
    /// Use simpleScene whenever you would use a string or int to reference a scene.
    /// Only one public field: myScene.Index
    /// </summary>
    [System.Serializable]
    public class SimpleScene
    {
#if UNITY_EDITOR
        [CustomLabel(""), Tooltip("Scene asset which will be referenced by this Index. \nDont worry, the build's scene list will be updated automatically if necessary.")]
        public SceneAsset sceneAsset = null;
#endif
        [SerializeField]
        private int index = -1;
        /// <summary>
        /// Unity's build index of this project. Use as you would an int to load scenes, ie : SceneManager.LoadScene(myScene.Index);
        /// </summary>
        public int Index
        {
            get
            {
#if UNITY_EDITOR
                if (sceneAsset == null)
                {
                    Debug.LogError("SceneAsset is null! This will cause a crash if used in a final build.");
                    return -1;
                }

                var tempindex = SimpleSceneIndexer.ForceGetIndexOf(UnityEditor.AssetDatabase.GetAssetPath(sceneAsset));
                if (tempindex != index)
                {
                    //TODO: que rediriga con un click a este project settings o a la ventana de refresh
                    //TODO: que diga que SimpleScene es la que esta desactualizada
                    Debug.LogWarning("The indexes of certain SimpleScenes are outdated. If this scene {" + sceneAsset.name + "} should be included in the final build (not a testing scene) try refreshing them via Tools/SimpleScene - Refresh values.");
                }
                index = tempindex;

#endif
                return index;

            }
        }

#if UNITY_EDITOR
        public SimpleScene(SceneAsset sceneAsset)
        {
            this.sceneAsset = sceneAsset;
            if (sceneAsset != null)
            {
                index = SimpleSceneIndexer.ForceGetIndexOf(UnityEditor.AssetDatabase.GetAssetPath(sceneAsset));
            }
        }

        /// <summary>
        /// Updates the serialized runtime cache without inspecting unrelated assets.
        /// Project graph collection uses this when it reaches a SimpleScene through a
        /// custom graph root.
        /// </summary>
        internal bool RefreshIndex()
        {
            if (sceneAsset == null)
            {
                return false;
            }

            int newIndex = SimpleSceneIndexer.ForceGetIndexOf(UnityEditor.AssetDatabase.GetAssetPath(sceneAsset));
            if (index == newIndex)
            {
                return false;
            }

            index = newIndex;
            return true;
        }

#endif
    }


    #region Backend
#if UNITY_EDITOR

    #region Indexing
    /// <summary>
    /// Utility class for managing SimpleScene indexes.
    /// </summary>
    internal static class SimpleSceneIndexer
    {
        /// <summary>
        /// Gets the build index of a scene by its path. If the scene is not in the build settings, it will be added and assigned the next available index.
        /// </summary>
        /// <param name="scenePath">Name of the desired scene</param>
        /// <returns>Build index of scene, always a valid index</returns>
        public static int ForceGetIndexOf(string scenePath)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex < 0)  //if the scene is not in the build settings, add it
            {
                buildIndex = SceneUtilities.AddSceneToBuild(scenePath);

            }
            return buildIndex;
        }
        /// <summary>
        /// Refresehes every SimpleScene in project
        /// </summary>
        public static void RefreshProject()
        {
            HashSet<Object> processedObjects = new HashSet<Object>();

            UpdateAllScriptableObjs(processedObjects);

            UpdateAllPrefabs(processedObjects);

            SceneUtilities.GetOpenedScenes(out List<Scene> openedScenes, out List<string> openedScenesPaths);

            EditorSceneManager.SaveModifiedScenesIfUserWantsTo(openedScenes.ToArray());

            foreach (Scene scene in openedScenes) //update opened scenes first for performance 
            {
                UpdateScene(scene, processedObjects);
            }


            //update All other scenes
            string[] scenePaths = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);

            foreach (var scenePath in scenePaths)
            {
                if (openedScenesPaths.Contains(scenePath))
                    continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                UpdateScene(scene, processedObjects);
            }


            //Reopen scenes
            SceneUtilities.OpenScenesInEditor(openedScenesPaths);

            AssetDatabase.SaveAssets();
            //TODO: que rediriga con un click a este project settings
            Debug.Log("SimpleScene index refresh completed. Go to project settings -> SimpleScene to see what has changed.");
        }

        public static bool UpdateScene(Scene scene, HashSet<Object> ignore)
        {

            Debug.Log("Updating scene: " + scene.path);
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!ignore.Contains(mb))
                    {
                        ignore.Add(mb);
                        changed = changed || RefreshSimpleSceneInObject(mb);
                    }

                }
            }
            if (changed)
            {//this should be handled already
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return changed;
        }
        public static bool UpdateAllScriptableObjs(HashSet<Object> ignore)
        {
            string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string guid in soGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                {
                    if (!ignore.Contains(so))
                    {
                        ignore.Add(so);
                        RefreshSimpleSceneInObject(so);
                    }
                }
            }
            return false;
        }
        public static bool UpdateAllPrefabs(HashSet<Object> ignore)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!ignore.Contains(mb))
                        {
                            ignore.Add(mb);
                            SimpleSceneIndexer.RefreshSimpleSceneInObject(mb);
                        }
                    }
                }
            }
            return false;

        }


        /// <summary>
        /// Refresehes any SimpleScene found in the serialized fields of an object
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="processed"></param>
        /// <returns>True if the serialization of the obj changed</returns>
        private static bool RefreshSimpleSceneInObject(Object obj)
        {

            SerializedObject serializedObject = new SerializedObject(obj);
            SerializedProperty prop = serializedObject.GetIterator();

            bool modified = false;
            Debug.Log("Exploring object: " + obj.name + " of type " + obj.GetType() + " for SimpleScene properties."); //TODO: eliminar este log
            while (prop.NextVisible(true)) //iterate over all serialized properties of the object, including nested ones
            {
                if (prop.propertyType == SerializedPropertyType.Generic &&
                    prop.type == nameof(SimpleScene)) //take SimpleScene
                {
                    SerializedProperty indexProp = prop.FindPropertyRelative("index");
                    SerializedProperty sceneAssetProp = prop.FindPropertyRelative("sceneAsset");

                    if (sceneAssetProp != null && sceneAssetProp.objectReferenceValue != null)
                    {
                        string scenePath = AssetDatabase.GetAssetPath(sceneAssetProp.objectReferenceValue);
                        int newIndex = ForceGetIndexOf(scenePath);

                        if (indexProp.intValue != newIndex)
                        {
                            indexProp.intValue = newIndex;
                            modified = true;
                        }
                    }
                }
            }

            if (modified)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(obj);
            }
            return modified;
        }

    }
    #endregion

    #region Drawer
    /// <summary>
    /// Drawer for SimpleScene.
    /// Simple scene will occupy one line in the inspector.
    /// Changing the sceneAsset field will automatically update the index field and the build's scene list if necessary.
    /// </summary>

    [CustomPropertyDrawer(typeof(SimpleScene))]
    internal class SimpleSceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty indexProp = property.FindPropertyRelative("index");
            SerializedProperty sceneAssetProp = property.FindPropertyRelative("sceneAsset");
            EditorGUI.BeginChangeCheck();
            Object newValue = EditorGUI.ObjectField(
             position,
             label,
             sceneAssetProp.objectReferenceValue,
             typeof(SceneAsset),
             false
            );

            if (EditorGUI.EndChangeCheck())
            {
                sceneAssetProp.objectReferenceValue = newValue;
                if (sceneAssetProp.objectReferenceValue != null)
                {
                    string scenePath = AssetDatabase.GetAssetPath(sceneAssetProp.objectReferenceValue);
                    indexProp.intValue = SimpleSceneIndexer.ForceGetIndexOf(scenePath);
                }
                else
                {
                    indexProp.intValue = -1;
                }

            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

    }


    #endregion

    #region Build

    /// <summary>
    /// Build preprocessor which ensures all SimpleScene indexes are updated before making a build. 
    /// </summary>
    internal class SceneIndexBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("Refreshing all SimpleScene indexes before build...");
#if SIMPLE_SCENE_PROJECT
            Debug.Log("SimpleSceneProjectSettings found, refreshing graph from baseScene reference...");
            SimpleSceneProjectUpdater.RefreshGraph();
#else
        SimpleSceneIndexer.RefreshProject();
#endif

        }
    }

    #endregion

    #region OtherUtilities
    /// <summary>
    /// Some scenes utilities used in different classes.
    /// </summary>
    internal static class SceneUtilities
    {
        /// <summary>
        /// Adds scene to build. Undefined behavoiur if the scene is already in the build.
        /// </summary>
        /// <param name="scenePath"></param>
        /// <returns>Index of said scene</returns>
        public static int AddSceneToBuild(string scenePath)
        {
            var newScene = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;

            var updatedScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            existingScenes.CopyTo(updatedScenes, 0);
            updatedScenes[existingScenes.Length] = newScene;
            EditorBuildSettings.scenes = updatedScenes;
            int buildIndex = existingScenes.Length; //last added
            Debug.Log("Scene added to build settings: " + scenePath + " at index " + buildIndex);

            return buildIndex; //last added 

        }
        /// <summary>
        /// Gets currently opened scenes.
        /// </summary>
        /// <param name="openedScenes">Currently opened scenes</param>
        /// <param name="openedScenesPaths">Currently opened scenes' paths</param>
        public static void GetOpenedScenes(out List<Scene> openedScenes, out List<string> openedScenesPaths)
        {
            openedScenes = new List<Scene>();
            openedScenesPaths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    openedScenes.Add(scene);
                    openedScenesPaths.Add(scene.path);
                }
            }

        }
        /// <summary>
        /// Opens scenes in editor. Doesnt check for modified changes of other opened scenes.
        /// </summary>
        /// <param name="scenePaths"></param>
        public static void OpenScenesInEditor(List<string> scenePaths)
        {
            if (scenePaths.Count > 0)
            {

                for (int i = 0; i < scenePaths.Count; i++)
                {
                    if (!string.IsNullOrEmpty(scenePaths[i]))
                    {
                        EditorSceneManager.OpenScene(scenePaths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                    }
                }

            }
        }
    }

    /// <summary>
    /// If the developer only uses SimpleScene, some funcionality of the package will be lost. This class checks for the presence of SimpleSceneProjectSettings so that additional code can be run.
    /// </summary>
    [InitializeOnLoad]
    public static class OptionalScriptDefine
    {
        const string DEFINE = "SIMPLE_SCENE_PROJECT";
        const string SCRIPT_NAME = "SimpleSceneProjectSettings";

        static OptionalScriptDefine()
        {
            bool exists = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Any(t => t.Name == SCRIPT_NAME);

            BuildTargetGroup target = EditorUserBuildSettings.selectedBuildTargetGroup;
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(target);

            string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            if (exists && !defines.Contains(DEFINE))
            {
                defines += ";" + DEFINE;
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
            }
            else if (!exists && defines.Contains(DEFINE))
            {
                defines = defines.Replace(DEFINE, "");
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
            }
        }
    }
    #endregion
#endif
    #endregion
}