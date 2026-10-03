#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace MLG.SimpleSceneTool.GraphWindow
{
    internal sealed class SimpleSceneGraphEdgeView : Edge
    {
        public readonly SimpleSceneGraphModel.EdgeData EdgeData;

        /// <summary>
        /// In Default mode this always has exactly one entry. In Compressed mode it holds every
        /// attribution this single line represents.
        /// </summary>
        public readonly IReadOnlyList<SimpleSceneGraphModel.AttributionData> Attributions;

        private readonly Color baseColor;

        public SimpleSceneGraphEdgeView(SimpleSceneGraphModel.EdgeData edgeData, IReadOnlyList<SimpleSceneGraphModel.AttributionData> attributions)
        {
            EdgeData = edgeData;
            Attributions = attributions;

            capabilities &= ~Capabilities.Deletable;

            baseColor = edgeData.From.IsProvider ? SimpleSceneGraphColors.ProviderEdge : SimpleSceneGraphColors.SceneEdge;
            edgeControl.inputColor = baseColor;
            edgeControl.outputColor = baseColor;

            edgeControl.generateVisualContent += DrawArrowHead;
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

        private void DrawArrowHead(MeshGenerationContext mgc)
        {
            Vector2[] points = edgeControl.controlPoints;
            if (points == null || points.Length < 2)
            {
                return;
            }

            Vector2 end = this.ChangeCoordinatesTo(edgeControl, points[points.Length - 1]);
            Vector2 beforeEnd = this.ChangeCoordinatesTo(edgeControl, points[points.Length - 2]);

            Vector2 direction = end - beforeEnd;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            const float pullBack = 6f;
            const float arrowLength = 12f;
            const float arrowHalfWidth = 6f;

            Vector2 tip = end - direction * pullBack;
            Vector2 baseCenter = tip - direction * arrowLength;
            Vector2 baseLeft = baseCenter + perpendicular * arrowHalfWidth;
            Vector2 baseRight = baseCenter - perpendicular * arrowHalfWidth;

            Painter2D painter = mgc.painter2D;
            painter.fillColor = edgeControl.outputColor;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(baseLeft);
            painter.LineTo(baseRight);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
#endif