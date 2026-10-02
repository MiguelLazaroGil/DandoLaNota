#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    [CustomPropertyDrawer(typeof(GVEntry))]
    internal class GVEntryDrawer : PropertyDrawer
    {
        const float Gap = 3f;
        const float Pad = 4f;
        const float LabelW = 46f;
        const float BadgeW = 56f;
        const float BadgeH = 14f;

        static float FixedH(float line) => Pad + 2f * (line + Gap);

        // ── Height ────────────────────────────────────────────────────────
        public float GetHeight(SerializedProperty prop, float elementWidth)
        {
            var typeProp = prop.FindPropertyRelative("type");
            var valueProp = prop.FindPropertyRelative("value");
            float line = EditorGUIUtility.singleLineHeight;
            var type = (VALUE_TYPE)typeProp.enumValueIndex;
            float valW = Mathf.Max(elementWidth - 10f - LabelW - Gap, 60f);

            if (valueProp?.managedReferenceValue == null)
                return FixedH(line) + line + Gap;

            switch (type)
            {
                case VALUE_TYPE.STRING:
                    {
                        var inner = valueProp.FindPropertyRelative("value");
                        float sh = GVEditorStyles.StyleTextArea().CalcHeight(
                                        new GUIContent(inner?.stringValue ?? ""), valW);
                        return FixedH(line) + sh + Gap;
                    }
                case VALUE_TYPE.VECTOR2:
                case VALUE_TYPE.VECTOR3:
                    return FixedH(line) + line * 2f + Gap;
                case VALUE_TYPE.CUSTOM:
                    {
                        var cp = prop.FindPropertyRelative("customTypeName");
                        return FixedH(line) + CustomFieldsHeight(cp?.stringValue ?? "", valueProp) + Gap;
                    }
                default:
                    return FixedH(line) + line + Gap;
            }
        }

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
            => GetHeight(prop, EditorGUIUtility.currentViewWidth - 20f);

        // ── OnGUI ─────────────────────────────────────────────────────────
        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            var nameProp = prop.FindPropertyRelative("name");
            var typeProp = prop.FindPropertyRelative("type");
            var valueProp = prop.FindPropertyRelative("value");
            var customProp = prop.FindPropertyRelative("customTypeName");

            var type = (VALUE_TYPE)typeProp.enumValueIndex;
            Color tCol = GVEditorStyles.GetTypeColor(type);

            GVEditorStyles.DrawAccentBar(pos, tCol);

            float x = pos.x + 8f;
            float w = pos.width - 10f;
            float y = pos.y + Pad;

            // ── Row 1: NAME ───────────────────────────────────────────────
            EditorGUI.LabelField(new Rect(x, y, LabelW, line), "NAME",
                GVEditorStyles.StyleSmallLabel(new Color(0.55f, 0.60f, 0.70f)));

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(x + LabelW + Gap, y, w - LabelW - Gap, line),
                nameProp, GUIContent.none);
            bool nameChanged = EditorGUI.EndChangeCheck();
            y += line + Gap;

            // Duplicate key warning
            string currentName = nameProp.stringValue;
            if (!string.IsNullOrEmpty(currentName))
            {
                var targetGV = prop.serializedObject.targetObject as GroupValues;
                if (targetGV != null)
                {
                    int myFI = ExtractLastIndex(prop.propertyPath, "fields.Array.data[");
                    int myEI = ExtractLastIndex(prop.propertyPath, "entries.Array.data[");
                    if (CountKeyOccurrences(currentName, targetGV, myFI, myEI) > 0)
                    {
                        DrawDuplicateKeyWarning(new Rect(x, y, w, line), currentName);
                        y += line + Gap;
                    }
                    if (nameChanged) targetGV.RebuildCache();
                }
            }

            // ── Row 2: TYPE badge ─────────────────────────────────────────
            Rect badgeR = new Rect(x, y + (line - BadgeH) * 0.5f, BadgeW, BadgeH);
            GVEditorStyles.DrawBadge(badgeR, type.ToString(), tCol);

            if (GUI.Button(badgeR, GUIContent.none, GUIStyle.none))
            {
                var capType = typeProp.Copy();
                var capVal = valueProp.Copy();
                var capProp = prop.Copy();
                TypePickerWindow.Show(badgeR, type, picked =>
                {
                    capType.enumValueIndex = (int)picked;
                    capVal.managedReferenceValue = GVValueFactory.Create(picked);
                    capProp.serializedObject.ApplyModifiedProperties();
                });
            }

            if (type == VALUE_TYPE.CUSTOM && customProp != null)
                DrawCustomClassPicker(
                    new Rect(x + BadgeW + Gap, y, w - BadgeW - Gap, line),
                    customProp, valueProp, prop);
            y += line + Gap;

            // ── Row 3+: VALUE ─────────────────────────────────────────────
            if (valueProp?.managedReferenceValue == null) return;
            var inner = valueProp.FindPropertyRelative("value");
            if (inner == null) return;

            EditorGUI.LabelField(new Rect(x, y, LabelW, line), "VALUE",
                GVEditorStyles.StyleSmallLabel(new Color(0.55f, 0.60f, 0.70f)));

            float valX = x + LabelW + Gap;
            float valW = w - LabelW - Gap;
            Rect valR = new Rect(valX, y, valW, line);

            switch (type)
            {
                case VALUE_TYPE.BOOL:
                    CC(() => inner.boolValue = EditorGUI.Toggle(valR, inner.boolValue)); break;
                case VALUE_TYPE.INT:
                    CC(() => inner.intValue = EditorGUI.IntField(valR, inner.intValue)); break;
                case VALUE_TYPE.FLOAT:
                    CC(() => inner.floatValue = EditorGUI.FloatField(valR, inner.floatValue)); break;
                case VALUE_TYPE.DOUBLE:
                    CC(() => inner.doubleValue = EditorGUI.DoubleField(valR, inner.doubleValue)); break;
                case VALUE_TYPE.LONG:
                    CC(() => inner.longValue = EditorGUI.LongField(valR, inner.longValue)); break;
                case VALUE_TYPE.SHORT:
                    CC(() => inner.intValue = (short)EditorGUI.IntField(valR, inner.intValue)); break;
                case VALUE_TYPE.BYTE:
                    CC(() => inner.intValue = (byte)EditorGUI.IntField(valR, inner.intValue)); break;
                case VALUE_TYPE.CHAR:
                    {
                        char curChar = (char)inner.intValue;
                        string curStr = curChar == 0 ? "" : curChar.ToString();
                        EditorGUI.BeginChangeCheck();
                        string next = EditorGUI.TextField(valR, curStr);
                        if (EditorGUI.EndChangeCheck())
                            inner.intValue = next.Length > 0 ? (int)next[0] : 0;
                        break;
                    }
                case VALUE_TYPE.STRING:
                    {
                        string txt = inner.stringValue ?? "";
                        valR.height = GVEditorStyles.StyleTextArea().CalcHeight(
                                          new GUIContent(txt), valW);
                        EditorGUI.BeginChangeCheck();
                        string s = EditorGUI.TextArea(valR, txt, GVEditorStyles.StyleTextArea());
                        if (EditorGUI.EndChangeCheck()) inner.stringValue = s;
                        break;
                    }
                case VALUE_TYPE.VECTOR2:
                case VALUE_TYPE.VECTOR3:
                    valR.height = line * 2f;
                    EditorGUI.PropertyField(valR, inner, GUIContent.none, true);
                    break;
                case VALUE_TYPE.CUSTOM:
                    DrawCustomFields(new Rect(valX, y, valW, pos.yMax - y),
                                     customProp?.stringValue ?? "", valueProp, prop);
                    break;
            }
        }

        // ── Custom class picker ───────────────────────────────────────────
        void DrawCustomClassPicker(Rect rect, SerializedProperty customProp,
                                   SerializedProperty valueProp, SerializedProperty rootProp)
        {
            string current = customProp.stringValue ?? "";
            Color col = CustomGVDataRegistry.Entries.TryGetValue(current, out var e)
                             ? e.Color : new Color(0.5f, 0.5f, 0.5f);
            string lbl = string.IsNullOrEmpty(current) ? "— none —" : current;

            GVEditorStyles.DrawBadge(rect, lbl, col);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                var capC = customProp.Copy();
                var capV = valueProp.Copy();
                var capR = rootProp.Copy();
                CustomDataPickerWindow.Show(rect, current, picked =>
                {
                    capC.stringValue = picked;
                    if (picked.Length > 0 &&
                        CustomGVDataRegistry.Types.TryGetValue(picked, out var t))
                    {
                        string json = UnityEngine.JsonUtility.ToJson(Activator.CreateInstance(t));
                        var iv = capV.FindPropertyRelative("value");
                        if (iv != null) iv.stringValue = json;
                    }
                    capR.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        // ── Custom fields ─────────────────────────────────────────────────
        void DrawCustomFields(Rect area, string typeName,
                              SerializedProperty valueProp, SerializedProperty rootProp)
        {
            if (string.IsNullOrEmpty(typeName)) return;
            if (!CustomGVDataRegistry.Types.TryGetValue(typeName, out var type)) return;
            var innerProp = valueProp.FindPropertyRelative("value");
            if (innerProp == null) return;

            var fromJson = typeof(UnityEngine.JsonUtility)
                .GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static,
                           null, new[] { typeof(string) }, null)
                ?.MakeGenericMethod(type);

            object inst;
            try
            {
                inst = fromJson?.Invoke(null, new object[] { innerProp.stringValue ?? "{}" })
                            ?? Activator.CreateInstance(type);
            }
            catch { inst = Activator.CreateInstance(type); }

            // Apply load-time attributes: reset DontSave, clamp values
            if (inst != null)
            {
                foreach (var fi in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (GVFieldAttributeHelper.IsDontSave(fi))
                    {
                        var def = fi.FieldType.IsValueType
                            ? Activator.CreateInstance(fi.FieldType) : null;
                        fi.SetValue(inst, def);
                    }
                    else
                    {
                        object v = fi.GetValue(inst);
                        object clamped = GVFieldAttributeHelper.ClampValue(fi, v);
                        if (clamped != v) fi.SetValue(inst, clamped);
                    }
                }
            }

            float y = area.y;
            bool dirty = false;

            foreach (var f in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if ((!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) ||
                      f.GetCustomAttribute<NonSerializedAttribute>() != null) continue;

                // [DontSave] — skip
                if (GVFieldAttributeHelper.IsDontSave(f)) continue;

                // Skip WriteN counter fields
                if (f.Name.StartsWith("__") && f.Name.EndsWith("WriteCount")) continue;

                bool isReadOnly = GVFieldAttributeHelper.IsReadOnly(f);
                bool isWriteOnce = GVFieldAttributeHelper.IsWriteOnce(f);
                int maxW = GVFieldAttributeHelper.GetMaxWrites(f);
                int remaining = maxW >= 0
                    ? GVFieldAttributeHelper.GetRemainingWrites(f, inst) : -1;

                string displayName = GVFieldAttributeHelper.GetJsonKey(f);
                object oldV = f.GetValue(inst);
                float fh = GVEditorStyles.ReflectedFieldHeightWithInstance(f.FieldType, oldV);

                var t = GVThemeManager.Current;
                const float resetW = 18f;
                const float badgeW = 28f;
                const float pad = 4f;

                float curX = area.x;

                // 🔄 reset button for WriteN and WriteOnce
                if (maxW >= 0 || isWriteOnce)
                {
                    Rect resetR = new Rect(curX, y, resetW, EditorGUIUtility.singleLineHeight);
                    bool exhausted = (maxW >= 0 && remaining == 0) ||
                                     (isWriteOnce && !GVFieldAttributeHelper.CanWrite(f, inst));
                    GUI.backgroundColor = exhausted ? t.buttonDanger : t.buttonNeutral;
                    var resetStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 9,
                        fontStyle = UnityEngine.FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                    };
                    resetStyle.normal.textColor = Color.white;
                    resetStyle.active.textColor = Color.white;
                    resetStyle.focused.textColor = Color.white;
                    resetStyle.hover.textColor = Color.white;
                    // Draw button border manually
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(resetR, GVThemeManager.Current.separator);
                        EditorGUI.DrawRect(new Rect(resetR.x + 1, resetR.y + 1,
                            resetR.width - 2, resetR.height - 2), GUI.backgroundColor);
                    }
                    if (GUI.Button(resetR, "R", resetStyle))
                    {
                        GVFieldAttributeHelper.ResetWriteCount(f, inst);
                        if (isWriteOnce)
                        {
                            var def = f.FieldType.IsValueType
                                ? System.Activator.CreateInstance(f.FieldType) : null;
                            f.SetValue(inst, def);
                        }
                        dirty = true;
                    }
                    GUI.backgroundColor = Color.white;
                    curX += resetW + pad;
                }

                // Badge
                Rect fieldBadgeR = new Rect(area.xMax - badgeW, y, badgeW,
                                        EditorGUIUtility.singleLineHeight - 2f);
                float fieldW = area.xMax - curX - badgeW - pad;
                Rect fieldR = new Rect(curX, y, fieldW, fh);

                if (isReadOnly)
                    GVEditorStyles.DrawBadge(fieldBadgeR, "🔒", t.textDim);
                else if (isWriteOnce)
                {
                    Color bc = GVFieldAttributeHelper.CanWrite(f, inst) ? t.valid : t.invalid;
                    GVEditorStyles.DrawBadge(fieldBadgeR, "✎¹", bc);
                }
                else if (maxW >= 0)
                {
                    Color bc = remaining > 0 ? t.valid : t.invalid;
                    GVEditorStyles.DrawBadge(fieldBadgeR, $"✎{remaining}", bc);
                }

                // Range label above the field name
                string rangeLabel = GVFieldAttributeHelper.GetRangeLabel(f);
                if (!string.IsNullOrEmpty(rangeLabel))
                {
                    var rangeLblStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize = 8,
                        alignment = TextAnchor.MiddleLeft,
                    };
                    rangeLblStyle.normal.textColor = GVThemeManager.Current.textDim;
                    // Show range label prepended to display name
                    displayName = $"{rangeLabel}  {displayName}";
                }

                EditorGUI.BeginDisabledGroup(isReadOnly);
                object newV = GVEditorStyles.DrawReflectedField(fieldR, displayName, f.FieldType, oldV);
                EditorGUI.EndDisabledGroup();

                bool isList = f.FieldType.IsGenericType &&
                              f.FieldType.GetGenericTypeDefinition() == typeof(List<>);
                if (!isReadOnly && newV != null && (isList || !newV.Equals(oldV)) &&
                    Event.current.type != EventType.Layout &&
                    Event.current.type != EventType.Repaint)
                {
                    newV = GVFieldAttributeHelper.ClampValue(f, newV);
                    if (GVFieldAttributeHelper.CanWrite(f, inst))
                    {
                        f.SetValue(inst, newV);
                        GVFieldAttributeHelper.IncrementWriteCount(f, inst);
                        dirty = true;
                    }
                }
                y += fh + GVEditorStyles.ReflGap;
            }

            if (dirty)
            {
                innerProp.stringValue = UnityEngine.JsonUtility.ToJson(inst);
                rootProp.serializedObject.ApplyModifiedProperties();
            }
        }

        // ── Custom fields height ──────────────────────────────────────────
        static float CustomFieldsHeight(string typeName, SerializedProperty valueProp)
        {
            if (string.IsNullOrEmpty(typeName)) return EditorGUIUtility.singleLineHeight;
            if (!CustomGVDataRegistry.Types.TryGetValue(typeName, out var type))
                return EditorGUIUtility.singleLineHeight;

            // Always deserialize the actual instance so List<T> element counts are
            // accurate for the CURRENT frame — not the previous frame's cached height.
            object inst = null;
            var iv = valueProp?.FindPropertyRelative("value");
            if (iv != null)
            {
                var fromJson = typeof(UnityEngine.JsonUtility)
                    .GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static,
                               null, new[] { typeof(string) }, null)
                    ?.MakeGenericMethod(type);
                try { inst = fromJson?.Invoke(null, new object[] { iv.stringValue ?? "{}" }); }
                catch { }
            }
            inst ??= Activator.CreateInstance(type);

            // Use ReflectedFieldsHeight with instance so every List<T> field
            // reports its actual count, not 0.
            return GVEditorStyles.ReflectedFieldsHeight(type, inst);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static void DrawDuplicateKeyWarning(Rect rect, string key)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.55f, 0.10f, 0.10f, 0.35f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height),
                                   new Color(0.95f, 0.20f, 0.20f));
            }
            var style = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1.0f, 0.55f, 0.55f);
            GUI.Label(new Rect(rect.x + 6, rect.y, rect.width - 6, rect.height),
                      $"⚠  Key '{key}' already exists in this GroupValues.", style);
        }

        static int CountKeyOccurrences(string key, GroupValues gv, int myFI, int myEI)
        {
            int count = 0;
            for (int fi = 0; fi < gv.fields.Count; fi++)
            {
                var entries = gv.fields[fi].entries;
                for (int ei = 0; ei < entries.Count; ei++)
                {
                    if (fi == myFI && ei == myEI) continue;
                    if (entries[ei].name == key) count++;
                }
            }
            return count;
        }

        static int ExtractLastIndex(string path, string marker)
        {
            int start = path.LastIndexOf(marker);
            if (start < 0) return -1;
            start += marker.Length;
            int end = path.IndexOf(']', start);
            if (end < 0) return -1;
            return int.TryParse(path.Substring(start, end - start), out int idx) ? idx : -1;
        }

        static void CC(Action a) { EditorGUI.BeginChangeCheck(); a(); EditorGUI.EndChangeCheck(); }
    }
}
#endif