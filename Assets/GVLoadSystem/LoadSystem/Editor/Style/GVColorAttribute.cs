using System;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Assign a custom display color to a class or struct used as a CUSTOM GVEntry type.
    /// If omitted, a color is auto-generated from the type name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class GVColorAttribute : Attribute
    {
        public Color Color { get; }

        /// <param name="r">Red   0–1</param>
        /// <param name="g">Green 0–1</param>
        /// <param name="b">Blue  0–1</param>
        public GVColorAttribute(float r, float g, float b)
            => Color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
    }
}