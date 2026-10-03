#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MLG.SimpleSceneTool.GraphWindow
{
    /// <summary>
    /// Pure data reader for the SimpleScene reference graph. Turns the data already computed and
    /// stored by SimpleSceneProjectSettings (via SimpleSceneProjectUpdater.RefreshGraph) into a
    /// graph of nodes and edges that a GraphView (or anything else) can render.
    ///
    /// This class only knows about SimpleScene, SceneAsset, and the ISimpleSceneGraphRoot /
    /// ISimpleSceneAutoGraphRoot interfaces. It has zero knowledge of SceneSet, SceneSetDatabase,
    /// or any other optional package built on top of SimpleScene.
    /// </summary>
    internal sealed class SimpleSceneGraphModel
{
    private const string ExternalContext = "<external>";
    private const int MaxChainsPerReference = 25;

    public sealed class NodeData
    {
        public string Id;
        public bool IsProvider;
        public bool IsBaseScene;
        public SceneAsset SceneAsset;
        public Object ProviderAsset;
        public string DisplayName;
        public readonly List<EdgeData> Outgoing = new List<EdgeData>();
        public readonly List<EdgeData> Incoming = new List<EdgeData>();
    }

    /// <summary>One asset visited along a reference chain, and how it was reached.</summary>
    public sealed class ChainHop
    {
        public Object Asset;
        public string ViaComponent;
        public string ViaGameObjectPath;
        public string ViaProperty;
    }

    /// <summary>
    /// One concrete reason an edge exists. For a scene-origin edge, ComponentType/GameObjectPath
    /// describe the actual in-scene component that starts this specific route - the same final
    /// target reached from two different GameObjects produces two separate attributions, not one.
    /// Not meaningful for a provider-origin edge (there is no scene/GameObject at a provider
    /// root), where ComponentType/GameObjectPath are simply left blank.
    /// </summary>
    public sealed class AttributionData
    {
        public string ComponentType;
        public string GameObjectPath;
        public string PropertyPath;
        public int ReferenceCount = 1;

        // Assets visited after the origin, ending with whatever actually holds the SimpleScene
        // reference (e.g. a SceneSet). Empty for a direct reference.
        public readonly List<ChainHop> Chain = new List<ChainHop>();

        public bool IsDirect => Chain.Count == 0;
    }

    public sealed class EdgeData
    {
        public string Id;
        public NodeData From;
        public NodeData To;
        public readonly List<AttributionData> Attributions = new List<AttributionData>();
    }

    private sealed class LinkHop
    {
        public string ToPath;
        public Object ToObject;
        public string ComponentType;
        public string GameObjectPath;
        public string PropertyPath;
    }

    public readonly List<NodeData> Nodes = new List<NodeData>();
    public readonly List<EdgeData> Edges = new List<EdgeData>();
    public NodeData BaseSceneNode;

    private readonly SimpleSceneProjectSettings settings;
    private readonly Dictionary<string, NodeData> nodesById = new Dictionary<string, NodeData>();
    private readonly Dictionary<string, EdgeData> edgesById = new Dictionary<string, EdgeData>();

    // contextKey -> fromAssetPath -> hops. contextKey is either a scene's asset path (links
    // discovered while scanning that scene) or ExternalContext (discovered via an
    // additionalGraphRoots / ISimpleSceneAutoGraphRoot entry point, outside any scene).
    private readonly Dictionary<string, Dictionary<string, List<LinkHop>>> adjacency =
        new Dictionary<string, Dictionary<string, List<LinkHop>>>();

    private SimpleSceneGraphModel(SimpleSceneProjectSettings settings)
    {
        this.settings = settings;
    }

    public static SimpleSceneGraphModel Build(SimpleSceneProjectSettings settings)
    {
        SimpleSceneGraphModel model = new SimpleSceneGraphModel(settings);
        model.BuildAdjacency();
        model.BuildSceneNodesAndEdges();
        model.BuildProviderNodesAndEdges();
        return model;
    }

    #region Building

    private void BuildAdjacency()
    {
        if (settings.assetLinks == null)
        {
            return;
        }

        foreach (var link in settings.assetLinks)
        {
            if (link.sourceAsset == null || link.targetAsset == null)
            {
                continue;
            }

            string context = link.rootScene != null
                ? AssetDatabase.GetAssetPath(link.rootScene)
                : ExternalContext;
            string fromPath = AssetDatabase.GetAssetPath(link.sourceAsset);
            string toPath = AssetDatabase.GetAssetPath(link.targetAsset);
            if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath))
            {
                continue;
            }

            AddHop(context, fromPath, new LinkHop
            {
                ToPath = toPath,
                ToObject = link.targetAsset,
                ComponentType = link.componentType,
                GameObjectPath = link.gameObjectPath,
                PropertyPath = link.propertyPath
            });
        }
    }

    private void AddHop(string context, string fromPath, LinkHop hop)
    {
        if (!adjacency.TryGetValue(context, out var forContext))
        {
            forContext = new Dictionary<string, List<LinkHop>>();
            adjacency[context] = forContext;
        }
        if (!forContext.TryGetValue(fromPath, out var hops))
        {
            hops = new List<LinkHop>();
            forContext[fromPath] = hops;
        }
        hops.Add(hop);
    }

    private void BuildSceneNodesAndEdges()
    {
        if (settings.sceneGraph != null)
        {
            foreach (var sceneNode in settings.sceneGraph)
            {
                if (sceneNode.scene != null)
                {
                    GetOrCreateSceneNode(sceneNode.scene);
                }
            }
        }

        if (settings.baseScene != null)
        {
            BaseSceneNode = GetOrCreateSceneNode(settings.baseScene);
            BaseSceneNode.IsBaseScene = true;
        }

        if (settings.sceneGraphEdges == null)
        {
            return;
        }

        foreach (var edge in settings.sceneGraphEdges)
        {
            if (edge.sourceScene == null || edge.targetScene == null)
            {
                continue;
            }

            NodeData from = GetOrCreateSceneNode(edge.sourceScene);
            NodeData to = GetOrCreateSceneNode(edge.targetScene);
            string id = "scene:" + AssetDatabase.GetAssetPath(edge.sourceScene) + ">" +
                        AssetDatabase.GetAssetPath(edge.targetScene);
            EdgeData edgeData = GetOrCreateEdge(from, to, id);

            string originPath = AssetDatabase.GetAssetPath(edge.sourceScene);

            if (edge.references != null)
            {
                foreach (var reference in edge.references)
                {
                    if (reference.sourceAsset == null)
                    {
                        continue; // stale/missing data - nothing meaningful to attribute
                    }

                    string culpritPath = AssetDatabase.GetAssetPath(reference.sourceAsset);

                    if (culpritPath == originPath)
                    {
                        // Direct: a component in this very scene holds the SimpleScene field.
                        edgeData.Attributions.Add(new AttributionData
                        {
                            ComponentType = reference.componentType,
                            GameObjectPath = reference.gameObjectPath,
                            PropertyPath = reference.propertyPath,
                            ReferenceCount = reference.referenceCount
                        });
                        continue;
                    }

                    // Indirect: walk every distinct route from the scene to whatever asset
                    // actually holds the SimpleScene field. Two different GameObjects reaching
                    // the same target through the same intermediate asset are two different
                    // reasons, not one, so each route becomes its own attribution.
                    List<List<ChainHop>> paths = FindChains(originPath, originPath, culpritPath, MaxChainsPerReference);
                    foreach (List<ChainHop> path in paths)
                    {
                        if (path.Count == 0)
                        {
                            continue;
                        }
                        ChainHop firstHop = path[0];
                        AttributionData attribution = new AttributionData
                        {
                            ComponentType = firstHop.ViaComponent,
                            GameObjectPath = firstHop.ViaGameObjectPath,
                            PropertyPath = firstHop.ViaProperty,
                            ReferenceCount = 1
                        };
                        attribution.Chain.AddRange(path);
                        edgeData.Attributions.Add(attribution);
                    }
                }
            }

            if (edgeData.Attributions.Count == 0)
            {
                // Fallback: no per-reference detail could be resolved, still show one generic edge.
                edgeData.Attributions.Add(new AttributionData { ReferenceCount = edge.referenceCount });
            }
        }
    }

    private void BuildProviderNodesAndEdges()
    {
        if (settings.externalReferences == null || settings.externalReferences.Count == 0)
        {
            return;
        }

        HashSet<string> reachableFromBase = ComputeAssetPathsReachableFromBase();

        List<Object> rootCandidates = new List<Object>();
        if (settings.additionalGraphRoots != null)
        {
            rootCandidates.AddRange(settings.additionalGraphRoots.Where(r => r != null));
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject candidate = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (candidate is ISimpleSceneAutoGraphRoot autoRoot &&
                autoRoot.IncludeInSimpleSceneBuild &&
                !rootCandidates.Contains(candidate))
            {
                rootCandidates.Add(candidate);
            }
        }

        foreach (Object root in rootCandidates)
        {
            string rootPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(rootPath) || reachableFromBase.Contains(rootPath))
            {
                continue; // Traceable to base scene some other way - not an "orphan" provider.
            }

            HashSet<string> reachableFromRoot = new HashSet<string> { rootPath };
            BfsCollect(ExternalContext, rootPath, reachableFromRoot);

            List<SimpleSceneProjectSettings.SimpleSceneGraphExternalReference> relevant =
                settings.externalReferences
                    .Where(er => er.sourceAsset != null && er.targetScene != null &&
                                 reachableFromRoot.Contains(AssetDatabase.GetAssetPath(er.sourceAsset)))
                    .ToList();
            if (relevant.Count == 0)
            {
                continue; // This root doesn't lead anywhere - don't clutter the graph with it.
            }

            NodeData providerNode = GetOrCreateProviderNode(root);
            foreach (var externalReference in relevant)
            {
                NodeData targetNode = GetOrCreateSceneNode(externalReference.targetScene);
                string edgeId = "provider:" + rootPath + ">" +
                                 AssetDatabase.GetAssetPath(externalReference.targetScene);
                EdgeData edgeData = GetOrCreateEdge(providerNode, targetNode, edgeId);

                string culpritPath = AssetDatabase.GetAssetPath(externalReference.sourceAsset);

                if (culpritPath == rootPath)
                {
                    // The provider itself directly holds this Scene-set entry.
                    edgeData.Attributions.Add(new AttributionData { ReferenceCount = externalReference.referenceCount });
                    continue;
                }

                List<List<ChainHop>> paths = FindChains(ExternalContext, rootPath, culpritPath, MaxChainsPerReference);
                if (paths.Count == 0)
                {
                    // Reachability was already confirmed above, so this shouldn't normally
                    // happen - fall back to a single-hop chain rather than dropping the edge.
                    AttributionData fallback = new AttributionData { ReferenceCount = externalReference.referenceCount };
                    fallback.Chain.Add(new ChainHop
                    {
                        Asset = externalReference.sourceAsset,
                        ViaComponent = externalReference.sourceAsset.GetType().Name,
                        ViaProperty = externalReference.propertyPath
                    });
                    edgeData.Attributions.Add(fallback);
                    continue;
                }

                foreach (List<ChainHop> path in paths)
                {
                    AttributionData attribution = new AttributionData { ReferenceCount = 1 };
                    attribution.Chain.AddRange(path);
                    edgeData.Attributions.Add(attribution);
                }
            }
        }
    }

    private HashSet<string> ComputeAssetPathsReachableFromBase()
    {
        HashSet<string> reachablePaths = new HashSet<string>();
        if (BaseSceneNode == null)
        {
            return reachablePaths;
        }

        HashSet<NodeData> reachableScenes = new HashSet<NodeData> { BaseSceneNode };
        Queue<NodeData> sceneQueue = new Queue<NodeData>();
        sceneQueue.Enqueue(BaseSceneNode);
        while (sceneQueue.Count > 0)
        {
            NodeData current = sceneQueue.Dequeue();
            foreach (var edge in current.Outgoing)
            {
                if (!edge.To.IsProvider && reachableScenes.Add(edge.To))
                {
                    sceneQueue.Enqueue(edge.To);
                }
            }
        }

        foreach (NodeData sceneNode in reachableScenes)
        {
            string scenePath = AssetDatabase.GetAssetPath(sceneNode.SceneAsset);
            reachablePaths.Add(scenePath);
            BfsCollect(scenePath, scenePath, reachablePaths);
        }

        return reachablePaths;
    }

    private void BfsCollect(string context, string startPath, HashSet<string> into)
    {
        if (!adjacency.TryGetValue(context, out var forContext))
        {
            return;
        }

        Queue<string> queue = new Queue<string>();
        queue.Enqueue(startPath);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!forContext.TryGetValue(current, out var hops))
            {
                continue;
            }
            foreach (var hop in hops)
            {
                if (into.Add(hop.ToPath))
                {
                    queue.Enqueue(hop.ToPath);
                }
            }
        }
    }

    private NodeData GetOrCreateSceneNode(SceneAsset sceneAsset)
    {
        string path = AssetDatabase.GetAssetPath(sceneAsset);
        string id = "scene:" + path;
        if (nodesById.TryGetValue(id, out var existing))
        {
            return existing;
        }

        NodeData node = new NodeData
        {
            Id = id,
            SceneAsset = sceneAsset,
            DisplayName = sceneAsset.name,
            IsProvider = false
        };
        nodesById[id] = node;
        Nodes.Add(node);
        return node;
    }

    private NodeData GetOrCreateProviderNode(Object providerAsset)
    {
        string path = AssetDatabase.GetAssetPath(providerAsset);
        string id = "provider:" + path;
        if (nodesById.TryGetValue(id, out var existing))
        {
            return existing;
        }

        NodeData node = new NodeData
        {
            Id = id,
            ProviderAsset = providerAsset,
            DisplayName = providerAsset.name + "\n(" + providerAsset.GetType().Name + ")",
            IsProvider = true
        };
        nodesById[id] = node;
        Nodes.Add(node);
        return node;
    }

    private EdgeData GetOrCreateEdge(NodeData from, NodeData to, string id)
    {
        if (edgesById.TryGetValue(id, out var existing))
        {
            return existing;
        }

        EdgeData edgeData = new EdgeData { Id = id, From = from, To = to };
        edgesById[id] = edgeData;
        Edges.Add(edgeData);
        from.Outgoing.Add(edgeData);
        to.Incoming.Add(edgeData);
        return edgeData;
    }

    #endregion

    #region Chain reconstruction

    /// <summary>
    /// Every distinct simple path (no repeated asset) from fromPath to targetPath within the
    /// given context, capped at maxChains. Assumes fromPath != targetPath - callers handle the
    /// direct case (nothing to walk) separately.
    /// </summary>
    private List<List<ChainHop>> FindChains(string context, string fromPath, string targetPath, int maxChains)
    {
        List<List<ChainHop>> results = new List<List<ChainHop>>();
        if (fromPath == targetPath || !adjacency.TryGetValue(context, out var forContext))
        {
            return results;
        }

        HashSet<string> visited = new HashSet<string> { fromPath };
        List<ChainHop> currentPath = new List<ChainHop>();
        CollectPaths(forContext, fromPath, targetPath, visited, currentPath, results, maxChains);
        return results;
    }

    private void CollectPaths(
        Dictionary<string, List<LinkHop>> forContext,
        string current,
        string target,
        HashSet<string> visited,
        List<ChainHop> path,
        List<List<ChainHop>> results,
        int maxChains)
    {
        if (results.Count >= maxChains || !forContext.TryGetValue(current, out var hops))
        {
            return;
        }

        foreach (var hop in hops)
        {
            if (results.Count >= maxChains || visited.Contains(hop.ToPath))
            {
                continue;
            }

            path.Add(new ChainHop
            {
                Asset = hop.ToObject,
                ViaComponent = hop.ComponentType,
                ViaGameObjectPath = hop.GameObjectPath,
                ViaProperty = hop.PropertyPath
            });

            if (hop.ToPath == target)
            {
                results.Add(new List<ChainHop>(path));
            }
            else
            {
                visited.Add(hop.ToPath);
                CollectPaths(forContext, hop.ToPath, target, visited, path, results, maxChains);
                visited.Remove(hop.ToPath);
            }

            path.RemoveAt(path.Count - 1);
        }
    }

    #endregion
}
#endif
}