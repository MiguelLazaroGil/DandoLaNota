#if UNITY_EDITOR
using System.Collections.Generic;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{
    [CustomPropertyDrawer(typeof(GVField))]
    internal class SettingFieldDrawer : PropertyDrawer
    {
        struct ListCache
        {
            public ReorderableList list;
            public SerializedObject so;
            // Width of the element rect inside the ReorderableList.
            // Derived once per OnGUI call and stored here so elementHeightCallback
            // always reads a consistent value — never a stale one from a layout pass
            // with a different panel width.
            public float elementWidth;
        }

        static readonly Dictionary<string, ListCache> s_cache = new();
        static readonly GVEntryDrawer s_entryDrawer = new();

        // ── GetPropertyHeight ─────────────────────────────────────────────
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line + 4f;  // header only when folded
            return line + 6f + GetList(property).GetHeight();
        }

        // ── OnGUI ─────────────────────────────────────────────────────────
        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            var nameProp = property.FindPropertyRelative("fieldName");
            string key = Key(property);

            // Compute and store element width before DoList so elementHeightCallback
            // uses the width that matches this exact layout pass.
            // ReorderableList (draggable) reserves 20px on the left for the drag handle.
            // We measure it from pos.width minus that margin.
            float elementWidth = pos.width - 20f;
            if (s_cache.TryGetValue(key, out var cached))
            {
                cached.elementWidth = elementWidth;
                s_cache[key] = cached;
            }

            var list = GetList(property);

            // Header
            Rect hr = new Rect(pos.x, pos.y, pos.width, line + 4f);
            GVEditorStyles.DrawBox(hr, GVEditorStyles.C_Header, GVEditorStyles.C_HeaderBdr);
            GVEditorStyles.DrawAccentBar(hr, GVEditorStyles.C_HeaderBdr);

            // Foldout toggle
            bool expanded = property.isExpanded;
            Rect foldR = new Rect(hr.x + 4f, hr.y + 3f, 16f, 14f);
            if (GUI.Button(foldR, expanded ? "▾" : "▸",
                           GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr)))
            {
                property.isExpanded = !property.isExpanded;
            }

            // "FIELD" label
            Rect fieldLblR = new Rect(foldR.xMax + 4f, hr.y + 2f, 36f, line);
            EditorGUI.LabelField(fieldLblR, "FIELD",
                GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr));

            // Field name text input
            Rect nameR = new Rect(fieldLblR.xMax + 4f, hr.y + 2f,
                                  hr.xMax - fieldLblR.xMax - 8f, line);
            EditorGUI.PropertyField(nameR, nameProp, GUIContent.none);

            // Body — only drawn when expanded
            if (!property.isExpanded) return;
            Rect lr = new Rect(pos.x, hr.yMax + 2f, pos.width, list.GetHeight());
            GVEditorStyles.DrawBox(lr, GVEditorStyles.C_Body, GVEditorStyles.C_Border);
            list.DoList(lr);
        }

        // ── Cache ─────────────────────────────────────────────────────────
        static string Key(SerializedProperty p)
            => /*p.serializedObject.targetObject.GetInstanceID() +*/ "_" + p.propertyPath;

        ReorderableList GetList(SerializedProperty property)
        {
            string key = Key(property);
            SerializedObject so = property.serializedObject;

            if (s_cache.TryGetValue(key, out var hit))
            {
                if (hit.so != so) s_cache.Remove(key);
                else return hit.list;
            }

            var entries = property.FindPropertyRelative("entries");
            ReorderableList list;

            if (entries == null)
            {
                list = new ReorderableList(new List<GVEntry>(), typeof(GVEntry),
                                           false, false, false, false);
            }
            else
            {
                list = new ReorderableList(so, entries, true, false, true, true);
                list.showDefaultBackground = false;

                list.onRemoveCallback = l =>
                {
                    ReorderableList.defaultBehaviours.DoRemoveButton(l);
                    entries.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    if (entries.serializedObject.targetObject is GroupValues gv)
                        gv.RebuildCache();
                };

                list.drawElementBackgroundCallback = (rect, idx, active, focused) =>
                {
                    if (Event.current.type != EventType.Repaint) return;
                    Color bg = focused || active ? GVEditorStyles.C_Selected
                             : idx % 2 == 0 ? GVEditorStyles.C_RowEven
                                                : GVEditorStyles.C_RowOdd;
                    EditorGUI.DrawRect(rect, bg);
                };

                list.drawElementCallback = (rect, idx, active, focused) =>
                {
                    if (idx >= entries.arraySize) return;
                    var el = entries.GetArrayElementAtIndex(idx);
                    if (el == null) return;
                    s_entryDrawer.OnGUI(
                        new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 2f),
                        el, GUIContent.none);
                };

                list.elementHeightCallback = idx =>
                {
                    if (idx >= entries.arraySize) return EditorGUIUtility.singleLineHeight;
                    var el = entries.GetArrayElementAtIndex(idx);
                    if (el == null) return EditorGUIUtility.singleLineHeight;
                    float ew = s_cache.TryGetValue(key, out var c) && c.elementWidth > 0f
                        ? c.elementWidth
                        : EditorGUIUtility.currentViewWidth - 40f;
                    return s_entryDrawer.GetHeight(el, ew);
                };

                list.onAddDropdownCallback = (btnRect, l) =>
                {
                    TypePickerWindow.Show(btnRect, VALUE_TYPE.INT, picked =>
                    {
                        int idx = entries.arraySize;
                        entries.InsertArrayElementAtIndex(idx);
                        var el = entries.GetArrayElementAtIndex(idx);
                        el.FindPropertyRelative("name").stringValue = "New" + picked;
                        el.FindPropertyRelative("type").enumValueIndex = (int)picked;
                        el.FindPropertyRelative("value").managedReferenceValue = GVValueFactory.Create(picked);
                        entries.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    });
                };
            }

            s_cache[key] = new ListCache { list = list, so = so, elementWidth = 0f };
            return list;
        }
    }
    #region GV_EDITOR

    /// <summary>
    /// Custom editor for the GroupValues SO when opened directly.
    /// Overrides the + button on the fields list to create empty fields.
    /// </summary>
    [CustomEditor(typeof(GroupValues))]
    internal class GroupValuesEditor : Editor
    {
        ReorderableList _fieldsList;

        void OnEnable()
        {
            _fieldsList = BuildFieldsList(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "fields", "m_Script");
            EditorGUILayout.Space(4);
            _fieldsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        internal static ReorderableList BuildFieldsList(SerializedObject so)
        {
            var fieldsProp = so.FindProperty("fields");
            var list = new ReorderableList(so, fieldsProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Fields");

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var elem = fieldsProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, elem, GUIContent.none, true);
            };

            list.elementHeightCallback = index =>
            {
                var elem = fieldsProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(elem, GUIContent.none, true);
            };

            list.onAddDropdownCallback = (btnRect, l) =>
            {
                var gv = (GroupValues)so.targetObject;
                Undo.RecordObject(gv, "Add Field");
                gv.fields.Add(new GVField { fieldName = "NewField" });
                gv.RebuildCache();
                EditorUtility.SetDirty(gv);
                so.Update();
            };

            return list;
        }
    }
}
    #endregion
#endif