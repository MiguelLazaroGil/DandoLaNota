#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using GVLoadSystem.GVEditor;
using GVLoadSystem.Core;
using GVLoadSystem.Settings;
using GVLoadSystem.Encryption;

namespace GVLoadSystem
{
    internal class GroupValuesBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        static ALoader loader = new ALoader();

        /*
        STEPS BEFORE MAKING BUILD
        1. VALIDATE ALL UIGVElement REFERENCES
        2. CHECK THAT EACH LOADERMONO HAS THE RIGHT ENCRYPTION METHOD AND PASS
        3. ERASE TESTING DATA (PlayerPrefs only — never touch persistent JSON files)
        4. SET DEFAULT VALUES IN SOs FROM TEMPLATES
        5. RESET NON-TEMPLATE GVs TO DEFAULTS IN THE SO
        NOTE: We do NOT save JSONs here — the player's persistent JSON is their data.
              On first install there is no JSON so LoadValues creates one from SO defaults.
              On update the JSON exists and LoadValues preserves the player's values.
        */
        public void OnPreprocessBuild(BuildReport report)
        {
#if LOG_LOADSYSTEM
            Debug.Log("[GroupValuesBuildProcessor] build preprocess started");
#endif

            // ── Step 1: Validate UIGVElement references ───────────────────
            ValidateUISettingsReferences();

            // ── Step 2: Bake overlay config ───────────────────────────────
            BakeOverlayConfig();

            // ── Step 3: Encryption ────────────────────────────────────────
            ApplyEncryptionSettings();

            // ── Step 4: Clear testing data (PlayerPrefs only) ─────────────
            ClearTestingData();

            // ── Steps 5-6: Templates & SO defaults ───────────────────────
            var templates = GroupValuesRegistry.GetAllTemplates();

            if (GroupValuesProjectSettings.instance.useTemplates)
                ApplyTemplateDefaults(templates);

            ResetNonTemplateGroupValues(templates);

#if LOG_LOADSYSTEM
            Debug.Log("[GroupValuesBuildProcessor] build preprocess finished");
#endif
        }
        [MenuItem("Tools/LoadSystem/Export Clean Package", priority = 500)]
        [System.Obsolete]
        static void ExportCleanPackage()
        {
            var settings = GroupValuesProjectSettings.instance;

            //
            var savedEncryption = settings.encryptionMethod;
            var savedBackups = settings.useBackups;
            var savedSalt = settings.passwordSalt;
            var savedSubfolder = settings.saveSubfolder;
            var savedLogs = settings.enableLogs;

            try
            {
                // 
                settings.encryptionMethod = EncryptionMethod.None;
                settings.useBackups = false;
                settings.passwordSalt = "";
                settings.saveSubfolder = "";
                settings.enableLogs = false;
                settings.Save();

                // 
                string path = EditorUtility.SaveFilePanel(
                    "Export GV Load System", "", "GVLoadSystem", "unitypackage");

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.ExportPackage(
                        "Assets/GVLoadSystem",
                        path,
                        ExportPackageOptions.Recurse);

                    Debug.Log($"[GVLoadSystem] Package exported to: {path}");
                }
            }
            finally
            {
                //
                settings.encryptionMethod = savedEncryption;
                settings.passwordSalt = savedSalt;
                settings.saveSubfolder = savedSubfolder;
                settings.enableLogs = savedLogs;
                settings.useBackups = savedBackups;
                settings.Save();
            }
        }
        // ── UIGVElement validation ────────────────────────────────────────
        [MenuItem("Tools/LoadSystem/Validate UI Settings References", priority = 20)]
        public static void ValidateManual()
        {
            var errors = CollectUISettingsErrors();
            if (errors.Count == 0)
            {
                Debug.Log("[UISettings] ✓ All GVEntryReferences are valid.");
                EditorUtility.DisplayDialog("Validation Passed",
                    "All UIGVElement references are valid.", "OK");
            }
            else
            {
                foreach (var (msg, ctx) in errors)
                    Debug.LogError(msg, ctx);
                EditorUtility.DisplayDialog("Validation Failed",
                    $"{errors.Count} invalid reference(s) found.\nCheck the Console for details.", "OK");
            }
        }

        static void ValidateUISettingsReferences()
        {
            var errors = CollectUISettingsErrors();
            if (errors.Count == 0) return;
            foreach (var (msg, ctx) in errors)
                Debug.LogError(msg, ctx);
            throw new BuildFailedException(
                $"[UISettings] Build blocked: {errors.Count} invalid GVEntryReference(s). " +
                $"Fix them before building.");
        }

        [System.Obsolete]
        static List<(string msg, Object ctx)> CollectUISettingsErrors()
        {
            var errors = new List<(string, Object)>();
            var elements = Object.FindObjectsByType<UIGVElement>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var elem in elements)
            {
                var entry = elem.Entry;
                if (entry == null)
                {
                    errors.Add(($"[UISettings] '{elem.gameObject.name}' has null GVEntryReference.",
                                 elem.gameObject));
                    continue;
                }
                if (entry.GroupValues == null)
                {
                    errors.Add(($"[UISettings] '{elem.gameObject.name}': no GroupValues assigned.",
                                 elem.gameObject));
                    continue;
                }
                if (string.IsNullOrEmpty(entry.EntryKey))
                {
                    errors.Add(($"[UISettings] '{elem.gameObject.name}': no entry key assigned.",
                                 elem.gameObject));
                    continue;
                }
                if (!entry.IsValid)
                {
                    errors.Add(($"[UISettings] '{elem.gameObject.name}': key '{entry.EntryKey}' " +
                                 $"not found in '{entry.GroupValues.name}'.",
                                 entry.GroupValues));
                }
            }
            return errors;
        }

        // ── Overlay config baking ─────────────────────────────────────────
        void BakeOverlayConfig()
        {

            GroupValuesDependencySolver.SetSaveSubfolder(
            GroupValuesProjectSettings.instance.saveSubfolder);

            GroupValuesDependencySolver.SetOverlayConfig(
                GroupValuesProjectSettings.instance.enableDebugOverlay,
                GroupValuesProjectSettings.instance.debugKey,
                GroupValuesProjectSettings.instance.overlayEditMode,
                GroupValuesProjectSettings.instance.freezeTimeOnOverlay);

        }

        // ── Encryption ────────────────────────────────────────────────────
        void ApplyEncryptionSettings()
        {
            loader.SetEncryptionSettings(
                GroupValuesProjectSettings.instance.encryptionMethod,
                GroupValuesProjectSettings.instance.passwordSalt);

            var settings = GroupValuesProjectSettings.instance;

#if UNITY_6000_0_OR_NEWER
var loaders = Object.FindObjectsByType<LoaderMono>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var loaders = Object.FindObjectsOfType<LoaderMono>(true);
#endif

            foreach (var l in loaders)
            {
                l.ApplySettings(settings.encryptionMethod, settings.passwordSalt, settings.useBackups, settings.saveSubfolder);
                EditorUtility.SetDirty(l);
            }
        }

        // ── Clear testing data ────────────────────────────────────────────
        void ClearTestingData()
        {
            // Only clear PlayerPrefs — never touch persistent JSON files
            // Those belong to the player (even in editor)
            PlayerPrefs.DeleteAll();
        }

        // ── Template defaults → SO only, no JSON ─────────────────────────
        void ApplyTemplateDefaults(List<GroupValuesTemplate> templates)
        {
            foreach (var template in templates)
            {
                if (template.groupValuesReference == null)
                    throw new BuildFailedException(
                        "[GroupValuesBuildProcessor] Template without reference detected");

                if (template.fields == null)
                    throw new BuildFailedException(
                        "[GroupValuesBuildProcessor] Template without fields detected");

                // Apply template defaults to the SO only — never touch persistentDataPath
                template.SetDefaultValuesInSO();
                EditorUtility.SetDirty(template.groupValuesReference);
            }
            AssetDatabase.SaveAssets();
        }

        // ── Reset non-template GVs → SO only, no JSON ────────────────────
        void ResetNonTemplateGroupValues(List<GroupValuesTemplate> templates)
        {
            var referenced = new HashSet<string>();
            foreach (var template in templates)
                if (template.groupValuesReference != null)
                    referenced.Add(template.groupValuesReference.name);

            var groupValues = GroupValuesRegistry.GetAll();
            foreach (var gv in groupValues)
            {
                if (gv == null)
                    throw new BuildFailedException(
                        "[GroupValuesBuildProcessor] GroupValue is null");

                if (referenced.Contains(gv.name)) continue;

                // Only reset the SO asset — persistentDataPath is NEVER touched here.
                // First install: no JSON in persistentDataPath → LoadValues creates it from SO defaults.
                // Update: JSON exists in persistentDataPath → LoadValues preserves player values.
                gv.ResetToDefaults();
                EditorUtility.SetDirty(gv);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif