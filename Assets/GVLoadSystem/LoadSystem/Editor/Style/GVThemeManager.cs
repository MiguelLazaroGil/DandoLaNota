#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Manages the active GVTheme. Persisted in ProjectSettings.
    /// Automatically generates predefined themes on first use.
    /// Access the current theme via GVThemeManager.Current.
    /// </summary>
    [FilePath("ProjectSettings/GVThemeManager.asset",
              FilePathAttribute.Location.ProjectFolder)]
    internal class GVThemeManager : ScriptableSingleton<GVThemeManager>
    {
        [SerializeField] GVTheme _activeTheme;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Returns the active theme, falling back to Ocean Blue defaults.</summary>
        public static GVTheme Current
        {
            get
            {
                if (instance._activeTheme != null) return instance._activeTheme;
                // Fallback — return a temporary default so nothing breaks
                return _fallback ??= GVTheme.CreateOceanBlue();
            }
        }

        static GVTheme _fallback;

        public static GVTheme ActiveTheme
        {
            get => instance._activeTheme;
            set
            {
                instance._activeTheme = value;
                GVTheme.Current = value; // expose to runtime
                instance.Save(true);
                foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    w.Repaint();
            }
        }

        // ── Session initialization ────────────────────────────────────────
        // Runs once per editor session
        static bool s_initialized;

        [InitializeOnLoadMethod]
        static void InitOnLoad()
        {
            if (s_initialized) return;
            s_initialized = true;

            // Sync GVTheme.Current immediately — before any OnGUI runs
            // This prevents the overlay from using the wrong theme on first frame
            if (instance._activeTheme != null)
                GVTheme.Current = instance._activeTheme;

            EditorApplication.delayCall += EnsureThemesExist;
        }

        public static void EnsureThemesExist()
        {
            var themes = FindAllThemes();

            // Generate predefined themes if none exist
            if (themes.Count == 0)
            {
                string folder = FindOrCreateThemesFolder();
                themes = GeneratePredefinedThemes(folder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Assign first theme if none active
            if (instance._activeTheme == null && themes.Count > 0)
            {
                // Prefer OceanBlue as default
                var oceanBlue = themes.FirstOrDefault(
                    t => t.name.Contains("OceanBlue")) ?? themes[0];
                instance._activeTheme = oceanBlue;
                instance.Save(true);
            }
        }

        // ── Theme discovery ───────────────────────────────────────────────
        public static List<GVTheme> FindAllThemes()
        {
            return AssetDatabase.FindAssets("t:GVTheme")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("LoadSystem") &&
                            p.Contains("Themes"))
                .Select(p => AssetDatabase.LoadAssetAtPath<GVTheme>(p))
                .Where(t => t != null)
                .OrderBy(t => t.name)
                .ToList();
        }

        // ── Folder helpers ────────────────────────────────────────────────
        static string FindOrCreateThemesFolder()
        {
            // Find the LoadSystem folder anywhere in Assets
            string loadSystemPath = FindLoadSystemFolder();

            if (string.IsNullOrEmpty(loadSystemPath))
            {
                // Fallback — create at root
                loadSystemPath = "Assets/LoadSystem";
                if (!AssetDatabase.IsValidFolder(loadSystemPath))
                    AssetDatabase.CreateFolder("Assets", "LoadSystem");
            }

            string themesPath = loadSystemPath + "/Themes";
            if (!AssetDatabase.IsValidFolder(themesPath))
            {
                string parent = Path.GetDirectoryName(themesPath).Replace('\\', '/');
                string folder = Path.GetFileName(themesPath);
                AssetDatabase.CreateFolder(parent, folder);
            }

            return themesPath;
        }

        static string FindLoadSystemFolder()
        {
            // Search for a folder named "LoadSystem" anywhere in Assets
            foreach (var guid in AssetDatabase.FindAssets("LoadSystem t:Folder"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "LoadSystem")
                    return path;
            }
            return null;
        }

        // ── Predefined theme generation ───────────────────────────────────
        static List<GVTheme> GeneratePredefinedThemes(string folder)
        {
            var created = new List<GVTheme>();

            void Create(GVTheme theme)
            {
                string path = $"{folder}/{theme.name}.asset";
                AssetDatabase.CreateAsset(theme, path);
                created.Add(theme);
                Debug.Log($"[GVTheme] Created predefined theme: {path}");
            }

            Create(GVTheme.CreateOceanBlue());
            Create(GVTheme.CreateDarkForest());
            Create(GVTheme.CreateCrimson());
            Create(GVTheme.CreateMinimal());

            return created;
        }

        // ── Create custom theme ───────────────────────────────────────────
        public static GVTheme CreateCustomTheme(string themeName)
        {
            string folder = FindOrCreateThemesFolder();

            // Duplicate active theme as starting point
            var source = Current;
            var newTheme = Object.Instantiate(source);
            newTheme.name = themeName;

            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{themeName}.asset");
            AssetDatabase.CreateAsset(newTheme, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GVTheme] Created custom theme: {path}");
            return newTheme;
        }
    }
}
#endif