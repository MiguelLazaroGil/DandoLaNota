#if UNITY_EDITOR
using MLG.SimpleSceneTool.GraphWindow;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal sealed class SimpleSceneGraphSidePanel : VisualElement
{
    private readonly ScrollView scrollView;

    private SimpleSceneGraphModel.EdgeData lastEdge;
    private IReadOnlyList<SimpleSceneGraphModel.AttributionData> lastAttributions;

    public SimpleSceneGraphSidePanel()
    {
        style.width = 320f;
        style.borderLeftWidth = 1f;
        style.borderLeftColor = new Color(0f, 0f, 0f, 0.4f);
        style.paddingLeft = 8f;
        style.paddingRight = 8f;
        style.paddingTop = 6f;

        scrollView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1f } };
        Add(scrollView);

        ShowNothingSelected();
    }

    public void ShowNothingSelected()
    {
        lastEdge = null;
        lastAttributions = null;

        scrollView.Clear();
        scrollView.Add(Header("Nothing selected"));
        scrollView.Add(Muted("Click a scene, a provider, or a reference line to see details here."));
    }

    public void ShowNode(SimpleSceneGraphModel.NodeData node)
    {
        lastEdge = null;
        lastAttributions = null;

        scrollView.Clear();

        scrollView.Add(Header(node.IsProvider ? "Provider" : (node.IsBaseScene ? "Base Scene" : "Scene")));
        scrollView.Add(AssetFieldRow("Asset", node.IsProvider ? node.ProviderAsset : (Object)node.SceneAsset));
        scrollView.Add(InfoRow("Name", node.DisplayName));

        int outgoing = node.Outgoing.Sum(e => e.Attributions.Count);
        int incoming = node.Incoming.Sum(e => e.Attributions.Count);
        scrollView.Add(InfoRow("Outgoing references", outgoing.ToString()));
        scrollView.Add(InfoRow("Incoming references", incoming.ToString()));

        scrollView.Add(SubHeader(node.IsProvider ? "Referenced via (outgoing)" : "Referencing components (outgoing)"));

        if (node.IsProvider)
        {
            ShowProviderReferencingObjects(node);
        }
        else
        {
            ShowSceneReferencingComponents(node);
        }
    }

    /// <summary>
    /// attributions normally has one entry (Default mode, or a Compressed edge that only ever
    /// had one reference anyway) and renders exactly as before. With more than one (a
    /// Compressed multi-reference edge), each gets its own "Caused by"/"Chain" block.
    /// </summary>
    public void ShowEdge(SimpleSceneGraphModel.EdgeData edge, IReadOnlyList<SimpleSceneGraphModel.AttributionData> attributions)
    {
        lastEdge = edge;
        lastAttributions = attributions;

        scrollView.Clear();

        scrollView.Add(Header(attributions.Count > 1 ? "References" : "Reference"));
        scrollView.Add(InfoRow("From", edge.From.DisplayName));
        scrollView.Add(InfoRow("To", edge.To.DisplayName));

        if (attributions.Count > 1)
        {
            scrollView.Add(Muted(attributions.Count + " separate references between these two, shown compressed into one line."));
        }

        for (int i = 0; i < attributions.Count; i++)
        {
            SimpleSceneGraphModel.AttributionData attribution = attributions[i];

            if (attributions.Count > 1)
            {
                scrollView.Add(Divider());
                scrollView.Add(SubHeader("Reference " + (i + 1) + " of " + attributions.Count));
            }

            if (!edge.From.IsProvider)
            {
                scrollView.Add(SubHeader("Caused by"));
                scrollView.Add(RouteRow(FormatRoute(attribution.GameObjectPath, attribution.ComponentType)));
                scrollView.Add(BuildCulpritResolutionRow(edge.From.SceneAsset, attribution.GameObjectPath, attribution.ComponentType));
            }

            scrollView.Add(SubHeader("Chain"));
            if (attribution.IsDirect)
            {
                scrollView.Add(Muted("Direct reference - no intermediate objects between " +
                                      edge.From.DisplayName + " and " + edge.To.DisplayName + "."));
            }
            else
            {
                foreach (SimpleSceneGraphModel.ChainHop hop in attribution.Chain)
                {
                    string label = string.IsNullOrEmpty(hop.ViaProperty) ? "Asset" : "via " + hop.ViaProperty;
                    scrollView.Add(AssetFieldRow(label, hop.Asset));
                }
            }
        }
    }

    private VisualElement BuildCulpritResolutionRow(SceneAsset sceneAsset, string gameObjectPath, string componentType)
    {
        SimpleSceneGraphSceneObjectResolver.ResolveResult result =
            SimpleSceneGraphSceneObjectResolver.Resolve(sceneAsset, gameObjectPath, componentType);

        switch (result.Status)
        {
            case SimpleSceneGraphSceneObjectResolver.ResolveStatus.Resolved:
                return AssetFieldRow("Component", result.Component);

            case SimpleSceneGraphSceneObjectResolver.ResolveStatus.SceneNotLoaded:
                return LoadAndSelectButton(sceneAsset, gameObjectPath, componentType);

            default:
                return Muted("Couldn't locate this in the scene - it may have moved since the graph was last refreshed. Try rebuilding the graph.");
        }
    }

    private VisualElement LoadAndSelectButton(SceneAsset sceneAsset, string gameObjectPath, string componentType)
    {
        Button button = new Button(() =>
        {
            SimpleSceneGraphSceneObjectResolver.ResolveResult result =
                SimpleSceneGraphSceneLoader.LoadAndResolve(sceneAsset, gameObjectPath, componentType);

            if (result.Status == SimpleSceneGraphSceneObjectResolver.ResolveStatus.Resolved)
            {
                EditorGUIUtility.PingObject(result.Component);
                Selection.activeObject = result.Component;
            }

            if (lastEdge != null && lastAttributions != null)
            {
                ShowEdge(lastEdge, lastAttributions); // rebuild - now reflects the loaded scene
            }
        })
        {
            text = "Load Scene & Select"
        };
        button.style.marginBottom = 2f;
        return button;
    }

    private void ShowSceneReferencingComponents(SimpleSceneGraphModel.NodeData node)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, string> labelByKey = new Dictionary<string, string>();
        List<string> order = new List<string>();

        foreach (SimpleSceneGraphModel.EdgeData edge in node.Outgoing)
        {
            foreach (SimpleSceneGraphModel.AttributionData attribution in edge.Attributions)
            {
                string key = (attribution.GameObjectPath ?? string.Empty) + "|" + (attribution.ComponentType ?? string.Empty);
                if (!counts.ContainsKey(key))
                {
                    counts[key] = 0;
                    labelByKey[key] = FormatRoute(attribution.GameObjectPath, attribution.ComponentType);
                    order.Add(key);
                }
                counts[key]++;
            }
        }

        if (order.Count == 0)
        {
            scrollView.Add(Muted("No outgoing references."));
            return;
        }

        foreach (string key in order.OrderByDescending(k => counts[k]))
        {
            int count = counts[key];
            scrollView.Add(RouteRow(labelByKey[key] + "  (" + count + " edge" + (count == 1 ? "" : "s") + ")"));
        }
    }

    private void ShowProviderReferencingObjects(SimpleSceneGraphModel.NodeData node)
    {
        Dictionary<Object, int> counts = new Dictionary<Object, int>();
        List<Object> order = new List<Object>();
        int directCount = 0;

        foreach (SimpleSceneGraphModel.EdgeData edge in node.Outgoing)
        {
            foreach (SimpleSceneGraphModel.AttributionData attribution in edge.Attributions)
            {
                if (attribution.Chain.Count == 0)
                {
                    directCount++;
                    continue;
                }
                Object asset = attribution.Chain[0].Asset;
                if (asset == null)
                {
                    continue;
                }
                if (!counts.ContainsKey(asset))
                {
                    counts[asset] = 0;
                    order.Add(asset);
                }
                counts[asset]++;
            }
        }

        if (order.Count == 0 && directCount == 0)
        {
            scrollView.Add(Muted("No outgoing references."));
            return;
        }

        foreach (Object asset in order.OrderByDescending(o => counts[o]))
        {
            int count = counts[asset];
            scrollView.Add(AssetFieldRow(count + " edge" + (count == 1 ? "" : "s"), asset));
        }
        if (directCount > 0)
        {
            scrollView.Add(Muted("Plus " + directCount + " direct scene-set entr" + (directCount == 1 ? "y" : "ies") + " on this provider itself."));
        }
    }

    #region Formatting helpers

    private static string FormatRoute(string gameObjectPath, string componentType)
    {
        string cleanedPath = CleanGameObjectPath(gameObjectPath);
        string component = string.IsNullOrEmpty(componentType) ? "(unknown component)" : ShortTypeName(componentType);
        return string.IsNullOrEmpty(cleanedPath) ? component : cleanedPath + "/" + component;
    }

    private static string CleanGameObjectPath(string rawPath)
    {
        if (string.IsNullOrEmpty(rawPath))
        {
            return string.Empty;
        }
        string[] segments = rawPath.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            int bracketIndex = segments[i].IndexOf('[');
            if (bracketIndex >= 0)
            {
                segments[i] = segments[i].Substring(0, bracketIndex);
            }
        }
        return string.Join("/", segments);
    }

    private static string ShortTypeName(string fullName)
    {
        int lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    #endregion

    #region UI helpers

    private static Label Header(string text)
    {
        return new Label(text)
        {
            style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4f, marginBottom = 4f }
        };
    }

    private static Label SubHeader(string text)
    {
        return new Label(text)
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10f, marginBottom = 2f }
        };
    }

    private static Label Muted(string text)
    {
        return new Label(text) { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.7f, marginBottom = 2f } };
    }

    private static VisualElement Divider()
    {
        VisualElement line = new VisualElement();
        line.style.height = 1f;
        line.style.marginTop = 8f;
        line.style.marginBottom = 4f;
        line.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        return line;
    }

    private static VisualElement InfoRow(string label, string value)
    {
        VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2f } };
        row.Add(new Label(label) { style = { width = 150f, opacity = 0.75f } });
        row.Add(new Label(value) { style = { flexGrow = 1f, whiteSpace = WhiteSpace.Normal } });
        return row;
    }

    /// <summary>
    /// A read-only object field for real project asset references. Left interactive (not
    /// SetEnabled(false)) so a single click still selects/pings the object, same as any normal
    /// reference field. "Read-only" is enforced by snapping any attempted value change straight
    /// back to the original target, rather than by disabling the whole control.
    /// </summary>
    private static VisualElement AssetFieldRow(string label, Object target)
    {
        ObjectField field = new ObjectField(label)
        {
            objectType = typeof(Object),
            value = target,
            style = { marginBottom = 2f }
        };
        field.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != target)
            {
                field.SetValueWithoutNotify(target);
            }
        });
        return field;
    }

    /// <summary>
    /// Plain descriptive text for an in-scene GameObject/component route. There is no live
    /// Object to bind here in general (the scene may not be open), so this is text, not a
    /// field - italicized to visually distinguish it from the real asset rows.
    /// </summary>
    private static VisualElement RouteRow(string text)
    {
        return new Label(text)
        {
            style = { whiteSpace = WhiteSpace.Normal, marginBottom = 2f, unityFontStyleAndWeight = FontStyle.Italic }
        };
    }

    #endregion
}
#endif