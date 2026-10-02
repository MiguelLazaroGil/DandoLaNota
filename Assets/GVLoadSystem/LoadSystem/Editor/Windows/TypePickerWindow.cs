#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using GVLoadSystem.Core;
namespace GVLoadSystem.GVEditor
{
    public class TypePickerWindow : EditorWindow
    {
        const float ItemH = 28f;
        const float ItemGap = 4f;
        const float Pad = 8f;

        VALUE_TYPE _current;
        Action<VALUE_TYPE> _onPick;

        GUIStyle _labelStyle;
        GVTheme _lastTheme;

        GUIStyle LabelStyle(Color c)
        {
            var t = GVThemeManager.Current;
            if (_labelStyle == null || _lastTheme != t)
            {
                _lastTheme = t;
                _labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = t.fontSizeSmall,
                    alignment = TextAnchor.MiddleLeft,
                };
            }
            _labelStyle.normal.textColor = c;
            return _labelStyle;
        }

        // ── API ───────────────────────────────────────────────────────────
        public static void Show(Rect activatorRect, VALUE_TYPE current,
                                Action<VALUE_TYPE> onPick)
        {
            var win = CreateInstance<TypePickerWindow>();
            win._current = current;
            win._onPick = onPick;

            var types = (VALUE_TYPE[])Enum.GetValues(typeof(VALUE_TYPE));
            float h = types.Length * (ItemH + ItemGap) + Pad * 2;

            win.ShowAsDropDown(
                GUIUtility.GUIToScreenRect(activatorRect),
                new Vector2(180f, h));
        }

        // ── GUI ───────────────────────────────────────────────────────────
        void OnGUI()
        {
            var t = GVThemeManager.Current;
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                               t.backgroundDeep);

            var types = (VALUE_TYPE[])Enum.GetValues(typeof(VALUE_TYPE));
            float y = Pad;

            foreach (var type in types)
            {
                Color col = GVEditorStyles.GetTypeColor(type);
                bool selected = type == _current;
                Rect itemRect = new Rect(Pad, y, position.width - Pad * 2, ItemH);

                DrawItem(itemRect, type, col, selected);

                if (Event.current.type == EventType.MouseDown &&
                    itemRect.Contains(Event.current.mousePosition))
                {
                    _onPick?.Invoke(type);
                    Close();
                    Event.current.Use();
                }

                y += ItemH + ItemGap;
            }
        }

        void DrawItem(Rect rect, VALUE_TYPE type, Color col, bool selected)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = selected
                    ? new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 1f)
                    : new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.18f, 1f);

                if (rect.Contains(Event.current.mousePosition))
                    bg = new Color(col.r * 0.28f, col.g * 0.28f, col.b * 0.28f, 1f);

                EditorGUI.DrawRect(rect, bg);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), col);

                if (selected)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), col);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), col);
                    EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), col);
                }
            }

            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 12f, rect.height),
                      type.ToString(), LabelStyle(col));
        }

        void OnInspectorUpdate() => Repaint();
    }
}
#endif