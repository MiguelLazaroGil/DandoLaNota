#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
namespace MLG.SimpleSceneTool.GraphWindow
{
    /// <summary>
    /// Marker type used only to satisfy Node.InstantiatePort's generic "type" parameter. This
    /// tool never performs typed connections, so the concrete type carries no real meaning.
    /// </summary>
    internal sealed class SimpleSceneGraphPortType
{
}

internal static class SimpleSceneGraphColors
{
    public static readonly Color BaseScene = new Color(0.30f, 0.62f, 0.32f);
    public static readonly Color Scene = new Color(0.28f, 0.42f, 0.62f);
    public static readonly Color Provider = new Color(0.78f, 0.48f, 0.16f);

    public static readonly Color SceneEdge = new Color(0.55f, 0.65f, 0.85f);
    public static readonly Color ProviderEdge = new Color(0.85f, 0.60f, 0.30f);
    public static readonly Color SelectedEdge = new Color(1f, 0.85f, 0.15f);
}

/// <summary>
/// Visual node for either a scene or a "provider" (a graph root that couldn't be traced back
/// to the base scene). Purely presentational - all graph logic lives in SimpleSceneGraphModel.
/// Ports are added on demand, one per attribution, by SimpleSceneGraphView while it builds
/// edges - call FinalizePorts() once all of a node's ports have been added.
/// </summary>
internal sealed class SimpleSceneGraphNodeView : Node
{
    public readonly SimpleSceneGraphModel.NodeData Data;

    public SimpleSceneGraphNodeView(SimpleSceneGraphModel.NodeData data)
    {
        Data = data;
        viewDataKey = data.Id;

        string prefix = data.IsProvider ? "\u25B2 " : (data.IsBaseScene ? "\u25C6 " : string.Empty);
        title = prefix + data.DisplayName;

        capabilities &= ~Capabilities.Deletable;
        capabilities &= ~Capabilities.Renamable;

        ApplyColor(data);
        style.minWidth = 170f;
    }

    /// <summary>Adds and returns one new output port - call once per outgoing attribution.</summary>
    public Port AddOutputPort(string label)
    {
        Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(SimpleSceneGraphPortType));
        port.portName = label;
        outputContainer.Add(port);
        return port;
    }

    /// <summary>Adds and returns one new input port - call once per incoming attribution.</summary>
    public Port AddInputPort(string label)
    {
        Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(SimpleSceneGraphPortType));
        port.portName = label;
        inputContainer.Add(port);
        return port;
    }

    /// <summary>Call once after every port this node will ever have has been added.</summary>
    public void FinalizePorts()
    {
        RefreshExpandedState();
        RefreshPorts();
    }

    private void ApplyColor(SimpleSceneGraphModel.NodeData data)
    {
        Color accent = data.IsProvider
            ? SimpleSceneGraphColors.Provider
            : (data.IsBaseScene ? SimpleSceneGraphColors.BaseScene : SimpleSceneGraphColors.Scene);

        titleContainer.style.backgroundColor = new StyleColor(accent);

        // Best-effort body tint - the title bar color above is the guaranteed-visible signal.
        Color bodyTint = new Color(accent.r * 0.30f, accent.g * 0.30f, accent.b * 0.30f, 1f);
        mainContainer.style.backgroundColor = new StyleColor(bodyTint);
    }
    public override void OnSelected()
    {
        base.OnSelected();
        GetFirstAncestorOfType<SimpleSceneGraphView>()?.NotifySelectionChanged();
    }

    public override void OnUnselected()
    {
        base.OnUnselected();
        GetFirstAncestorOfType<SimpleSceneGraphView>()?.NotifySelectionChanged();
    }
}
#endif
}