using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GVLoadSystem.Core
{

    /// <summary>
    /// Marks a serializable class as a custom type usable in GroupValues.
    /// Color is optional — if omitted a unique color is auto-assigned from a fixed palette.
    /// Usage:  [CustomSettingData("MyType")]
    ///         [CustomSettingData("MyType", 0.4f, 0.8f, 0.3f)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomGVDataAttribute : Attribute
    {
        public string TypeName { get; }
        public Color Color { get; }
        public bool HasColor { get; }

        public CustomGVDataAttribute(string typeName)
        {
            TypeName = typeName;
            HasColor = false;
            Color = Color.gray;
        }

        /// <param name="typeName">Registry key shown in the picker.</param>
        /// <param name="r">Red   0-1</param>
        /// <param name="g">Green 0-1</param>
        /// <param name="b">Blue  0-1</param>
        public CustomGVDataAttribute(string typeName, float r, float g, float b)
        {
            TypeName = typeName;
            Color = new Color(r, g, b);
            HasColor = true;
        }
    }

    /// <summary>
    /// Discovers at runtime all classes marked with [CustomSettingData] via reflection.
    /// </summary>
    public static class CustomGVDataRegistry
    {
        public struct Entry
        {
            public Type Type;
            public string Name;
            public Color Color;
        }

        static Dictionary<string, Entry> s_entries;

        public static IReadOnlyDictionary<string, Entry> Entries
        {
            get { s_entries ??= Build(); return s_entries; }
        }

        // Keep backward-compat property used by existing code
        public static IReadOnlyDictionary<string, Type> Types
        {
            get
            {
                s_entries ??= Build();
                var result = new Dictionary<string, Type>();
                foreach (var kv in s_entries) result[kv.Key] = kv.Value.Type;
                return result;
            }
        }

        public static void Rebuild() => s_entries = Build();

        /// <summary>
        /// Registers a type at runtime (used by GroupValuesWrapperDiscovery).
        /// If already registered via [CustomSettingData] attribute, this is a no-op.
        /// </summary>
        public static void RegisterType(Type type)
        {
            s_entries ??= Build();

            string key = type.Name;
            if (s_entries.ContainsKey(key)) return; // already registered via attribute

            // Auto-assign a color from the palette
            int autoIdx = s_entries.Count % AutoPalette.Length;
            var color = AutoPalette[autoIdx];

            s_entries[key] = new Entry
            {
                Type = type,
                Name = key,
                Color = color,
            };
        }

        // Palette used for auto-color when the attribute has no explicit color.
        static readonly Color[] AutoPalette =
        {
        new Color(0.30f, 0.70f, 0.90f),
        new Color(0.85f, 0.50f, 0.20f),
        new Color(0.55f, 0.85f, 0.35f),
        new Color(0.80f, 0.30f, 0.70f),
        new Color(0.95f, 0.85f, 0.20f),
        new Color(0.40f, 0.60f, 0.95f),
        new Color(0.90f, 0.40f, 0.40f),
        new Color(0.35f, 0.90f, 0.75f),
    };

        static Dictionary<string, Entry> Build()
        {
            var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var usedColors = new HashSet<int>(); // packed RGB ints to detect duplicates
            int autoIdx = 0;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("System") ||
                    assembly.FullName.StartsWith("Unity") ||
                    assembly.FullName.StartsWith("UnityEngine") ||
                    assembly.FullName.StartsWith("mscorlib"))
                    continue;

                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<CustomGVDataAttribute>();
                    if (attr == null) continue;

                    if (result.ContainsKey(attr.TypeName))
                    {
                        Debug.LogWarning(
                            $"[CustomGVDataRegistry] Duplicate name '{attr.TypeName}' " +
                            $"in {type.FullName}. Ignored.");
                        continue;
                    }

                    Color col;
                    if (attr.HasColor)
                    {
                        col = attr.Color;
                    }
                    else
                    {
                        // Pick next palette color not already used by another entry
                        col = AutoPalette[autoIdx % AutoPalette.Length];
                        int packed = PackColor(col);
                        while (usedColors.Contains(packed) && autoIdx < AutoPalette.Length * 3)
                        {
                            autoIdx++;
                            col = AutoPalette[autoIdx % AutoPalette.Length];
                            packed = PackColor(col);
                        }
                        autoIdx++;
                        usedColors.Add(PackColor(col));
                    }

                    result[attr.TypeName] = new Entry { Type = type, Name = attr.TypeName, Color = col };
                }
            }

            return result;
        }

        static int PackColor(Color c)
            => ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);
    }
}