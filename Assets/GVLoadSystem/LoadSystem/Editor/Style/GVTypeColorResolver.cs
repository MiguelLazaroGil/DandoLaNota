#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using GVLoadSystem.Core;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    internal static class GVTypeColorResolver
    {
        static readonly Dictionary<Type, Color> s_cache = new();

        public static Color GetColor(Type t)
        {
            if (s_cache.TryGetValue(t, out var cached)) return cached;

            Color col;
            var attr = t.GetCustomAttribute<GVColorAttribute>(inherit: false);
            if (attr != null)
            {
                col = attr.Color;
            }
            else
            {
                // Deterministic color from type full name hash.
                // Fixed S and V keep colors readable on dark backgrounds.
                int hash = t.FullName?.GetHashCode() ?? t.Name.GetHashCode();
                float hue = (float)((uint)hash % 360) / 360f;
                col = Color.HSVToRGB(hue, 0.55f, 0.80f);
            }

            s_cache[t] = col;
            return col;
        }

        /// <summary>Clears the cache (call if types are hot-reloaded in Editor).</summary>
        public static void ClearCache() => s_cache.Clear();
    }
}
#endif