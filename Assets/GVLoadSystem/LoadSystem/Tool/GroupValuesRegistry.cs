using System.Collections.Generic;
using UnityEngine;
using GVLoadSystem.Core;
using GVLoadSystem.GVEditor;



#if UNITY_EDITOR
using UnityEditor;

namespace GVLoadSystem
{
    public static class GroupValuesRegistry
    {
        static readonly HashSet<string> reservedFileNames = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase) { "Documentation" };
        static ALoader loader;
        public static List<GroupValues> GetAll()
        {
            var guids = AssetDatabase.FindAssets("t:GroupValues");
            var list = new List<GroupValues>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (reservedFileNames.Contains(fileName)) continue;
                list.Add(AssetDatabase.LoadAssetAtPath<GroupValues>(path));
            }
            return list;
        }

        public static List<GroupValuesTemplate> GetAllTemplates()
        {
            var guids = AssetDatabase.FindAssets("t:GroupValuesTemplate");
            var list = new List<GroupValuesTemplate>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                list.Add(AssetDatabase.LoadAssetAtPath<GroupValuesTemplate>(path));
            }
            return list;
        }

        public static GroupValues FindByName(string name)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:GroupValues");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GroupValues>(path);
        }
    }
}
#endif

namespace GVLoadSystem
{
    /// <summary>
    /// Runs before the first scene loads and migrates all GroupValues JSON files
    /// to match the current SO structure if their versions differ.
    /// Only runs in editor and development builds.
    /// </summary>

    internal static class GroupValuesMigrationRunner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RunMigrations()
        {
#if UNITY_EDITOR
           // Debug.Log($"[Migration] RunMigrations called — isPlaying: {Application.isPlaying}");
            var settings = GroupValuesProjectSettings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[Migration] GroupValuesProjectSettings not found. Skipping.");
                return;
            }

            var allGVs = GroupValuesRegistry.GetAll();
            if (allGVs == null || allGVs.Count == 0) return;

#if LOG_LOADSYSTEM
        Debug.Log($"[Migration] Checking {allGVs.Count} GroupValues for migration...");
#endif

            int migrated = 0;
            foreach (var gv in allGVs)
            {
                if (gv == null) continue;

                // Excluir GVs dentro de la carpeta del paquete
                string assetPath = AssetDatabase.GetAssetPath(gv);
                if (assetPath.Contains("GVLoadSystem")) continue;


                var loader = new ALoader();
                loader.SetGroupValues(gv);
                loader.ChangeAssetName(gv.name);

                if (!loader.AutoResolveFromResources())
                {
                    continue;
                }

                loader.SetEncryptionSettings(
                    settings.encryptionMethod,
                    settings.passwordSalt);

                loader.MigrateIfNeeded();
                migrated++;
            }

#if LOG_LOADSYSTEM
        Debug.Log($"[Migration] Migration check complete for {migrated} GroupValues.");
#endif
#else
        // In builds, migration is handled differently — GVs are loaded from Resources
        // and the loader handles version checking during LoadValues()
        RunBuildMigrations();
#endif
        }

#if !UNITY_EDITOR
    static void RunBuildMigrations()
    {
       

#if LOG_LOADSYSTEM
        Debug.Log("[Migration] Build migration skipped — handled per-loader at LoadValues time.");
#endif

       
    }
#endif
    }
}