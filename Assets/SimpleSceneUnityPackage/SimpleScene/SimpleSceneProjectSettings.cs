#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using MLG.SimpleSceneTool.GraphWindow;
namespace MLG.SimpleSceneTool
{

    [FilePath("ProjectSettings/SimpleSceneProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class SimpleSceneProjectSettings : ScriptableSingleton<SimpleSceneProjectSettings>
    {
        [Tooltip("The first scene in the player build and the starting point for scene discovery.")]
        public SceneAsset baseScene;

        [Tooltip("Assets loaded outside the scene graph, for example an Addressables bootstrap asset. " +
                 "Scene-set databases loaded through Resources are found automatically.")]
        public List<ScriptableObject> additionalGraphRoots = new List<ScriptableObject>();

        [HideInInspector]
        public List<SimpleSceneGraphNode> sceneGraph = new List<SimpleSceneGraphNode>();

        [HideInInspector]
        public List<SimpleSceneGraphEdge> sceneGraphEdges = new List<SimpleSceneGraphEdge>();

        [HideInInspector]
        public List<SimpleSceneGraphAssetLink> assetLinks = new List<SimpleSceneGraphAssetLink>();

        [HideInInspector]
        public List<SimpleSceneGraphExternalReference> externalReferences = new List<SimpleSceneGraphExternalReference>();

        [Serializable]
        public class SimpleSceneGraphNode
        {
            public SceneAsset scene;
            public List<SceneAsset> outgoingScenes = new List<SceneAsset>();
            public List<SceneAsset> incomingScenes = new List<SceneAsset>();
        }

        [Serializable]
        public class SimpleSceneGraphReference
        {
            public Object rootSource;
            public Object sourceAsset;
            public string gameObjectPath;
            public string componentType;
            public string propertyPath;
            public int referenceCount = 1;
        }

        [Serializable]
        public class SimpleSceneGraphEdge
        {
            public SceneAsset sourceScene;
            public SceneAsset targetScene;
            public int referenceCount;
            public List<SimpleSceneGraphReference> references = new List<SimpleSceneGraphReference>();
        }

        [Serializable]
        public class SimpleSceneGraphAssetLink
        {
            public SceneAsset rootScene;
            public Object sourceAsset;
            public Object targetAsset;
            public string gameObjectPath;
            public string componentType;
            public string propertyPath;
            public int referenceCount = 1;
        }

        [Serializable]
        public class SimpleSceneGraphExternalReference
        {
            public Object sourceAsset;
            public SceneAsset targetScene;
            public string propertyPath;
            public int referenceCount = 1;
        }

        public void ResetGraph()
        {
            sceneGraph.Clear();
            sceneGraphEdges.Clear();
            assetLinks.Clear();
            externalReferences.Clear();
        }

        public void AddSceneReference(SceneAsset sourceScene, SceneAsset targetScene, SimpleSceneGraphReference reference)
        {
            if (targetScene == null)
            {
                return;
            }

            EnsureNode(targetScene);
            if (sourceScene == null)
            {
                AddExternalReference(reference.sourceAsset, targetScene, reference.propertyPath);
                return;
            }

            SimpleSceneGraphNode sourceNode = EnsureNode(sourceScene);
            SimpleSceneGraphNode targetNode = EnsureNode(targetScene);
            if (!sourceNode.outgoingScenes.Contains(targetScene))
            {
                sourceNode.outgoingScenes.Add(targetScene);
            }
            if (!targetNode.incomingScenes.Contains(sourceScene))
            {
                targetNode.incomingScenes.Add(sourceScene);
            }

            SimpleSceneGraphEdge edge = sceneGraphEdges.Find(candidate =>
                candidate.sourceScene == sourceScene && candidate.targetScene == targetScene);
            if (edge == null)
            {
                edge = new SimpleSceneGraphEdge
                {

                    sourceScene = sourceScene,
                    targetScene = targetScene
                };
                sceneGraphEdges.Add(edge);

            }

            edge.referenceCount++;
            AddOrIncrementReference(edge.references, reference);
        }

        public void AddAssetLink(
            SceneAsset rootScene,
            Object sourceAsset,
            Object targetAsset,
            string gameObjectPath,
            string componentType,
            string propertyPath)
        {
            SimpleSceneGraphAssetLink existing = assetLinks.Find(candidate =>
                candidate.rootScene == rootScene &&
                candidate.sourceAsset == sourceAsset &&
                candidate.targetAsset == targetAsset &&
                candidate.gameObjectPath == gameObjectPath &&
                candidate.componentType == componentType &&
                candidate.propertyPath == propertyPath);

            if (existing != null)
            {
                existing.referenceCount++;
                return;
            }

            assetLinks.Add(new SimpleSceneGraphAssetLink
            {
                rootScene = rootScene,
                sourceAsset = sourceAsset,
                targetAsset = targetAsset,
                gameObjectPath = gameObjectPath,
                componentType = componentType,
                propertyPath = propertyPath
            });
        }

        /// <summary>
        /// Removes recorded object-to-object links that cannot eventually reach a
        /// SimpleScene reference. Discovery still visits those assets so it cannot
        /// miss a valid path; this only keeps the stored graph useful to inspect.
        /// </summary>
        public void PruneAssetLinksWithoutSceneDependencies()
        {
            HashSet<Object> sceneRelevantAssets = new HashSet<Object>();

            foreach (SimpleSceneGraphEdge edge in sceneGraphEdges)
            {
                foreach (SimpleSceneGraphReference reference in edge.references)
                {
                    if (reference.sourceAsset != null)
                    {
                        sceneRelevantAssets.Add(reference.sourceAsset);
                    }
                }
            }

            foreach (SimpleSceneGraphExternalReference reference in externalReferences)
            {
                if (reference.sourceAsset != null)
                {
                    sceneRelevantAssets.Add(reference.sourceAsset);
                }
            }

            bool foundNewRelevantAsset;
            do
            {
                foundNewRelevantAsset = false;
                foreach (SimpleSceneGraphAssetLink link in assetLinks)
                {
                    if (link.targetAsset != null && sceneRelevantAssets.Contains(link.targetAsset) &&
                        link.sourceAsset != null && sceneRelevantAssets.Add(link.sourceAsset))
                    {
                        foundNewRelevantAsset = true;
                    }
                }
            }
            while (foundNewRelevantAsset);

            assetLinks.RemoveAll(link =>
                link.targetAsset == null || !sceneRelevantAssets.Contains(link.targetAsset));
        }

        private SimpleSceneGraphNode EnsureNode(SceneAsset scene)
        {
            SimpleSceneGraphNode node = sceneGraph.Find(candidate => candidate.scene == scene);
            if (node == null)
            {
                node = new SimpleSceneGraphNode { scene = scene };
                sceneGraph.Add(node);
            }
            return node;
        }

        private void AddExternalReference(Object sourceAsset, SceneAsset targetScene, string propertyPath)
        {
            SimpleSceneGraphExternalReference existing = externalReferences.Find(candidate =>
                candidate.sourceAsset == sourceAsset &&
                candidate.targetScene == targetScene &&
                candidate.propertyPath == propertyPath);
            if (existing != null)
            {
                existing.referenceCount++;
                return;
            }

            externalReferences.Add(new SimpleSceneGraphExternalReference
            {
                sourceAsset = sourceAsset,
                targetScene = targetScene,
                propertyPath = propertyPath
            });
        }

        private static void AddOrIncrementReference(
            List<SimpleSceneGraphReference> references,
            SimpleSceneGraphReference reference)
        {
            SimpleSceneGraphReference existing = references.Find(candidate =>
                candidate.sourceAsset == reference.sourceAsset &&
                candidate.gameObjectPath == reference.gameObjectPath &&
                candidate.componentType == reference.componentType &&
                candidate.propertyPath == reference.propertyPath);
            if (existing != null)
            {
                existing.referenceCount++;
                return;
            }

            references.Add(reference);
        }
        public void Save()
        {
            Save(true);
        }
    }

    internal class SimpleSceneRefreshWindow : EditorWindow
    {
        private const float Width = 340f;
        private const float Height = 230f;

        [MenuItem("Tools/SimpleScene/Refresh Simple Scene Indexes")]
        public static void Open()
        {
            SimpleSceneRefreshWindow window = GetWindow<SimpleSceneRefreshWindow>("SimpleScene Refresh");
            window.minSize = new Vector2(Width, Height);
            window.maxSize = new Vector2(Width, Height);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Refresh Graph rebuilds Build Settings from the base scene, reachable prefabs and ScriptableObjects, " +
                "and registered graph roots. It removes stale scenes, including excluded scene sets.",
                MessageType.Info);
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Build Settings and graph."))
            {
                SimpleSceneProjectUpdater.RefreshGraph();
            }
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Scene Graph Viewer"))
            {
                SimpleSceneGraphWindow.Open();
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Refresh every SimpleScene is the standalone compatibility command. It intentionally searches all assets.",
                MessageType.None);
            if (GUILayout.Button("Refresh every SimpleScene"))
            {
                SimpleSceneIndexer.RefreshProject();
            }
        }
    }

    internal static class SimpleSceneSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSimpleSceneSettingsProvider()
        {
            return new SettingsProvider("Project/SimpleScene", SettingsScope.Project)
            {
                label = "SimpleScene",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>(new[]
                {
                "SimpleScene", "Scene", "Index", "Refresh", "Graph", "Build", "Prefab", "ScriptableObject"
            })
            };
        }

        private static void DrawSettings(string searchContext)
        {
            SimpleSceneProjectSettings settings = SimpleSceneProjectSettings.instance;
            SerializedObject serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("baseScene"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("additionalGraphRoots"), true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                settings.Save();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "The graph follows serialized references from the base scene. It enters only referenced prefabs and " +
                "ScriptableObjects, so unreferenced prefabs are not added to the build.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Build Settings and graph"))
            {
                SimpleSceneProjectUpdater.RefreshGraph();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Scene Graph Viewer"))
            {
                SimpleSceneGraphWindow.Open();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Collected graph", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                DrawExpandedList(serializedSettings.FindProperty("sceneGraph"), "Scene Nodes", GetNodeLabel);
                DrawExpandedList(serializedSettings.FindProperty("sceneGraphEdges"), "Scene Edges", GetEdgeLabel);
                DrawExpandedList(serializedSettings.FindProperty("assetLinks"), "Asset Links", GetAssetLinkLabel);
                DrawExpandedList(serializedSettings.FindProperty("externalReferences"), "External References", GetExternalReferenceLabel);
            }
        }

        private delegate string ElementLabelProvider(SerializedProperty property, int index);

        private static void DrawExpandedList(SerializedProperty list, string label, ElementLabelProvider getLabel)
        {
            EditorGUILayout.LabelField(label + " (" + list.arraySize + ")", EditorStyles.boldLabel);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                ExpandPropertyTree(element);
                EditorGUILayout.PropertyField(element, new GUIContent(getLabel(element, i)), true);
            }
        }

        private static void ExpandPropertyTree(SerializedProperty property)
        {
            property.isExpanded = true;
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            while (iterator.NextVisible(true) && !SerializedProperty.EqualContents(iterator, end))
            {
                iterator.isExpanded = true;
            }
        }

        private static string GetNodeLabel(SerializedProperty property, int index)
        {
            return GetObjectName(property.FindPropertyRelative("scene"), "Scene node " + (index + 1));
        }

        private static string GetEdgeLabel(SerializedProperty property, int index)
        {
            return GetObjectName(property.FindPropertyRelative("sourceScene"), "External") +
                   " → " + GetObjectName(property.FindPropertyRelative("targetScene"), "Missing target");
        }

        private static string GetAssetLinkLabel(SerializedProperty property, int index)
        {
            return GetObjectName(property.FindPropertyRelative("sourceAsset"), "Scene") +
                   " → " + GetObjectName(property.FindPropertyRelative("targetAsset"), "Missing target");
        }

        private static string GetExternalReferenceLabel(SerializedProperty property, int index)
        {
            return GetObjectName(property.FindPropertyRelative("sourceAsset"), "External") +
                   " → " + GetObjectName(property.FindPropertyRelative("targetScene"), "Missing target");
        }

        private static string GetObjectName(SerializedProperty property, string fallback)
        {
            Object value = property == null ? null : property.objectReferenceValue;
            return value == null ? fallback : value.name;
        }
    }

    /// <summary>
    /// Rebuilds the player scene list from actual serialized references. The collector keeps both
    /// scene-to-scene edges and the asset/property provenance needed by a future graph visualizer.
    /// </summary>
    internal static class SimpleSceneProjectUpdater
    {
        public static void RefreshGraph()
        {
            SimpleSceneProjectSettings settings = SimpleSceneProjectSettings.instance;
            if (settings.baseScene == null)
            {
                Debug.LogError("No base scene was selected. Assign one in Project Settings/SimpleScene before rebuilding the graph.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorBuildSettingsScene[] previousBuildSettings = EditorBuildSettings.scenes;
            try
            {
                SimpleSceneGraphCollector collector = new SimpleSceneGraphCollector(settings);
                collector.Collect();
                settings.Save();
                AssetDatabase.SaveAssets();
                Debug.Log("SimpleScene graph refresh completed.");
            }
            catch
            {
                EditorBuildSettings.scenes = previousBuildSettings;
                throw;
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }
    }

    internal sealed class SimpleSceneGraphCollector
    {
        private sealed class AssetWorkItem
        {
            public Object asset;
            public SceneAsset rootScene;
        }

        private readonly SimpleSceneProjectSettings settings;
        private readonly Queue<SceneAsset> pendingScenes = new Queue<SceneAsset>();
        private readonly Queue<AssetWorkItem> pendingAssets = new Queue<AssetWorkItem>();
        private readonly HashSet<string> processedScenePaths = new HashSet<string>();
        private readonly HashSet<string> processedAssetContexts = new HashSet<string>();
        private readonly HashSet<string> queuedScenePaths = new HashSet<string>();
        private readonly HashSet<string> queuedAssetContexts = new HashSet<string>();

        public SimpleSceneGraphCollector(SimpleSceneProjectSettings settings)
        {
            this.settings = settings;
        }

        public void Collect()
        {
            settings.ResetGraph();
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];

            EnqueueScene(settings.baseScene);
            foreach (ScriptableObject root in settings.additionalGraphRoots)
            {
                EnqueueAsset(root, null);
            }
            EnqueueAutomaticRoots();

            while (pendingScenes.Count > 0 || pendingAssets.Count > 0)
            {
                if (pendingScenes.Count > 0)
                {
                    ProcessScene(pendingScenes.Dequeue());
                }
                else
                {
                    ProcessAsset(pendingAssets.Dequeue());
                }
            }

            settings.PruneAssetLinksWithoutSceneDependencies();
        }

        private void EnqueueAutomaticRoots()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject candidate = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                ISimpleSceneAutoGraphRoot automaticRoot = candidate as ISimpleSceneAutoGraphRoot;
                if (automaticRoot != null && automaticRoot.IncludeInSimpleSceneBuild)
                {
                    EnqueueAsset(candidate, null);
                }
            }
        }

        private void EnqueueScene(SceneAsset sceneAsset)
        {
            if (sceneAsset == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            SimpleSceneIndexer.ForceGetIndexOf(path);
            EnsureSceneNode(sceneAsset);
            if (queuedScenePaths.Add(path))
            {
                pendingScenes.Enqueue(sceneAsset);
            }
        }

        private void EnqueueAsset(Object candidate, SceneAsset rootScene)
        {
            if (candidate == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(candidate);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(mainAsset is ScriptableObject) && !IsPrefab(mainAsset))
            {
                return;
            }
            ISimpleSceneAutoGraphRoot automaticRoot = mainAsset as ISimpleSceneAutoGraphRoot;
            if (automaticRoot != null && !automaticRoot.IncludeInSimpleSceneBuild)
            {
                return;
            }

            string rootPath = rootScene == null ? "<external>" : AssetDatabase.GetAssetPath(rootScene);
            string contextKey = path + "|" + rootPath;
            if (queuedAssetContexts.Add(contextKey))
            {
                pendingAssets.Enqueue(new AssetWorkItem
                {
                    asset = mainAsset,
                    rootScene = rootScene
                });
            }
        }

        private void ProcessScene(SceneAsset sceneAsset)
        {
            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(path) || !processedScenePaths.Add(path))
            {
                return;
            }

            SimpleSceneIndexer.ForceGetIndexOf(path);
            EnsureSceneNode(sceneAsset);

            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasAlreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!wasAlreadyOpen)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ScanGameObject(root, sceneAsset, sceneAsset, ref changed);
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private void ProcessAsset(AssetWorkItem item)
        {
            string path = AssetDatabase.GetAssetPath(item.asset);
            string rootPath = item.rootScene == null ? "<external>" : AssetDatabase.GetAssetPath(item.rootScene);
            if (string.IsNullOrEmpty(path) || !processedAssetContexts.Add(path + "|" + rootPath))
            {
                return;
            }

            ISimpleSceneGraphRoot graphRoot = item.asset as ISimpleSceneGraphRoot;
            if (graphRoot != null)
            {
                bool changed = false;
                foreach (SimpleScene simpleScene in graphRoot.GetSimpleScenesForBuild())
                {
                    changed |= ProcessSimpleSceneValue(
                        simpleScene,
                        item.asset,
                        item.rootScene,
                        string.Empty,
                        item.asset.GetType().FullName,
                        "Scene-set entry");
                }
                if (changed)
                {
                    EditorUtility.SetDirty(item.asset);
                }
                return;
            }

            ScriptableObject scriptableObject = item.asset as ScriptableObject;
            if (scriptableObject != null)
            {
                ScanSerializedObject(scriptableObject, item.asset, item.rootScene, string.Empty, scriptableObject.GetType().FullName, false);
                return;
            }

            GameObject prefabAsset = item.asset as GameObject;
            if (prefabAsset == null || !IsPrefab(prefabAsset))
            {
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                ScanGameObject(prefabContents, prefabAsset, item.rootScene, ref changed);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private void ScanGameObject(GameObject gameObject, Object sourceAsset, SceneAsset rootScene, ref bool changed)
        {
            if (IsPrefabInstanceRoot(gameObject))
            {
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (sourcePrefab != null)
                {
                    settings.AddAssetLink(
                        rootScene,
                        sourceAsset,
                        sourcePrefab,
                        GetGameObjectPath(gameObject),
                        string.Empty,
                        "Prefab instance");
                    EnqueueAsset(sourcePrefab, rootScene);
                }

                ScanPrefabInstanceOverrides(gameObject, sourceAsset, rootScene);
                return;
            }

            string gameObjectPath = GetGameObjectPath(gameObject);
            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component != null)
                {
                    changed |= ScanSerializedObject(
                        component,
                        sourceAsset,
                        rootScene,
                        gameObjectPath,
                        component.GetType().FullName,
                        false);
                }
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                ScanGameObject(transform.GetChild(i).gameObject, sourceAsset, rootScene, ref changed);
            }
        }

        private void ScanPrefabInstanceOverrides(GameObject prefabRoot, Object sourceAsset, SceneAsset rootScene)
        {
            MonoBehaviour[] components = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                if (component != null)
                {
                    ScanSerializedObject(
                        component,
                        sourceAsset,
                        rootScene,
                        GetGameObjectPath(component.gameObject),
                        component.GetType().FullName,
                        true);
                }
            }
        }

        private bool ScanSerializedObject(
            Object serializedTarget,
            Object sourceAsset,
            SceneAsset rootScene,
            string gameObjectPath,
            string componentType,
            bool prefabOverridesOnly)
        {
            SerializedObject serializedObject = new SerializedObject(serializedTarget);
            SerializedProperty property = serializedObject.GetIterator();
            bool changed = false;
            bool enterChildren = true;

            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType == SerializedPropertyType.Generic && property.type == nameof(SimpleScene))
                {
                    SerializedProperty sceneAssetProperty = property.FindPropertyRelative("sceneAsset");
                    if (sceneAssetProperty != null && (!prefabOverridesOnly || sceneAssetProperty.prefabOverride))
                    {
                        changed |= ProcessSimpleSceneProperty(
                            property,
                            sourceAsset,
                            rootScene,
                            gameObjectPath,
                            componentType,
                            !prefabOverridesOnly);
                    }
                    enterChildren = false;
                    continue;
                }

                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                {
                    continue;
                }
                if (prefabOverridesOnly && !property.prefabOverride)
                {
                    continue;
                }

                QueueReferencedAsset(
                    property.objectReferenceValue,
                    sourceAsset,
                    rootScene,
                    gameObjectPath,
                    componentType,
                    property.propertyPath);
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedTarget);
            }
            return changed;
        }

        private bool ProcessSimpleSceneProperty(
            SerializedProperty simpleSceneProperty,
            Object sourceAsset,
            SceneAsset rootScene,
            string gameObjectPath,
            string componentType,
            bool updateIndex)
        {
            SerializedProperty sceneAssetProperty = simpleSceneProperty.FindPropertyRelative("sceneAsset");
            SceneAsset targetScene = sceneAssetProperty == null
                ? null
                : sceneAssetProperty.objectReferenceValue as SceneAsset;
            if (targetScene == null)
            {
                return false;
            }

            EnqueueScene(targetScene);
            settings.AddSceneReference(rootScene, targetScene, new SimpleSceneProjectSettings.SimpleSceneGraphReference
            {
                sourceAsset = sourceAsset,
                gameObjectPath = gameObjectPath,
                componentType = componentType,
                propertyPath = simpleSceneProperty.propertyPath
            });

            if (!updateIndex)
            {
                return false;
            }

            SerializedProperty indexProperty = simpleSceneProperty.FindPropertyRelative("index");
            if (indexProperty == null)
            {
                return false;
            }

            int buildIndex = SimpleSceneIndexer.ForceGetIndexOf(AssetDatabase.GetAssetPath(targetScene));
            if (indexProperty.intValue == buildIndex)
            {
                return false;
            }

            indexProperty.intValue = buildIndex;
            return true;
        }

        private bool ProcessSimpleSceneValue(
            SimpleScene simpleScene,
            Object sourceAsset,
            SceneAsset rootScene,
            string gameObjectPath,
            string componentType,
            string propertyPath)
        {
            if (simpleScene == null || simpleScene.sceneAsset == null)
            {
                return false;
            }

            EnqueueScene(simpleScene.sceneAsset);
            settings.AddSceneReference(rootScene, simpleScene.sceneAsset, new SimpleSceneProjectSettings.SimpleSceneGraphReference
            {
                sourceAsset = sourceAsset,
                gameObjectPath = gameObjectPath,
                componentType = componentType,
                propertyPath = propertyPath
            });
            return simpleScene.RefreshIndex();
        }

        private void QueueReferencedAsset(
            Object referencedObject,
            Object sourceAsset,
            SceneAsset rootScene,
            string gameObjectPath,
            string componentType,
            string propertyPath)
        {
            string path = AssetDatabase.GetAssetPath(referencedObject);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(mainAsset is ScriptableObject) && !IsPrefab(mainAsset))
            {
                return;
            }

            settings.AddAssetLink(
                rootScene,
                sourceAsset,
                mainAsset,
                gameObjectPath,
                componentType,
                propertyPath);
            EnqueueAsset(mainAsset, rootScene);
        }

        private static bool IsPrefab(Object asset)
        {
            GameObject gameObject = asset as GameObject;
            return gameObject != null && PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab;
        }

        private static bool IsPrefabInstanceRoot(GameObject gameObject)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }

            return PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) == gameObject;
        }

        private static string GetGameObjectPath(GameObject gameObject)
        {
            List<string> segments = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                segments.Add(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private void EnsureSceneNode(SceneAsset sceneAsset)
        {
            if (sceneAsset == null)
            {
                return;
            }
            if (settings.sceneGraph.Find(node => node.scene == sceneAsset) == null)
            {
                settings.sceneGraph.Add(new SimpleSceneProjectSettings.SimpleSceneGraphNode { scene = sceneAsset });
            }
        }
    }
#endif
}