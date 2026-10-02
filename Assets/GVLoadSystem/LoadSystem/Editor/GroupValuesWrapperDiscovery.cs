#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Automatically discovers all declared fields of type GroupValuesWrapper&lt;T&gt;
    /// across all assemblies and registers the inner T types in CustomSettingDataRegistry.
    /// Results are cached in SessionState — only re-scans after a new compilation.
    /// </summary>
    [InitializeOnLoad]
    internal static class GroupValuesWrapperDiscovery
    {
        // SessionState keys
        const string k_CacheKey = "GVWrapper_CachedTypes";   // comma-separated type names
        const string k_DirtyKey = "GVWrapper_NeedsRescan";   // "1" if rescan needed
        const string k_AssemblyHash = "GVWrapper_AssemblyHash";  // hash of loaded assemblies

        static GroupValuesWrapperDiscovery()
        {
            // Mark dirty when a compilation finishes so next access triggers rescan
            CompilationPipeline.compilationFinished += _ =>
                SessionState.SetBool(k_DirtyKey, true);

            EditorApplication.delayCall += () =>
            {
                if (NeedsRescan())
                    DiscoverAndCache();
                else
                    RestoreFromCache();
            };
        }

        [MenuItem("Tools/LoadSystem/Refresh Wrapper Types", priority = 1000)]
        public static void DiscoverManual()
        {
            int count = DiscoverAndCache();
            Debug.Log($"[GroupValuesWrapper] Discovered {count} wrapper type(s).");
        }

        // ── Cache helpers ─────────────────────────────────────────────────

        static bool NeedsRescan()
        {
            // Dirty flag set by compilation hook
            if (SessionState.GetBool(k_DirtyKey, true)) return true;

            // Also rescan if cache is empty
            string cache = SessionState.GetString(k_CacheKey, "");
            return string.IsNullOrEmpty(cache);
        }

        static void RestoreFromCache()
        {
            string cache = SessionState.GetString(k_CacheKey, "");
            if (string.IsNullOrEmpty(cache)) return;

            int restored = 0;
            foreach (var typeName in cache.Split(','))
            {
                if (string.IsNullOrEmpty(typeName)) continue;

                // Resolve type by name across all assemblies
                var type = FindType(typeName);
                if (type == null) continue;

                CustomGVDataRegistry.RegisterType(type);
                restored++;
            }
#if LOG_LOADSYSTEM
        if (restored > 0)
            Debug.Log($"[GroupValuesWrapper] Restored {restored} wrapper type(s) from cache.");
#endif
        }

        static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        // ── Discovery ─────────────────────────────────────────────────────

        static int DiscoverAndCache()
        {
            var wrapperGenericType = typeof(GroupValuesWrapper<>);
            var discovered = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = assembly.GetName().Name;
                if (asmName.StartsWith("Unity") ||
                    asmName.StartsWith("System") ||
                    asmName.StartsWith("mscorlib") ||
                    asmName.StartsWith("netstandard") ||
                    asmName.StartsWith("Microsoft")) continue;

                IEnumerable<Type> types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e)
                { types = e.Types.Where(t => t != null); }

                foreach (var type in types)
                    ScanType(type, wrapperGenericType, discovered);
            }

            // Validate and register
            var valid = new List<Type>();
            foreach (var t in discovered)
            {
                if (!t.IsSerializable)
                {
                    Debug.LogWarning(
                        $"[GroupValuesWrapper] '{t.Name}' is used in a GroupValuesWrapper " +
                        $"but is not [Serializable]. Add [Serializable] to enable " +
                        $"inspector editing and JSON serialization.");
                    continue;
                }
                CustomGVDataRegistry.RegisterType(t);
                valid.Add(t);
            }

            // Write cache — store fully qualified names for reliable restoration
            string cacheValue = string.Join(",", valid.Select(t => t.FullName));
            SessionState.SetString(k_CacheKey, cacheValue);
            SessionState.SetBool(k_DirtyKey, false);

            return valid.Count;
        }

        static void ScanType(Type type, Type wrapperGenericType, List<Type> discovered)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static;

            foreach (var field in type.GetFields(flags))
                TryAdd(field.FieldType, wrapperGenericType, discovered);

            foreach (var prop in type.GetProperties(flags))
                TryAdd(prop.PropertyType, wrapperGenericType, discovered);
        }

        static void TryAdd(Type ft, Type wrapperGenericType, List<Type> discovered)
        {
            if (!ft.IsGenericType) return;
            if (ft.GetGenericTypeDefinition() != wrapperGenericType) return;
            var inner = ft.GetGenericArguments()[0];
            if (!discovered.Contains(inner))
                discovered.Add(inner);
        }
    }
}
#endif