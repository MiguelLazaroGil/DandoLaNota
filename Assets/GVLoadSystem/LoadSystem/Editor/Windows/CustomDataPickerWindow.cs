#if UNITY_EDITOR
using System;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Dropdown picker for custom data types registered via [CustomSettingData].
    /// Mirrors the look of TypePickerWindow using each type's registered color.
    /// </summary>
    public class CustomDataPickerWindow : EditorWindow
    {
        const float ItemH = 28f;
        const float ItemGap = 4f;
        const float Pad = 8f;

        string _current;
        Action<string> _onPick;

        static GUIStyle s_labelStyle;

        static GUIStyle LabelStyle(Color c)
        {
            s_labelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
            };
            s_labelStyle.normal.textColor = c;
            return s_labelStyle;
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <param name="activatorRect">Rect in GUI space that anchors the window.</param>
        /// <param name="current">Currently selected type name (empty = none).</param>
        /// <param name="onPick">Callback — receives the chosen type name, or "" for none.</param>
        public static void Show(Rect activatorRect, string current, Action<string> onPick)
        {
            var win = CreateInstance<CustomDataPickerWindow>();
            win._current = current;
            win._onPick = onPick;

            var entries = CustomGVDataRegistry.Entries;
            // +1 for the "— none —" row
            float h = (entries.Count + 1) * (ItemH + ItemGap) + Pad * 2;
            float w = 200f;

            win.ShowAsDropDown(GUIUtility.GUIToScreenRect(activatorRect),
                               new Vector2(w, h));
        }

        // ── GUI ───────────────────────────────────────────────────────────
        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                               new Color(0.13f, 0.15f, 0.19f));

            float y = Pad;

            // "— none —" row
            DrawItem(new Rect(Pad, y, position.width - Pad * 2, ItemH),
                     "— none —", new Color(0.5f, 0.5f, 0.5f), _current == "");
            if (Event.current.type == EventType.MouseDown &&
                new Rect(Pad, y, position.width - Pad * 2, ItemH)
                    .Contains(Event.current.mousePosition))
            {
                _onPick?.Invoke("");
                Close();
                Event.current.Use();
            }
            y += ItemH + ItemGap;

            foreach (var kv in CustomGVDataRegistry.Entries)
            {
                Rect itemRect = new Rect(Pad, y, position.width - Pad * 2, ItemH);
                DrawItem(itemRect, kv.Value.Name, kv.Value.Color, kv.Key == _current);

                if (Event.current.type == EventType.MouseDown &&
                    itemRect.Contains(Event.current.mousePosition))
                {
                    _onPick?.Invoke(kv.Key);
                    Close();
                    Event.current.Use();
                }

                y += ItemH + ItemGap;
            }
        }

        void DrawItem(Rect rect, string label, Color col, bool selected)
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
                      label, LabelStyle(col));
        }

        void OnInspectorUpdate() => Repaint();
    }
}
#endif