#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace MLG.SimpleSceneTool.CheckpointSystem
{
    /// <summary>
    /// Shared Scene Set picker and inline inspector. It keeps all editor surfaces
    /// consistent while still editing the referenced SceneSet asset itself.
    /// </summary>
    internal static class SceneSetEditorGUI
    {
        private const float FoldoutWidth = 16f;
        private const float BrowseButtonWidth = 62f;
        private static SceneSet[] cachedSceneSets;

        static SceneSetEditorGUI()
        {
            EditorApplication.projectChanged += ClearCache;
        }

        public static SceneSet DrawSceneSetReference(
            SerializedProperty property,
            GUIContent label,
            bool showContents,
            bool contentsEditable)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return null;
            }
            SceneSet current = property.objectReferenceValue as SceneSet;
            bool expanded = property.isExpanded;
            SceneSet selected = DrawSceneSetPickerRow(
                label,
                current,
                showContents,
                ref expanded,
                sceneSet => AssignReference(property, sceneSet));

            property.isExpanded = expanded;
            if (selected != current)
            {
                property.objectReferenceValue = selected;
                current = selected;
            }

            if (showContents && expanded)
            {
                DrawSceneSetContents(current, contentsEditable);
            }
            return current;
        }

        public static SceneSet DrawSceneSetPicker(
            GUIContent label,
            SceneSet current,
            ref bool expanded,
            bool showContents,
            bool contentsEditable,
            Action<SceneSet> onBrowseSelection)
        {
            SceneSet selected = DrawSceneSetPickerRow(label, current, showContents, ref expanded, onBrowseSelection);
            if (showContents && expanded)
            {
                DrawSceneSetContents(selected, contentsEditable);
            }
            return selected;
        }

        public static void DrawSceneSetContents(SceneSet sceneSet, bool editable)
        {
            if (sceneSet == null)
            {
                EditorGUILayout.HelpBox("No Scene Set selected.", MessageType.Info);
                return;
            }

            SerializedObject serializedSceneSet = new SerializedObject(sceneSet);
            serializedSceneSet.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Set Contents", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!editable))
            {
                EditorGUILayout.PropertyField(serializedSceneSet.FindProperty("displayName"));
                EditorGUILayout.PropertyField(serializedSceneSet.FindProperty("excludeFromBuild"));
                EditorGUILayout.PropertyField(serializedSceneSet.FindProperty("scenes"), true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedSceneSet.ApplyModifiedProperties();
                EditorUtility.SetDirty(sceneSet);
            }
            EditorGUILayout.EndVertical();
        }

        private static SceneSet DrawSceneSetPickerRow(
            GUIContent label,
            SceneSet current,
            bool showContents,
            ref bool expanded,
            Action<SceneSet> onBrowseSelection)
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect fieldRect = row;
            if (showContents)
            {
                Rect foldoutRect = new Rect(row.x, row.y, FoldoutWidth, row.height);
                expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                fieldRect.xMin += FoldoutWidth;
            }

            Rect browseRect = new Rect(fieldRect.xMax - BrowseButtonWidth, fieldRect.y, BrowseButtonWidth, fieldRect.height);
            fieldRect.xMax = browseRect.xMin - 2f;

            EditorGUI.BeginChangeCheck();
            SceneSet selected = (SceneSet)EditorGUI.ObjectField(fieldRect, label, current, typeof(SceneSet), false);
            if (EditorGUI.EndChangeCheck())
            {
                current = selected;
            }

            if (GUI.Button(browseRect, "Browse"))
            {
                ShowSceneSetSearchPopup(browseRect, onBrowseSelection);
            }
            return current;
        }

        private static void ShowSceneSetSearchPopup(Rect activatorRect, Action<SceneSet> onSelection)
        {
            PopupWindow.Show(activatorRect, new SceneSetSearchPopup(GetSceneSets(), true, onSelection));
        }

        private static SceneSet[] GetSceneSets()
        {
            if (cachedSceneSets != null)
            {
                return cachedSceneSets;
            }

            cachedSceneSets = AssetDatabase.FindAssets("t:SceneSet")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneSet>)
                .Where(sceneSet => sceneSet != null)
                .OrderBy(sceneSet => sceneSet.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return cachedSceneSets;
        }

        private static void AssignReference(SerializedProperty property, SceneSet sceneSet)
        {
            SerializedObject serializedObject = new SerializedObject(property.serializedObject.targetObject);
            SerializedProperty updatedProperty = serializedObject.FindProperty(property.propertyPath);
            if (updatedProperty == null)
            {
                return;
            }

            serializedObject.Update();
            updatedProperty.objectReferenceValue = sceneSet;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(property.serializedObject.targetObject);
        }

        private static void ClearCache()
        {
            cachedSceneSets = null;
        }
    }

    [CustomEditor(typeof(SceneSetDatabase))]
    internal class SceneSetDatabaseEditor : Editor
    {
        private SerializedProperty excludeFromBuild;
        private SerializedProperty initialSceneSet;
        private SerializedProperty sceneSets;
        private SerializedProperty expandSceneSets;
        private DefaultAsset importFolder;

        private void OnEnable()
        {
            excludeFromBuild = serializedObject.FindProperty("excludeFromBuild");
            initialSceneSet = serializedObject.FindProperty("initialSceneSet");
            sceneSets = serializedObject.FindProperty("sceneSets");
            expandSceneSets = serializedObject.FindProperty("expandSceneSetsInInspector");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(excludeFromBuild);
            bool showContents = expandSceneSets.boolValue = EditorGUILayout.Toggle(
                "Expand Scene Sets", expandSceneSets.boolValue);

            EditorGUILayout.Space();
            SceneSetEditorGUI.DrawSceneSetReference(initialSceneSet, new GUIContent("Initial Scene Set"), showContents, true);
            DrawSceneSetList(showContents);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Scene Sets", EditorStyles.boldLabel);
            importFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Folder", importFolder, typeof(DefaultAsset), false);
            using (new EditorGUI.DisabledScope(!IsValidFolder(importFolder)))
            {
                if (GUILayout.Button("Add Every Scene Set in Folder"))
                {
                    ImportSceneSetsFromFolder();
                    importFolder = null;
                    if (serializedObject.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }

        private void DrawSceneSetList(bool showContents)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Sets (" + sceneSets.arraySize + ")", EditorStyles.boldLabel);
            for (int i = 0; i < sceneSets.arraySize; i++)
            {
                SerializedProperty sceneSetProperty = sceneSets.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                SceneSetEditorGUI.DrawSceneSetReference(sceneSetProperty, new GUIContent("Scene Set " + (i + 1)), showContents, true);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button("−", GUILayout.Width(24f)))
                {
                    sceneSets.DeleteArrayElementAtIndex(i);
                    if (sceneSets.arraySize == i + 1 && sceneSetProperty.objectReferenceValue != null)
                    {
                        sceneSets.DeleteArrayElementAtIndex(i);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Scene Set Slot"))
            {
                sceneSets.InsertArrayElementAtIndex(sceneSets.arraySize);
                sceneSets.GetArrayElementAtIndex(sceneSets.arraySize - 1).objectReferenceValue = null;
            }
        }

        private void ImportSceneSetsFromFolder()
        {
            string folderPath = AssetDatabase.GetAssetPath(importFolder);
            SceneSet[] foundSceneSets = AssetDatabase.FindAssets("t:SceneSet", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneSet>)
                .Where(sceneSet => sceneSet != null)
                .ToArray();

            SceneSetDatabase database = (SceneSetDatabase)target;
            Undo.RecordObject(database, "Import Scene Sets into Database");
            int addedCount = database.AddSceneSets(foundSceneSets);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + addedCount + " Scene Set(s) from '" + folderPath + "'.", database);
        }

        private static bool IsValidFolder(DefaultAsset folder)
        {
            return folder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder));
        }
    }

    [CustomEditor(typeof(ChangeSceneSet))]
    internal class ChangeSceneSetEditor : Editor
    {
        private SerializedProperty modeProperty;
        private SerializedProperty sceneSetProperty;
        private SerializedProperty sceneSetsProperty;
        private SerializedProperty unloadUnusedImmediatelyProperty;

        private void OnEnable()
        {
            modeProperty = serializedObject.FindProperty("mode");
            sceneSetProperty = serializedObject.FindProperty("sceneSet");
            sceneSetsProperty = serializedObject.FindProperty("sceneSets");
            unloadUnusedImmediatelyProperty = serializedObject.FindProperty("unloadUnusedImmediately");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(modeProperty);

            SceneSetLoadMode mode = (SceneSetLoadMode)modeProperty.enumValueIndex;
            if (mode == SceneSetLoadMode.Single)
            {
                SceneSetEditorGUI.DrawSceneSetReference(sceneSetProperty, new GUIContent("Scene Set"), true, false);
            }
            else
            {
                EditorGUILayout.PropertyField(sceneSetsProperty, true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(unloadUnusedImmediatelyProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
    /// <summary>
    /// Lightweight search popup for picking a Scene Set by DisplayName. Opened from the
    /// Browse button so the picker never occupies permanent inspector space.
    /// </summary>
    internal sealed class SceneSetSearchPopup : PopupWindowContent
    {
        private const float Width = 280f;
        private const float RowHeight = 20f;
        private const float MaxListHeight = 260f;
        private const string SearchControlName = "SimpleScene.SceneSetSearchPopup.Search";

        private readonly List<SceneSet> options;
        private readonly bool allowNone;
        private readonly Action<SceneSet> onSelected;
        private string search = string.Empty;
        private Vector2 scrollPosition;
        private bool hasFocusedSearchField;

        public SceneSetSearchPopup(IEnumerable<SceneSet> options, bool allowNone, Action<SceneSet> onSelected)
        {
            this.options = new List<SceneSet>(options);
            this.allowNone = allowNone;
            this.onSelected = onSelected;
        }

        public override Vector2 GetWindowSize()
        {
            int rowCount = GetFiltered().Count + (allowNone ? 1 : 0);
            float listHeight = Mathf.Clamp(rowCount * RowHeight, RowHeight, MaxListHeight);
            return new Vector2(Width, listHeight + EditorGUIUtility.singleLineHeight + 12f);
        }

        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName(SearchControlName);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            if (!hasFocusedSearchField)
            {
                EditorGUI.FocusTextInControl(SearchControlName);
                hasFocusedSearchField = true;
            }

            List<SceneSet> filtered = GetFiltered();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (allowNone && GUILayout.Button("None", EditorStyles.label))
            {
                Select(null);
            }

            foreach (SceneSet sceneSet in filtered)
            {
                string label = sceneSet.DisplayName;
                if (sceneSet.ExcludeFromBuild)
                {
                    label += "  (excluded)";
                }
                if (GUILayout.Button(label, EditorStyles.label))
                {
                    Select(sceneSet);
                }
            }

            if (filtered.Count == 0)
            {
                EditorGUILayout.LabelField("No matching Scene Sets.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private List<SceneSet> GetFiltered()
        {
            if (string.IsNullOrEmpty(search))
            {
                return options;
            }
            return options.FindAll(sceneSet =>
                sceneSet != null && sceneSet.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Select(SceneSet sceneSet)
        {
            onSelected(sceneSet);
            editorWindow.Close();
        }
    }
#endif
}