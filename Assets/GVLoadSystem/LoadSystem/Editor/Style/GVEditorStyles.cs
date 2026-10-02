#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{
    internal static class GVEditorStyles
    {
        // ── Theme shortcut ────────────────────────────────────────────────
        static GVTheme T => GVThemeManager.Current;

        // ── Structural colors (delegate to theme) ────────────────────────
        public static Color C_Header => T.backgroundPanel;
        public static Color C_HeaderBdr => T.accent;
        public static Color C_Body => T.backgroundDeep;
        public static Color C_RowEven => T.backgroundRow0;
        public static Color C_RowOdd => T.backgroundRow1;
        public static Color C_Selected => T.selected;
        public static Color C_Border => T.separator;

        public const float ReflGap = 3f;

        // ── Type colors (delegate to theme) ──────────────────────────────
        public static Color GetTypeColor(VALUE_TYPE type) => T.GetTypeColor(type);

        // ── Styles — rebuilt when theme changes ──────────────────────────
        // We track the last theme used to invalidate cached styles on theme switch
        static GVTheme s_lastTheme;
        static GUIStyle s_badgeStyle;
        static GUIStyle s_smallLabel;
        static GUIStyle s_iconStyle;
        static GUIStyle s_textArea;
        static GUIStyle s_sectionHeader;
        static GUIStyle s_toolbarBtn;
        static GUIStyle s_deleteBtn;
        static GUIStyle s_utilBtn;
        static GUIStyle s_addBtn;
        static GUIStyle s_popupLabel;
        static GUIStyle s_toggle;

        static void InvalidateIfThemeChanged()
        {
            var current = GVThemeManager.Current;
            if (current == s_lastTheme) return;
            s_lastTheme = current;
            s_badgeStyle = null;
            s_smallLabel = null;
            s_iconStyle = null;
            s_textArea = null;
            s_sectionHeader = null;
            s_toolbarBtn = null;
            s_deleteBtn = null;
            s_utilBtn = null;
            s_addBtn = null;
            s_popupLabel = null;
            s_toggle = null;
        }

        // ── Style factories ───────────────────────────────────────────────
        public static GUIStyle StyleBadge(Color c)
        {
            InvalidateIfThemeChanged();
            s_badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            s_badgeStyle.normal.textColor = c;
            return s_badgeStyle;
        }

        public static GUIStyle StyleSmallLabel(Color c)
        {
            InvalidateIfThemeChanged();
            s_smallLabel ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = T.fontSizeSmall - 2,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            s_smallLabel.normal.textColor = c;
            return s_smallLabel;
        }

        public static GUIStyle StyleIcon(Color c)
        {
            InvalidateIfThemeChanged();
            s_iconStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = T.fontSizeSmall,
                alignment = TextAnchor.MiddleCenter,
            };
            s_iconStyle.normal.textColor = c;
            return s_iconStyle;
        }

        public static GUIStyle StyleTextArea()
        {
            InvalidateIfThemeChanged();
            if (s_textArea == null)
            {
                s_textArea = new GUIStyle(EditorStyles.textArea);
                s_textArea.wordWrap = true;
                s_textArea.padding = new RectOffset(3, 3, 2, 2);
                s_textArea.normal.textColor = T.textSecondary;
                s_textArea.fontSize = T.fontSizeBody;
            }
            return s_textArea;
        }

        public static GUIStyle StyleSectionHeader()
        {
            InvalidateIfThemeChanged();
            if (s_sectionHeader == null)
            {
                s_sectionHeader = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = T.fontSizeSmall,
                    fontStyle = FontStyle.Bold,
                };
                s_sectionHeader.normal.textColor = T.accent;
            }
            return s_sectionHeader;
        }

        public static GUIStyle StyleToolbarButton()
        {
            InvalidateIfThemeChanged();
            if (s_toolbarBtn == null)
            {
                s_toolbarBtn = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = T.fontSizeBody,
                    padding = new RectOffset(8, 8, 4, 4),
                    alignment = TextAnchor.MiddleCenter,
                };
                s_toolbarBtn.normal.textColor = T.textPrimary;
                s_toolbarBtn.hover.textColor = Color.white;
                s_toolbarBtn.active.textColor = Color.white;
                s_toolbarBtn.onNormal.textColor = Color.white;
                s_toolbarBtn.onHover.textColor = Color.white;
                s_toolbarBtn.normal.background = MakeTex(T.buttonNeutral);
                s_toolbarBtn.hover.background = MakeTex(T.buttonNeutral);
                s_toolbarBtn.active.background = MakeTex(T.buttonPrimary);
                s_toolbarBtn.onNormal.background = MakeTex(T.buttonPrimary);
                s_toolbarBtn.onHover.background = MakeTex(T.buttonPrimary);
            }
            return s_toolbarBtn;
        }

        public static GUIStyle StyleDeleteButton()
        {
            InvalidateIfThemeChanged();
            if (s_deleteBtn == null)
            {
                s_deleteBtn = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = T.fontSizeSmall,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 2),
                };
                s_deleteBtn.normal.textColor = T.invalid;
            }
            return s_deleteBtn;
        }

        public static GUIStyle StyleUtilButton()
        {
            InvalidateIfThemeChanged();
            if (s_utilBtn == null)
            {
                s_utilBtn = new GUIStyle(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = T.fontSizeSmall,
                    padding = new RectOffset(8, 8, 4, 4),
                    alignment = TextAnchor.MiddleCenter,
                };
                s_utilBtn.normal.textColor = T.textPrimary;
                s_utilBtn.hover.textColor = Color.white;
            }
            return s_utilBtn;
        }

        public static GUIStyle StyleAddButton()
        {
            InvalidateIfThemeChanged();
            if (s_addBtn == null)
            {
                s_addBtn = new GUIStyle(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = T.fontSizeSmall,
                    padding = new RectOffset(10, 10, 4, 4),
                    alignment = TextAnchor.MiddleCenter,
                };
                s_addBtn.normal.textColor =
                s_addBtn.hover.textColor =
                s_addBtn.active.textColor = Color.white;
            }
            return s_addBtn;
        }

        public static GUIStyle StylePopupLabel()
        {
            InvalidateIfThemeChanged();
            if (s_popupLabel == null)
            {
                s_popupLabel = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = T.fontSizeSmall,
                    alignment = TextAnchor.MiddleLeft,
                };
                s_popupLabel.normal.textColor = T.textSecondary;
            }
            return s_popupLabel;
        }

        public static GUIStyle StyleToggleLabel()
        {
            InvalidateIfThemeChanged();
            if (s_toggle == null)
            {
                s_toggle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = T.fontSizeSmall,
                };
                s_toggle.normal.textColor = T.textSecondary;
            }
            return s_toggle;
        }

        // ── Draw helpers ──────────────────────────────────────────────────
        public static void DrawBox(Rect rect, Color fill, Color border)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1,
                                        rect.width - 2, rect.height - 2), fill);
        }

        public static void DrawAccentBar(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), color);
        }

        public static void DrawBadge(Rect rect, string text, Color color)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = new Color(color.r * 0.25f, color.g * 0.25f,
                                     color.b * 0.25f, 0.85f);
                EditorGUI.DrawRect(rect, bg);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
            }
            GUI.Label(rect, text, StyleBadge(color));
        }

        public static void DrawIcon(Rect rect, string icon, Color color)
            => GUI.Label(rect, icon, StyleIcon(color));

        public static void DrawSeparator(float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(x, y, width, 1), T.separator);
        }

        public static bool ColorButton(string label, Color bg, GUILayoutOption opt = null)
        {
            GUI.backgroundColor = bg;
            bool pressed = opt != null
                ? GUILayout.Button(label, StyleToolbarButton(), opt)
                : GUILayout.Button(label, StyleToolbarButton());
            GUI.backgroundColor = Color.white;
            return pressed;
        }

        public static void DrawRowBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(rect, T.backgroundPanel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), T.separator);
        }

        public static int StyledPopup(string label, int selected,
                                      string[] options, float totalWidth)
        {
            float lw = 110f;
            float pw = totalWidth - lw - 8f;
            Rect row = EditorGUILayout.GetControlRect(false, T.rowHeight - 2f);
            DrawRowBackground(row);
            EditorGUI.LabelField(new Rect(row.x + 6f, row.y + 2f, lw, row.height),
                                 label, StylePopupLabel());
            return EditorGUI.Popup(
                new Rect(row.x + 6f + lw, row.y + 2f, pw, row.height - 4f),
                selected, options);
        }

        public static bool StyledToggle(string label, bool value, float totalWidth)
        {
            Rect row = EditorGUILayout.GetControlRect(false, T.rowHeight - 2f);
            DrawRowBackground(row);
            Rect chk = new Rect(row.x + 6f, row.y + 3f, 14f, 14f);
            Rect lbl = new Rect(chk.xMax + 4f, row.y + 2f,
                                totalWidth - chk.xMax - 10f, row.height);
            bool result = EditorGUI.Toggle(chk, value);
            EditorGUI.LabelField(lbl, label, StyleToggleLabel());
            return result;
        }

        public static void DrawWindowSectionHeader(string title)
        {
            Rect r = EditorGUILayout.GetControlRect(false, T.headerHeight - 6f);
            DrawBox(r, T.backgroundPanel, T.accent);
            DrawAccentBar(r, T.accent);
            GUI.Label(new Rect(r.x + 8, r.y + 3, r.width - 8, r.height),
                      title, StyleSectionHeader());
        }
        public static string NicifyFieldName(string name)
            => GroupValuesProjectSettings.instance.nicifyFieldNames
                ? ObjectNames.NicifyVariableName(name)
                : name;
        // ── Texture helper ────────────────────────────────────────────────
        public static Texture2D MakeTexPublic(Color col) => MakeTex(col);
        static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        // ════════════════════════════════════════════════════════════════
        // REFLECTED FIELD SYSTEM
        // ════════════════════════════════════════════════════════════════
        const float ReflLabelW = 100f;

        public static float ReflectedFieldHeight(Type ft)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (ft == typeof(Vector2) || ft == typeof(Quaternion)) return line * 2f;
            if (ft == typeof(Vector3) || ft == typeof(Vector4)) return line * 2f;
            if (ft == typeof(Rect) || ft == typeof(Bounds)) return line * 3f;
            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                return line * 2f;
            if (ft.IsSerializable && !ft.IsPrimitive &&
                !typeof(UnityEngine.Object).IsAssignableFrom(ft) &&
                ft != typeof(string))
            {
                float h = line + ReflGap;
                foreach (var f in ft.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if ((!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) ||
                          f.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                    if (GVFieldAttributeHelper.IsDontSave(f)) continue;
                    if (GVFieldAttributeHelper.IsHide(f)) continue;
                    if (f.Name.StartsWith("__") && f.Name.EndsWith("WriteCount")) continue;
                    h += ReflectedFieldHeight(f.FieldType) + ReflGap;
                }
                return Mathf.Max(line, h);
            }
            return line;
        }

        public static float ReflectedFieldsHeight(Type type, object instance)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float total = 0f;
            foreach (var f in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if ((!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) ||
                      f.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                if (GVFieldAttributeHelper.IsHide(f)) continue;
                total += ReflectedFieldHeightWithInstance(f.FieldType,
                             instance != null ? f.GetValue(instance) : null) + ReflGap;
            }
            return Mathf.Max(line, total);
        }

        public static float ReflectedFieldHeightWithInstance(Type ft, object value)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = value as IList;
                int count = list?.Count ?? 0;
                Type elemType = ft.GetGenericArguments()[0];
                float h = line + ReflGap;
                for (int i = 0; i < count; i++)
                    h += ReflectedFieldHeightWithInstance(elemType, list[i]) + ReflGap;
                return h;
            }
            if (ft.IsSerializable && !ft.IsPrimitive &&
                !typeof(UnityEngine.Object).IsAssignableFrom(ft) &&
                ft != typeof(string) &&
                !(ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>)))
            {
                if (value != null)
                    return EditorGUIUtility.singleLineHeight + ReflGap +
                           ReflectedFieldsHeight(ft, value);
                return ReflectedFieldHeight(ft);
            }
            return ReflectedFieldHeight(ft);
        }

        public static object DrawReflectedField(Rect rect, string name, Type ft, object cur)
        {
            float line = EditorGUIUtility.singleLineHeight;
            Rect lR = new Rect(rect.x, rect.y, ReflLabelW, line);
            Rect vR = new Rect(rect.x + ReflLabelW + ReflGap, rect.y,
                                  rect.width - ReflLabelW - ReflGap, line);

            EditorGUI.LabelField(lR, name, StyleSmallLabel(T.textDim));

            if (ft == typeof(bool)) return EditorGUI.Toggle(vR, cur is bool b ? b : false);
            if (ft == typeof(int)) return EditorGUI.IntField(vR, cur is int i ? i : 0);
            if (ft == typeof(float)) return EditorGUI.FloatField(vR, cur is float f ? f : 0f);
            if (ft == typeof(double)) return EditorGUI.DoubleField(vR, cur is double d ? d : 0.0);
            if (ft == typeof(long)) return EditorGUI.LongField(vR, cur is long l ? l : 0L);
            if (ft == typeof(string)) return EditorGUI.TextField(vR, cur as string ?? "");
            if (ft == typeof(char))
            {
                string cur_s = cur is char c ? c.ToString() : "";
                EditorGUI.BeginChangeCheck();
                string next = EditorGUI.TextField(vR, cur_s);
                if (EditorGUI.EndChangeCheck() && next.Length > 0) return next[0];
                return cur;
            }
            if (ft == typeof(short)) return (short)Mathf.Clamp(EditorGUI.IntField(vR, cur is short sh ? sh : (short)0), short.MinValue, short.MaxValue);
            if (ft == typeof(byte)) return (byte)Mathf.Clamp(EditorGUI.IntField(vR, cur is byte by ? by : (byte)0), 0, 255);

            if (ft == typeof(Color))
                return EditorGUI.ColorField(vR, cur is Color c ? c : Color.white);
            if (ft == typeof(Color32))
                return (Color32)EditorGUI.ColorField(vR,
                    cur is Color32 c32 ? (Color)c32 : Color.white);
            if (ft == typeof(Vector2))
            { vR.height = line * 2f; return EditorGUI.Vector2Field(vR, GUIContent.none, cur is Vector2 v2 ? v2 : default); }
            if (ft == typeof(Vector3))
            { vR.height = line * 2f; return EditorGUI.Vector3Field(vR, GUIContent.none, cur is Vector3 v3 ? v3 : default); }
            if (ft == typeof(Vector4))
            { vR.height = line * 2f; return EditorGUI.Vector4Field(vR, GUIContent.none, cur is Vector4 v4 ? v4 : default); }
            if (ft == typeof(Quaternion))
            {
                vR.height = line * 2f;
                Vector3 euler = cur is Quaternion q ? q.eulerAngles : Vector3.zero;
                EditorGUI.BeginChangeCheck();
                Vector3 newE = EditorGUI.Vector3Field(vR, GUIContent.none, euler);
                if (EditorGUI.EndChangeCheck()) return Quaternion.Euler(newE);
                return cur;
            }
            if (ft == typeof(Rect))
            { vR.height = line * 3f; return EditorGUI.RectField(vR, cur is Rect r ? r : default); }
            if (ft == typeof(Bounds))
            { vR.height = line * 3f; return EditorGUI.BoundsField(vR, cur is Bounds bo ? bo : default); }
            if (ft == typeof(AnimationCurve))
                return EditorGUI.CurveField(vR, cur as AnimationCurve ?? new AnimationCurve());

            if (ft.IsEnum)
                return EditorGUI.EnumPopup(vR, cur as Enum ?? (Enum)Activator.CreateInstance(ft));

            if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
            {
                EditorGUI.LabelField(vR, $"({ft.Name})", StyleSmallLabel(T.textDim));
                return cur;
            }

            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elemType = ft.GetGenericArguments()[0];
                IList list = cur as IList ?? (IList)Activator.CreateInstance(ft);
                float y = rect.y;

                // Color del tipo que contiene este campo lista (pasado desde la rama serializable)
                // Si se llama desde el nivel raíz no hay padre, usamos elemType como fallback.
                Color listCol = GVTypeColorResolver.GetColor(elemType);

                Rect headerR = new Rect(rect.x, y, rect.width, line);
                EditorGUI.DrawRect(headerR, T.backgroundPanel);
                EditorGUI.DrawRect(headerR, new Color(listCol.r, listCol.g, listCol.b, 0.10f));
                EditorGUI.DrawRect(new Rect(rect.x, y, 2, line), listCol);
                EditorGUI.LabelField(new Rect(rect.x + 2, y, ReflLabelW, line),
                    $"{name}  [{list.Count}]", StyleSmallLabel(T.textPrimary));

                GUI.backgroundColor = new Color(
                    Mathf.Clamp01(listCol.r * 0.6f + T.buttonPrimary.r * 0.4f),
                    Mathf.Clamp01(listCol.g * 0.6f + T.buttonPrimary.g * 0.4f),
                    Mathf.Clamp01(listCol.b * 0.6f + T.buttonPrimary.b * 0.4f));
                if (GUI.Button(new Rect(rect.xMax - 50f, y, 48f, line),
                               "+ Add", EditorStyles.miniButton))
                {
                    object def = elemType.IsValueType
                    ? Activator.CreateInstance(elemType)
                    : elemType == typeof(string)
                    ? (object)""
                    : Activator.CreateInstance(elemType);
                    list.Add(def);
                }
                GUI.backgroundColor = Color.white;
                y += line + ReflGap;

                int removeIdx = -1;
                Color listElemCol = GVTypeColorResolver.GetColor(elemType);
                for (int idx = 0; idx < list.Count; idx++)
                {
                    object ov = list[idx];
                    float elemH = ReflectedFieldHeightWithInstance(elemType, ov);
                    Rect elemR = new Rect(rect.x + 8f, y, rect.width - 28f, elemH);
                    Rect delR = new Rect(rect.xMax - 20f, y + (elemH - line) * 0.5f, 18f, line);

                    float rowAlpha = idx % 2 == 0 ? 0.04f : 0.11f;
                    EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, elemH),
                        new Color(listElemCol.r, listElemCol.g, listElemCol.b, rowAlpha));
                    object nv = DrawReflectedField(elemR, $"[{idx}]", elemType, ov);
                    bool elemIsList = elemType.IsGenericType &&
                                      elemType.GetGenericTypeDefinition() == typeof(List<>);
                    if (nv != null && (elemIsList || ov == null || !nv.Equals(ov))) { list[idx] = nv; }
                    GUI.backgroundColor = T.buttonDanger;
                    if (GUI.Button(delR, "x", StyleDeleteButton())) removeIdx = idx;
                    GUI.backgroundColor = Color.white;
                    y += elemH + ReflGap;
                }
                if (removeIdx >= 0) { list.RemoveAt(removeIdx); }
                return list;
            }

            if (ft.IsSerializable && !ft.IsPrimitive)
            {
                object inst = cur ?? Activator.CreateInstance(ft);
                bool dirty = false;
                float y = rect.y;

                Color typeCol = GVTypeColorResolver.GetColor(ft);
                Rect headerR = new Rect(rect.x, y, rect.width, line);
                EditorGUI.DrawRect(headerR, T.backgroundPanel);
                EditorGUI.DrawRect(headerR, new Color(typeCol.r, typeCol.g, typeCol.b, 0.10f));
                EditorGUI.DrawRect(new Rect(rect.x, y, 2, line), typeCol);
                EditorGUI.LabelField(new Rect(rect.x + 6, y, rect.width - 6, line),
                    name, StyleSmallLabel(T.textPrimary));
                y += line + ReflGap;

                foreach (var field in ft.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if ((!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) ||
                          field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;

                    // [DontSave] — skip entirely
                    if (GVFieldAttributeHelper.IsDontSave(field)) continue;
                    if (GVFieldAttributeHelper.IsHide(field)) continue;

                    // Skip WriteN counter fields
                    if (field.Name.StartsWith("__") && field.Name.EndsWith("WriteCount")) continue;

                    bool readOnly = GVFieldAttributeHelper.IsReadOnly(field);
                    bool writeOnce = GVFieldAttributeHelper.IsWriteOnce(field);
                    int maxW = GVFieldAttributeHelper.GetMaxWrites(field);

                    // Display name with attribute badges
                    string displayName = GVFieldAttributeHelper.GetJsonKey(field);
                    displayName = NicifyFieldName(displayName);
                    if (readOnly) displayName += " 🔒";
                    else if (writeOnce) displayName += " ✎¹";
                    else if (maxW >= 0) displayName += $" ✎{maxW}";

                    float fh = ReflectedFieldHeightWithInstance(field.FieldType, field.GetValue(inst));
                    object ov = field.GetValue(inst);
                    object nv;

                    EditorGUI.BeginDisabledGroup(readOnly);
                    nv = DrawReflectedField(
                        new Rect(rect.x + 8f, y, rect.width - 8f, fh),
                        displayName, field.FieldType, ov);
                    EditorGUI.EndDisabledGroup();

                    if (!readOnly && nv != null && !nv.Equals(ov) &&
                        Event.current.type != EventType.Layout &&
                        Event.current.type != EventType.Repaint)
                    {
                        nv = GVFieldAttributeHelper.ClampValue(field, nv);
                        if (GVFieldAttributeHelper.CanWrite(field, inst))
                        {
                            field.SetValue(inst, nv);
                            GVFieldAttributeHelper.IncrementWriteCount(field, inst);
                            dirty = true;
                        }
                    }
                    y += fh + ReflGap;
                }
                return dirty ? inst : cur;
            }

            EditorGUI.LabelField(vR, $"({ft.Name})", StyleSmallLabel(T.textDim));
            return cur;
        }
    }
}
#endif