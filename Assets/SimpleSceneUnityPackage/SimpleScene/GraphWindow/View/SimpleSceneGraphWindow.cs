#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace MLG.SimpleSceneTool.GraphWindow
{
    internal enum SimpleSceneGraphViewMode
    {
        Default,
        Compressed,
        Simplified
    }

    public class SimpleSceneGraphWindow : EditorWindow
    {
        private SimpleSceneGraphView graphView;
        private SimpleSceneGraphSidePanel sidePanel;
        private SimpleSceneGraphModel currentModel;
        private SimpleSceneGraphViewMode viewMode = SimpleSceneGraphViewMode.Default;

        [MenuItem("Tools/SimpleScene/Scene Graph Viewer")]
        public static void Open()
        {
            SimpleSceneGraphWindow window = GetWindow<SimpleSceneGraphWindow>();
            window.titleContent = new GUIContent("Scene Graph");
            window.minSize = new Vector2(760f, 420f);
        }

        private void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(BuildToolbar());

            VisualElement body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };
            rootVisualElement.Add(body);

            graphView = new SimpleSceneGraphView
            {
                name = "SimpleSceneGraphView",
                style = { flexGrow = 1f }
            };
            graphView.SelectionChanged += OnSelectionChanged;
            body.Add(graphView);

            sidePanel = new SimpleSceneGraphSidePanel();
            body.Add(sidePanel);

            Reload();
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    borderBottomWidth = 1f,
                    borderBottomColor = new Color(0f, 0f, 0f, 0.4f)
                }
            };

            EnumField modeField = new EnumField("View", viewMode) { style = { width = 220f } };
            modeField.RegisterValueChangedCallback(evt =>
            {
                viewMode = (SimpleSceneGraphViewMode)evt.newValue;
                Reload();
            });
            toolbar.Add(modeField);

            return toolbar;
        }

        private void Reload()
        {
            currentModel = SimpleSceneGraphModel.Build(SimpleSceneProjectSettings.instance);
            if (currentModel.Nodes.Count == 0)
            {
                Debug.LogWarning("SimpleScene graph is empty. Run \"Rebuild Build Settings and graph\" in Project Settings/SimpleScene first.");
            }
            graphView.PopulateFromModel(currentModel, viewMode);
            sidePanel.ShowNothingSelected();
        }

        private void OnSelectionChanged()
        {
            if (currentModel == null || sidePanel == null)
            {
                return;
            }

            List<ISelectable> selection = graphView.selection;
            if (selection.Count == 1 && selection[0] is SimpleSceneGraphNodeView nodeView)
            {
                sidePanel.ShowNode(nodeView.Data);
            }
            else if (selection.Count == 1 && selection[0] is SimpleSceneGraphEdgeView edgeView)
            {
                sidePanel.ShowEdge(edgeView.EdgeData, edgeView.Attributions);
            }
            else
            {
                sidePanel.ShowNothingSelected();
            }
        }

        private void OnDisable()
        {
            if (graphView != null)
            {
                graphView.SelectionChanged -= OnSelectionChanged;
                graphView = null;
            }
            sidePanel = null;
        }
    }

    internal class SimpleSceneGraphView : GraphView
    {
        public event System.Action SelectionChanged;

        public SimpleSceneGraphView()
        {
            SetupZoom(0.15f, 2.5f);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new GridBackground();
            grid.StretchToParentSize();
            Insert(0, grid);

            graphViewChanged = OnGraphViewChanged;
        }

        internal void NotifySelectionChanged()
        {
            SelectionChanged?.Invoke();
        }

        public void PopulateFromModel(SimpleSceneGraphModel model, SimpleSceneGraphViewMode mode)
        {
            ClearGraph();

            int maxDegree = 0;
            foreach (SimpleSceneGraphModel.NodeData nodeData in model.Nodes)
            {
                int degree = nodeData.Outgoing.Sum(e => e.Attributions.Count) + nodeData.Incoming.Sum(e => e.Attributions.Count);
                maxDegree = Mathf.Max(maxDegree, degree);
            }

            Dictionary<string, SimpleSceneGraphNodeView> nodeViews = new Dictionary<string, SimpleSceneGraphNodeView>();
            foreach (SimpleSceneGraphModel.NodeData nodeData in model.Nodes)
            {
                if (mode == SimpleSceneGraphViewMode.Simplified && nodeData.IsProvider)
                {
                    continue;
                }
                SimpleSceneGraphNodeView nodeView = new SimpleSceneGraphNodeView(nodeData);
                AddElement(nodeView);
                nodeViews[nodeData.Id] = nodeView;
            }

            ApplyLayout(model, nodeViews, mode);
            AddEdges(model, nodeViews, mode);

            foreach (SimpleSceneGraphNodeView nodeView in nodeViews.Values)
            {
                nodeView.FinalizePorts();
            }

            schedule.Execute(() => FrameAll()).ExecuteLater(1);
        }

        private void AddEdges(SimpleSceneGraphModel model, Dictionary<string, SimpleSceneGraphNodeView> nodeViews, SimpleSceneGraphViewMode mode)
        {
            foreach (SimpleSceneGraphModel.EdgeData edgeData in model.Edges)
            {
                if (mode == SimpleSceneGraphViewMode.Simplified && edgeData.From.IsProvider)
                {
                    continue; // the provider itself isn't rendered in Simplified mode either
                }
                if (!nodeViews.TryGetValue(edgeData.From.Id, out SimpleSceneGraphNodeView fromView) ||
                    !nodeViews.TryGetValue(edgeData.To.Id, out SimpleSceneGraphNodeView toView))
                {
                    continue; // one endpoint isn't rendered in this mode
                }
                if (mode == SimpleSceneGraphViewMode.Default)
                {
                    foreach (SimpleSceneGraphModel.AttributionData attribution in edgeData.Attributions)
                    {
                        Port outputPort = fromView.AddOutputPort("\u2192 " + edgeData.To.DisplayName);
                        Port inputPort = toView.AddInputPort("\u2190 " + edgeData.From.DisplayName);
                        CreateEdgeView(edgeData, new[] { attribution }, outputPort, inputPort);
                    }
                }
                else
                {
                    string countSuffix = edgeData.Attributions.Count > 1
                        ? "  (\u00d7" + edgeData.Attributions.Count + ")"
                        : string.Empty;
                    Port outputPort = fromView.AddOutputPort("\u2192 " + edgeData.To.DisplayName + countSuffix);
                    Port inputPort = toView.AddInputPort("\u2190 " + edgeData.From.DisplayName + countSuffix);
                    CreateEdgeView(edgeData, edgeData.Attributions, outputPort, inputPort);
                }
            }
        }

        private void CreateEdgeView(
            SimpleSceneGraphModel.EdgeData edgeData,
            IReadOnlyList<SimpleSceneGraphModel.AttributionData> attributions,
            Port outputPort,
            Port inputPort)
        {
            SimpleSceneGraphEdgeView edgeView = new SimpleSceneGraphEdgeView(edgeData, attributions)
            {
                output = outputPort,
                input = inputPort
            };
            outputPort.Connect(edgeView);
            inputPort.Connect(edgeView);
            AddElement(edgeView);
        }

        private void ApplyLayout(SimpleSceneGraphModel model, Dictionary<string, SimpleSceneGraphNodeView> nodeViews, SimpleSceneGraphViewMode mode)
        {
            Dictionary<string, Vector2> positions = SimpleSceneGraphLayout.Compute(model);
            foreach (KeyValuePair<string, Vector2> kvp in positions)
            {
                if (nodeViews.TryGetValue(kvp.Key, out SimpleSceneGraphNodeView nodeView))
                {
                    nodeView.SetPosition(new Rect(kvp.Value, Vector2.zero));
                }
            }
        }

        private void ClearGraph()
        {
            foreach (Edge edge in edges.ToList())
            {
                RemoveElement(edge);
            }
            foreach (Node node in nodes.ToList())
            {
                RemoveElement(node);
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            change.elementsToRemove?.Clear();
            return change;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Intentionally left empty - no "Create Node", "Cut", "Duplicate", etc.
        }
    }
}
#endif