#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MLG.SimpleSceneTool.GraphWindow
{
    using NodeData = SimpleSceneGraphModel.NodeData;
    using EdgeData = SimpleSceneGraphModel.EdgeData;

    /// <summary>
    /// Auto-layout for the scene graph. The graph is first split into connected components
    /// (undirected reachability over every edge, scene-to-scene and provider-to-scene alike) -
    /// this tool has no guarantee everything is reachable from one root, so a single global BFS
    /// from the base scene isn't enough on its own. Each component is then laid out independently
    /// as a Sugiyama-style layered drawing: DFS-based cycle breaking so every node gets a layer
    /// even inside a reference cycle, barycenter crossing reduction between adjacent layers, then
    /// height-aware coordinate assignment. Components are stacked as separate vertical bands so
    /// unrelated clusters can never visually overlap.
    ///
    /// Multi-reference edges never carry extra weight anywhere in here - every calculation works
    /// off DISTINCT neighboring nodes, so five references between A and B pull exactly as hard as
    /// one would.
    /// </summary>
    internal static class SimpleSceneGraphLayout
    {
        private const float ColumnWidth = 450f;
        private const float NodeGap = 28f;
        private const float ComponentBandGap = 80f;
        private const float TitleBarHeight = 34f;
        private const float PortRowHeight = 24f;
        private const float MinNodeHeight = 200f;
        private const int CrossingReductionSweeps = 8;

        public static Dictionary<string, Vector2> Compute(SimpleSceneGraphModel model)
        {
            Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
            if (model.Nodes.Count == 0)
            {
                return positions;
            }

            // Self-loops don't affect relative positioning of two different nodes.
            List<EdgeData> realEdges = model.Edges.Where(e => e.From != e.To).ToList();

            Dictionary<NodeData, List<NodeData>> undirected = BuildUndirectedAdjacency(realEdges);
            Dictionary<NodeData, List<NodeData>> distinctSuccessors = BuildDistinctAdjacency(realEdges, successors: true);
            Dictionary<NodeData, List<NodeData>> distinctPredecessors = BuildDistinctAdjacency(realEdges, successors: false);

            List<List<NodeData>> components = FindConnectedComponents(model.Nodes, undirected);
            components = OrderComponents(components, model.BaseSceneNode);

            float bandTopY = 0f;
            foreach (List<NodeData> component in components)
            {
                HashSet<NodeData> componentSet = new HashSet<NodeData>(component);

                List<NodeData> preferredStarts = ComputePreferredStarts(component, componentSet, distinctPredecessors, model.BaseSceneNode);
                (HashSet<(NodeData From, NodeData To)> backEdges, List<NodeData> topoOrder) =
                    AnalyzeComponent(component, componentSet, preferredStarts, distinctSuccessors);

                Dictionary<NodeData, int> layerByNode = AssignLayersForComponent(topoOrder, backEdges, distinctPredecessors, componentSet);
                List<List<NodeData>> layers = GroupByLayer(component, layerByNode);

                ReduceCrossings(layers, distinctSuccessors, distinctPredecessors);

                float bandHeight = AssignCoordinatesForComponent(layers, positions, bandTopY);
                bandTopY += bandHeight + ComponentBandGap;
            }

            return positions;
        }

        #region Connected components

        private static Dictionary<NodeData, List<NodeData>> BuildUndirectedAdjacency(List<EdgeData> edges)
        {
            Dictionary<NodeData, HashSet<NodeData>> sets = new Dictionary<NodeData, HashSet<NodeData>>();
            foreach (EdgeData edge in edges)
            {
                AddUndirectedPair(sets, edge.From, edge.To);
                AddUndirectedPair(sets, edge.To, edge.From);
            }
            return sets.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        }

        private static void AddUndirectedPair(Dictionary<NodeData, HashSet<NodeData>> sets, NodeData a, NodeData b)
        {
            if (!sets.TryGetValue(a, out HashSet<NodeData> set))
            {
                set = new HashSet<NodeData>();
                sets[a] = set;
            }
            set.Add(b);
        }

        private static List<List<NodeData>> FindConnectedComponents(List<NodeData> allNodes, Dictionary<NodeData, List<NodeData>> undirected)
        {
            HashSet<NodeData> visited = new HashSet<NodeData>();
            List<List<NodeData>> components = new List<List<NodeData>>();

            foreach (NodeData start in allNodes)
            {
                if (!visited.Add(start))
                {
                    continue;
                }

                List<NodeData> component = new List<NodeData> { start };
                Queue<NodeData> queue = new Queue<NodeData>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    NodeData current = queue.Dequeue();
                    if (!undirected.TryGetValue(current, out List<NodeData> neighbors))
                    {
                        continue;
                    }
                    foreach (NodeData neighbor in neighbors)
                    {
                        if (visited.Add(neighbor))
                        {
                            component.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static List<List<NodeData>> OrderComponents(List<List<NodeData>> components, NodeData baseSceneNode)
        {
            List<NodeData> baseComponent = null;
            List<List<NodeData>> rest = new List<List<NodeData>>();

            foreach (List<NodeData> component in components)
            {
                if (baseComponent == null && baseSceneNode != null && component.Contains(baseSceneNode))
                {
                    baseComponent = component;
                }
                else
                {
                    rest.Add(component);
                }
            }

            rest.Sort((a, b) => b.Count.CompareTo(a.Count)); // larger clusters first

            List<List<NodeData>> ordered = new List<List<NodeData>>();
            if (baseComponent != null)
            {
                ordered.Add(baseComponent);
            }
            ordered.AddRange(rest);
            return ordered;
        }

        #endregion

        #region Per-component root discovery and cycle breaking

        private static List<NodeData> ComputePreferredStarts(
            List<NodeData> component,
            HashSet<NodeData> componentSet,
            Dictionary<NodeData, List<NodeData>> distinctPredecessors,
            NodeData baseSceneNode)
        {
            List<NodeData> starts = new List<NodeData>();

            if (baseSceneNode != null && componentSet.Contains(baseSceneNode))
            {
                starts.Add(baseSceneNode);
            }

            foreach (NodeData node in component)
            {
                if (node == baseSceneNode)
                {
                    continue;
                }
                bool hasIncoming = distinctPredecessors.TryGetValue(node, out List<NodeData> preds) && preds.Count > 0;
                if (!hasIncoming)
                {
                    starts.Add(node); // includes every provider - they never receive edges
                }
            }

            return starts;
        }

        /// <summary>
        /// DFS from the preferred starts, marking back-edges (edges to a node currently on the DFS
        /// stack - the standard signal of a cycle) so layering can ignore them, and recording a
        /// post-order traversal along the way. Any node the preferred starts don't reach - an
        /// isolated cycle with no external entry point - gets a fresh DFS tree of its own, so every
        /// node in the component ends up covered. The post-order, reversed, is a valid topological
        /// order for the resulting (back-edge-free) DAG, which longest-path layering below needs.
        /// </summary>
        private static (HashSet<(NodeData From, NodeData To)> BackEdges, List<NodeData> TopoOrder) AnalyzeComponent(
            List<NodeData> component,
            HashSet<NodeData> componentSet,
            List<NodeData> preferredStarts,
            Dictionary<NodeData, List<NodeData>> distinctSuccessors)
        {
            HashSet<(NodeData, NodeData)> backEdges = new HashSet<(NodeData, NodeData)>();
            HashSet<NodeData> visited = new HashSet<NodeData>();
            HashSet<NodeData> onStack = new HashSet<NodeData>();
            List<NodeData> postOrder = new List<NodeData>();

            void Visit(NodeData node)
            {
                visited.Add(node);
                onStack.Add(node);

                if (distinctSuccessors.TryGetValue(node, out List<NodeData> successors))
                {
                    foreach (NodeData next in successors)
                    {
                        if (!componentSet.Contains(next))
                        {
                            continue;
                        }
                        if (onStack.Contains(next))
                        {
                            backEdges.Add((node, next));
                        }
                        else if (!visited.Contains(next))
                        {
                            Visit(next);
                        }
                    }
                }

                onStack.Remove(node);
                postOrder.Add(node);
            }

            foreach (NodeData start in preferredStarts)
            {
                if (!visited.Contains(start))
                {
                    Visit(start);
                }
            }

            foreach (NodeData node in component)
            {
                if (!visited.Contains(node))
                {
                    Visit(node); // an isolated cycle - nothing outside it reaches in
                }
            }

            postOrder.Reverse();
            return (backEdges, postOrder);
        }

        /// <summary>
        /// Longest-path layering: a node's layer is one past its FARTHEST predecessor, not its
        /// nearest. This guarantees every forward edge strictly increases in column, which is what
        /// stops two connected nodes from ever landing in the same column just because one happens
        /// to also be reachable via a shorter route.
        /// </summary>
        private static Dictionary<NodeData, int> AssignLayersForComponent(
            List<NodeData> topoOrder,
            HashSet<(NodeData From, NodeData To)> backEdges,
            Dictionary<NodeData, List<NodeData>> distinctPredecessors,
            HashSet<NodeData> componentSet)
        {
            Dictionary<NodeData, int> layerByNode = new Dictionary<NodeData, int>();

            foreach (NodeData node in topoOrder)
            {
                int layer = 0;
                if (distinctPredecessors.TryGetValue(node, out List<NodeData> preds))
                {
                    foreach (NodeData pred in preds)
                    {
                        if (!componentSet.Contains(pred) || backEdges.Contains((pred, node)))
                        {
                            continue;
                        }
                        if (layerByNode.TryGetValue(pred, out int predLayer))
                        {
                            layer = Mathf.Max(layer, predLayer + 1);
                        }
                    }
                }
                layerByNode[node] = layer;
            }

            return layerByNode;
        }

        private static List<List<NodeData>> GroupByLayer(List<NodeData> component, Dictionary<NodeData, int> layerByNode)
        {
            int layerCount = layerByNode.Count > 0 ? layerByNode.Values.Max() + 1 : 1;
            List<List<NodeData>> layers = new List<List<NodeData>>();
            for (int i = 0; i < layerCount; i++)
            {
                layers.Add(new List<NodeData>());
            }
            foreach (NodeData node in component)
            {
                layers[layerByNode[node]].Add(node);
            }
            return layers;
        }

        #endregion

        #region Crossing reduction (barycenter heuristic)

        /// <summary>
        /// Builds a per-node list of DISTINCT neighboring nodes. This is what keeps multi-reference
        /// edges from skewing the layout: they influence *which* nodes are neighbors, never *how
        /// strongly* - a node referenced five times over five different GameObjects still counts
        /// as exactly one neighbor here.
        /// </summary>
        private static Dictionary<NodeData, List<NodeData>> BuildDistinctAdjacency(List<EdgeData> edges, bool successors)
        {
            Dictionary<NodeData, HashSet<NodeData>> sets = new Dictionary<NodeData, HashSet<NodeData>>();

            foreach (EdgeData edge in edges)
            {
                NodeData key = successors ? edge.From : edge.To;
                NodeData value = successors ? edge.To : edge.From;
                if (!sets.TryGetValue(key, out HashSet<NodeData> set))
                {
                    set = new HashSet<NodeData>();
                    sets[key] = set;
                }
                set.Add(value);
            }

            return sets.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        }

        private static void ReduceCrossings(
            List<List<NodeData>> layers,
            Dictionary<NodeData, List<NodeData>> distinctSuccessors,
            Dictionary<NodeData, List<NodeData>> distinctPredecessors)
        {
            if (layers.Count < 2)
            {
                return;
            }

            List<List<NodeData>> best = CloneLayers(layers);
            int bestCrossings = CountTotalCrossings(layers, distinctSuccessors);

            for (int sweep = 0; sweep < CrossingReductionSweeps; sweep++)
            {
                bool forward = sweep % 2 == 0;
                Dictionary<NodeData, int> rowIndex = BuildRowIndex(layers);

                if (forward)
                {
                    for (int layer = 1; layer < layers.Count; layer++)
                    {
                        SortLayerByBarycenter(layers[layer], distinctPredecessors, rowIndex);
                        UpdateRowIndex(layers[layer], rowIndex);
                    }
                }
                else
                {
                    for (int layer = layers.Count - 2; layer >= 0; layer--)
                    {
                        SortLayerByBarycenter(layers[layer], distinctSuccessors, rowIndex);
                        UpdateRowIndex(layers[layer], rowIndex);
                    }
                }

                int crossings = CountTotalCrossings(layers, distinctSuccessors);
                if (crossings < bestCrossings)
                {
                    bestCrossings = crossings;
                    best = CloneLayers(layers);
                }
            }

            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].Clear();
                layers[i].AddRange(best[i]);
            }
        }

        private static void SortLayerByBarycenter(
            List<NodeData> layer,
            Dictionary<NodeData, List<NodeData>> neighborsLookup,
            Dictionary<NodeData, int> rowIndex)
        {
            Dictionary<NodeData, float> barycenter = new Dictionary<NodeData, float>();
            foreach (NodeData node in layer)
            {
                float fallback = rowIndex.TryGetValue(node, out int currentRow) ? currentRow : 0f;
                if (!neighborsLookup.TryGetValue(node, out List<NodeData> neighbors) || neighbors.Count == 0)
                {
                    barycenter[node] = fallback;
                    continue;
                }

                float sum = 0f;
                int count = 0;
                foreach (NodeData neighbor in neighbors)
                {
                    if (rowIndex.TryGetValue(neighbor, out int r))
                    {
                        sum += r;
                        count++;
                    }
                }
                barycenter[node] = count > 0 ? sum / count : fallback;
            }

            layer.Sort((a, b) => barycenter[a].CompareTo(barycenter[b]));
        }

        private static int CountTotalCrossings(List<List<NodeData>> layers, Dictionary<NodeData, List<NodeData>> distinctSuccessors)
        {
            int total = 0;
            for (int i = 0; i < layers.Count - 1; i++)
            {
                total += CountCrossingsBetweenLayers(layers[i], layers[i + 1], distinctSuccessors);
            }
            return total;
        }

        private static int CountCrossingsBetweenLayers(
            List<NodeData> upperLayer,
            List<NodeData> lowerLayer,
            Dictionary<NodeData, List<NodeData>> distinctSuccessors)
        {
            Dictionary<NodeData, int> lowerPos = new Dictionary<NodeData, int>();
            for (int i = 0; i < lowerLayer.Count; i++)
            {
                lowerPos[lowerLayer[i]] = i;
            }

            List<int> lowerPositionsInUpperOrder = new List<int>();
            foreach (NodeData upperNode in upperLayer)
            {
                if (!distinctSuccessors.TryGetValue(upperNode, out List<NodeData> succs))
                {
                    continue;
                }
                foreach (NodeData lowerNode in succs)
                {
                    if (lowerPos.TryGetValue(lowerNode, out int p))
                    {
                        lowerPositionsInUpperOrder.Add(p);
                    }
                }
            }

            int crossings = 0;
            for (int i = 0; i < lowerPositionsInUpperOrder.Count; i++)
            {
                for (int j = i + 1; j < lowerPositionsInUpperOrder.Count; j++)
                {
                    if (lowerPositionsInUpperOrder[j] < lowerPositionsInUpperOrder[i])
                    {
                        crossings++;
                    }
                }
            }
            return crossings;
        }

        private static Dictionary<NodeData, int> BuildRowIndex(List<List<NodeData>> layers)
        {
            Dictionary<NodeData, int> rowIndex = new Dictionary<NodeData, int>();
            foreach (List<NodeData> layer in layers)
            {
                UpdateRowIndex(layer, rowIndex);
            }
            return rowIndex;
        }

        private static void UpdateRowIndex(List<NodeData> layer, Dictionary<NodeData, int> rowIndex)
        {
            for (int i = 0; i < layer.Count; i++)
            {
                rowIndex[layer[i]] = i;
            }
        }

        private static List<List<NodeData>> CloneLayers(List<List<NodeData>> layers)
        {
            List<List<NodeData>> clone = new List<List<NodeData>>(layers.Count);
            foreach (List<NodeData> layer in layers)
            {
                clone.Add(new List<NodeData>(layer));
            }
            return clone;
        }

        #endregion

        #region Coordinate assignment

        private static float AssignCoordinatesForComponent(List<List<NodeData>> layers, Dictionary<string, Vector2> positions, float bandTopY)
        {
            float maxBandHeight = 0f;
            for (int column = 0; column < layers.Count; column++)
            {
                float y = bandTopY;
                foreach (NodeData node in layers[column])
                {
                    positions[node.Id] = new Vector2(column * ColumnWidth, y);
                    y += EstimateNodeHeight(node) + NodeGap;
                }
                maxBandHeight = Mathf.Max(maxBandHeight, y - bandTopY);
            }
            return maxBandHeight;
        }

        private static float EstimateNodeHeight(NodeData node)
        {
            int outCount = node.Outgoing.Sum(e => e.Attributions.Count);
            int inCount = node.Incoming.Sum(e => e.Attributions.Count);
            int maxPorts = Mathf.Max(1, Mathf.Max(inCount, outCount));
            return Mathf.Max(MinNodeHeight, TitleBarHeight + maxPorts * PortRowHeight);
        }

        #endregion
    }
#endif
}