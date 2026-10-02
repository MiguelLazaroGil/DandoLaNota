#if UNITY_EDITOR
using System.Linq;
using GVLoadSystem.Encryption;
using UnityEditor;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{
    static class GroupValuesSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider("Project/Load System", SettingsScope.Project)
            {
                label = "Load System",
                guiHandler = (searchContext) =>
{
    var settings = GroupValuesProjectSettings.instance;
    SerializedObject so = new SerializedObject(settings);
    so.Update();

    SerializedProperty saveSubfolderProp = so.FindProperty("saveSubfolder");
    SerializedProperty usebackupsProp = so.FindProperty("useBackups");
    SerializedProperty useTemplatesProp = so.FindProperty("useTemplates");
    SerializedProperty encryptionProp = so.FindProperty("encryptionMethod");

    EditorGUILayout.LabelField("Group Values", EditorStyles.boldLabel);

    string controlName = "SaveSubfolderField";
    GUI.SetNextControlName(controlName);
    EditorGUI.BeginChangeCheck();
    EditorGUILayout.PropertyField(saveSubfolderProp,
        new GUIContent("Save Subfolder", "..."));
    bool subfolderChanged = EditorGUI.EndChangeCheck();

    if (subfolderChanged)
        so.ApplyModifiedProperties();


    bool pressedEnter = Event.current.type == EventType.KeyDown &&
                        (Event.current.keyCode == KeyCode.Return ||
                         Event.current.keyCode == KeyCode.KeypadEnter ||
                         Event.current.keyCode == KeyCode.Tab);
    bool clickedOutside = Event.current.type == EventType.MouseDown &&
                          GUI.GetNameOfFocusedControl() == controlName;

    if ((pressedEnter || clickedOutside) &&
        GUI.GetNameOfFocusedControl() == controlName)
    {
        so.ApplyModifiedProperties();
        settings.Save();
        GUI.FocusControl(null);
    }

    EditorGUI.BeginChangeCheck();

    EditorGUILayout.PropertyField(usebackupsProp,
        new GUIContent("Use Backups",
            "Writes a backup before each save. If the main file is corrupted, " +
            "the backup is used automatically."));
    EditorGUILayout.PropertyField(useTemplatesProp,
        new GUIContent("Use Templates"));
    EditorGUILayout.PropertyField(encryptionProp,
        new GUIContent("Encryption Method"));

    if (EditorGUI.EndChangeCheck())
    {
        so.ApplyModifiedProperties();
        settings.Save();
    }

    // ── Password Salt ─────────────────────────────────────────────
    EditorGUI.BeginDisabledGroup(true);
    EditorGUILayout.TextField("Password Salt", settings.passwordSalt);
    EditorGUI.EndDisabledGroup();

    if (GUILayout.Button("Generate New Salt"))
    {
        settings.passwordSalt = PasswordGenerator.GenerateNewPassword("");
        GroupValuesProjectSettings.GetOrCreateSettings();
        settings.Save();
    }
}

            };

            // ── Debug Overlay + Theme sections ───────────────────────────
            var _baseDraw = provider.guiHandler;
            provider.guiHandler = searchCtx =>
            {
                _baseDraw?.Invoke(searchCtx);

                var settings = GroupValuesProjectSettings.instance;
                if (settings == null) return;
                var so = new UnityEditor.SerializedObject(settings);
                so.Update(); // must call Update before reading/drawing properties

                // Debug Overlay
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(so.FindProperty("enableLogs"),
                    new GUIContent("Enable Info Logs",
                        "Adds LOG_LOADSYSTEM define. " +
                        "Shows Debug.Log messages from the LoadSystem. " +
                        "Warnings and errors always show regardless of this setting."));
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    GroupValuesProjectSettings.instance.ApplyLogDefine();
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("showHelpButton"),
                 new GUIContent("Show Help Button",
                "Shows a ? button in the LoaderMono inspector that opens the documentation."));

                EditorGUILayout.PropertyField(so.FindProperty("nicifyFieldNames"),
                    new GUIContent("Nicify Field Names",
                        "Converts camelCase field names to readable words, like Unity's inspector."));  
                so.ApplyModifiedProperties();

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Debug Overlay", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("enableDebugOverlay"),
                    new GUIContent("Enable Overlay",
                        "Shows an in-game overlay when the debug key is pressed."));
                EditorGUILayout.PropertyField(so.FindProperty("debugKey"),
                    new GUIContent("Debug Key", "Key to toggle the overlay."));
                EditorGUILayout.PropertyField(so.FindProperty("overlayEditMode"),
                    new GUIContent("Edit Mode",
                        "Allow editing GroupValues values from the overlay."));
                EditorGUILayout.PropertyField(so.FindProperty("freezeTimeOnOverlay"),
                    new GUIContent("Freeze Time on Open",
                        "Sets Time.timeScale = 0 while the overlay is visible."));
                so.ApplyModifiedProperties();

                // Theme
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Theme", EditorStyles.boldLabel);
                DrawThemeSection();
            };

            return provider;
        }

        static void DrawThemeSection()
        {
            var themes = GVThemeManager.FindAllThemes();

            if (themes.Count == 0)
            {
                EditorGUILayout.HelpBox("No themes found. Click 'Generate Defaults' to create them.",
                    MessageType.Warning);
                if (GUILayout.Button("Generate Default Themes"))
                    GVThemeManager.EnsureThemesExist();
                return;
            }

            // Theme selector dropdown
            var current = GVThemeManager.ActiveTheme;
            int curIdx = themes.IndexOf(current);
            if (curIdx < 0) curIdx = 0;

            string[] names = themes.Select(t => t.name
                .Replace("GVTheme_", "").Replace("_", " ")).ToArray();

            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("Active Theme", curIdx, names);
            if (EditorGUI.EndChangeCheck() && newIdx != curIdx)
                GVThemeManager.ActiveTheme = themes[newIdx];

            // Preview swatches
            EditorGUILayout.BeginHorizontal();
            var t = GVThemeManager.Current;
            DrawSwatch(t.backgroundDeep, "Bg");
            DrawSwatch(t.accent, "Accent");
            DrawSwatch(t.textPrimary, "Text");
            DrawSwatch(t.valid, "Valid");
            DrawSwatch(t.warning, "Warn");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            // Edit active theme
            if (GUILayout.Button("Edit Theme", GUILayout.Width(100)))
                Selection.activeObject = GVThemeManager.ActiveTheme;

            // Create new custom theme
            if (GUILayout.Button("New Theme", GUILayout.Width(100)))
            {
                string themeName = "GVTheme_Custom";
                var newTheme = GVThemeManager.CreateCustomTheme(themeName);
                GVThemeManager.ActiveTheme = newTheme;
                Selection.activeObject = newTheme;
            }

            // Regenerate defaults
            if (GUILayout.Button("Regenerate Defaults", GUILayout.Width(140)))
                GVThemeManager.EnsureThemesExist();

            EditorGUILayout.EndHorizontal();
        }

        static void DrawSwatch(Color color, string label)
        {
            Rect r = GUILayoutUtility.GetRect(40, 16,
                GUILayout.Width(40), GUILayout.Height(16));
            EditorGUI.DrawRect(r, color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax, r.width, 1),
                               new Color(0, 0, 0, 0.5f));
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            style.normal.textColor = color.grayscale > 0.5f
                ? Color.black : Color.white;
            GUI.Label(r, label, style);
        }
    }
}
#endif