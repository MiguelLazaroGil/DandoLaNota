#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GVLoadSystem.Encryption;
using GVLoadSystem.Core;


namespace GVLoadSystem.GVEditor
{
    [FilePath("ProjectSettings/GroupValuesProjectSettings.asset",
              FilePathAttribute.Location.ProjectFolder)]
    internal class GroupValuesProjectSettings : ScriptableSingleton<GroupValuesProjectSettings>
    {
        public bool useTemplates;
        public bool useBackups = false;
        public string saveSubfolder = "";
        public EncryptionMethod encryptionMethod = EncryptionMethod.None;
        public bool enableLogs = false;
        public bool showHelpButton = true;
        public bool nicifyFieldNames = true;
        public string passwordSalt;

        // Debug Overlay — shown in Project Settings via GroupValuesSettingsProvider
        public bool enableDebugOverlay = false;
        public KeyCode debugKey = KeyCode.F1;
        public bool overlayEditMode = false;
        public bool freezeTimeOnOverlay = false;

        public static GroupValuesProjectSettings GetOrCreateSettings(string newPass = null)
        {
            var settings = instance;

            if (newPass == null)
            {
                if (string.IsNullOrEmpty(settings.passwordSalt))
                {
                    settings.passwordSalt = PasswordGenerator.Generate(string.Empty);
                    settings.Save(true);
                }
            }
            else
            {
                settings.passwordSalt = newPass;
            }

            return settings;
        }

        const string LogDefine = "LOG_LOADSYSTEM";

        public void Save()
        {
            Save(true);
            UpdateAllLoaders();
            ApplyLogDefine();
            // Ensure GroupValuesDependencySolver picks up any changes to saveSubfolder or debug overlay settings
            GroupValuesDependencySolver.SetSaveSubfolder(saveSubfolder);
            GroupValuesDependencySolver.SetOverlayConfig(
                enableDebugOverlay, debugKey, overlayEditMode, freezeTimeOnOverlay);
        }

        public void ApplyLogDefine()
        {
            var target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            var list = new List<string>(defines);
            bool has = list.Contains(LogDefine);

            if (enableLogs && !has)
            {
                list.Add(LogDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
            else if (!enableLogs && has)
            {
                list.Remove(LogDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
        }

        // ── Apply encryption settings to every ALoader in the project ────
        void UpdateAllLoaders()
        {
            int updatedLoaded = 0;
            int updatedPrefabs = 0;
            int updatedScenes = 0;

            // 1. Loaded scenes — fast, in memory
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<LoaderMono>(true))
                    {
                        mb.ApplySettings(encryptionMethod, passwordSalt, useBackups, saveSubfolder);
                        updatedLoaded++;
                    }
            }

            if (updatedLoaded > 0)
                EditorSceneManager.MarkAllScenesDirty();

            // 2. Prefabs in project — find every prefab that has a LoaderMono
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var loaders = prefab.GetComponentsInChildren<LoaderMono>(true);
                if (loaders.Length == 0) continue;

                bool dirty = false;
                foreach (var loader in loaders)
                {
                    loader.ApplySettings(encryptionMethod, passwordSalt, useBackups, saveSubfolder);
                    dirty = true;
                    updatedPrefabs++;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }

            // 3. Scenes not currently loaded — open each one, update, save, close
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");

            // Collect paths of already-loaded scenes so we skip them
            var loadedPaths = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                loadedPaths.Add(SceneManager.GetSceneAt(i).path);

            foreach (var guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);

                // Only process scenes inside the project's Assets folder.
                // Scenes from packages (Packages/...), Unity templates, etc.
                // cannot be opened with EditorSceneManager and have no LoaderMono anyway.
                if (!scenePath.StartsWith("Assets/")) continue;
                if (loadedPaths.Contains(scenePath)) continue;

                // Open scene additively (non-destructive, doesn't affect active scene)
                Scene opened = EditorSceneManager.OpenScene(
                    scenePath, OpenSceneMode.Additive);

                bool sceneDirty = false;
                foreach (var root in opened.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<LoaderMono>(true))
                    {
                        mb.ApplySettings(encryptionMethod, passwordSalt, useBackups, saveSubfolder);
                        sceneDirty = true;
                        updatedScenes++;
                    }

                if (sceneDirty)
                    EditorSceneManager.SaveScene(opened);

                // Close the scene — don't leave it open
                EditorSceneManager.CloseScene(opened, true);
            }

#if LOG_LOADSYSTEM
            Debug.Log($"[GroupValuesProjectSettings] Encryption settings applied — " +
                      $"loaded scenes: {updatedLoaded}, prefabs: {updatedPrefabs}, " +
                      $"unloaded scenes: {updatedScenes}");
#endif

        }
    }

    // ── Auto-apply settings when LoaderMono is added to a GameObject ─────
    [InitializeOnLoad]
    static class LoaderMonoInitializer
    {
        static LoaderMonoInitializer()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        static void OnComponentAdded(Component component)
        {
            if (component is not LoaderMono loader) return;

            var settings = GroupValuesProjectSettings.instance;
            loader.ApplySettings(settings.encryptionMethod, settings.passwordSalt, settings.useBackups, settings.saveSubfolder);
            EditorUtility.SetDirty(loader);

#if LOG_LOADSYSTEM
            Debug.Log($"[LoaderMono] Encryption settings applied to '{loader.gameObject.name}'.");
#endif
        }
    }
}
#endif