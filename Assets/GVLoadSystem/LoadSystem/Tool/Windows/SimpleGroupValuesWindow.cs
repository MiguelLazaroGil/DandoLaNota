#if UNITY_EDITOR
using System.IO;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Editor window for inspecting and managing SimpleGroupValues.
    /// Read-only view of all keys/values + delete options.
    /// </summary>
    internal class SimpleGroupValuesWindow : EditorWindow
    {
        Vector2 _scroll;
        string _searchQuery = "";

        static Color C_Bg => GVThemeManager.Current.backgroundDeep;
        static Color C_Panel => GVThemeManager.Current.backgroundPanel;
        static Color C_Header => GVThemeManager.Current.backgroundPanel;
        static Color C_Accent => GVThemeManager.Current.accent;
        static Color C_RowEven => GVThemeManager.Current.backgroundRow0;
        static Color C_RowOdd => GVThemeManager.Current.backgroundRow1;
        static Color C_Separator => GVThemeManager.Current.separator;
        static Color C_Value => GVThemeManager.Current.valid;
        static Color C_Warning => GVThemeManager.Current.warning;

        const float RowH = 22f;
        const float ColKey = 200f;
        const float ColType = 80f;

        GUIStyle _styleHeader;
        GUIStyle _styleKey;
        GUIStyle _styleType;
        GUIStyle _styleValue;
        GUIStyle _styleBody;
        GUIStyle _styleBtn;
        GUIStyle _styleSearch;

        [MenuItem("Tools/LoadSystem/SimpleGroupValues Viewer", priority = 12)]
        public static void Open()
            => GetWindow<SimpleGroupValuesWindow>("SGV Viewer");

        void OnFocus() => Repaint();
        void OnEnable() => Repaint();

        void OnGUI()
        {
            EnsureStyles();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), C_Bg);

            DrawToolbar();

            var inst = SimpleGroupValues.instance;
            if (inst == null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.HelpBox(
                    "No SimpleGroupValues asset found.\n" +
                    "Create one at Assets > Create > LoadSystem > SimpleGroupValues\n" +
                    "and place it in a Resources/LoadSystem/ folder.",
                    MessageType.Warning);
                return;
            }

            DrawTableHeader();
            DrawEntries(inst);
            DrawFooter(inst);
        }

        // ── Toolbar ───────────────────────────────────────────────────────
        void DrawToolbar()
        {
            Rect bar = new Rect(0, 0, position.width, 36f);
            EditorGUI.DrawRect(bar, C_Panel);
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), C_Separator);
            EditorGUI.DrawRect(new Rect(0, 0, 3, 36), C_Accent);
            GUI.Label(new Rect(12, 8, 240, 20), "SimpleGroupValues Viewer", _styleHeader);

            // Search
            float sw = 180f;
            GUI.Label(new Rect(position.width - sw - 120f, 9, 20, 18), "🔍", _styleBody);
            _searchQuery = GUI.TextField(
                new Rect(position.width - sw - 100f, 8, sw, 20),
                _searchQuery, _styleSearch);

            // Save / Load buttons
            GUI.backgroundColor = GVThemeManager.Current.buttonPrimary;
            if (GUI.Button(new Rect(position.width - sw - 270f, 6, 60f, 24f), "Save", _styleBtn))
            {
                SimpleGroupValues.SaveInternal();
                Debug.Log("[SGV] Saved.");
            }
            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUI.Button(new Rect(position.width - sw - 205f, 6, 60f, 24f), "Load", _styleBtn))
            {
                SimpleGroupValues.LoadFromFile();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Table header ──────────────────────────────────────────────────
        void DrawTableHeader()
        {
            Rect hdr = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(hdr, C_Header);
            EditorGUI.DrawRect(new Rect(hdr.x, hdr.y, 3, hdr.height), C_Accent);

            float x = hdr.x + 8f;
            GUI.Label(new Rect(x, hdr.y + 3, ColKey, 16), "Key", _styleKey);
            GUI.Label(new Rect(x + ColKey, hdr.y + 3, ColType, 16), "Type", _styleKey);
            GUI.Label(new Rect(x + ColKey + ColType, hdr.y + 3,
                               hdr.width - ColKey - ColType - 40f, 16), "Value", _styleKey);

            // Separator lines
            DrawVLine(hdr, hdr.x + 8 + ColKey);
            DrawVLine(hdr, hdr.x + 8 + ColKey + ColType);

            EditorGUI.DrawRect(new Rect(hdr.x, hdr.yMax - 1, hdr.width, 1), C_Separator);
        }

        // ── Entries ───────────────────────────────────────────────────────
        void DrawEntries(SimpleGroupValues inst)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string lowerSearch = _searchQuery.ToLower();
            int rowIdx = 0;

            for (int i = 0; i < inst.entries.Count; i++)
            {
                var e = inst.entries[i];
                if (!string.IsNullOrEmpty(_searchQuery) &&
                    !e.name.ToLower().Contains(lowerSearch) &&
                    !(e.value?.GetValue()?.ToString() ?? "").ToLower().Contains(lowerSearch))
                    continue;

                Rect row = EditorGUILayout.GetControlRect(false, RowH);
                EditorGUI.DrawRect(row, rowIdx % 2 == 0 ? C_RowEven : C_RowOdd);
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3, row.height),
                                   GVEditorStyles.GetTypeColor(e.type) * 0.7f);

                float x = row.x + 8f;
                float y = row.y + (RowH - 14f) * 0.5f;
                float valW = row.width - ColKey - ColType - 48f;

                // Key
                GUI.Label(new Rect(x, y, ColKey - 4, 14), e.name, _styleKey);
                DrawVLine(row, row.x + 8 + ColKey);

                // Type badge
                Rect badgeR = new Rect(x + ColKey + 2, row.y + 4, ColType - 8, 14);
                GVEditorStyles.DrawBadge(badgeR, e.type.ToString(),
                                         GVEditorStyles.GetTypeColor(e.type));
                DrawVLine(row, row.x + 8 + ColKey + ColType);

                // Value
                string val = e.value?.GetValue()?.ToString() ?? "null";
                if (val.Length > 80) val = val.Substring(0, 77) + "...";
                GUI.Label(new Rect(x + ColKey + ColType + 2, y, valW, 14), val, _styleValue);

                // Delete button
                GUI.backgroundColor = GVThemeManager.Current.buttonDanger;
                if (GUI.Button(new Rect(row.xMax - 22f, row.y + 2, 18f, RowH - 4),
                               "×", GVEditorStyles.StyleDeleteButton()))
                {
                    SimpleGroupValues.Delete(e.name);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;

                rowIdx++;
            }

            if (rowIdx == 0)
            {
                Rect empty = EditorGUILayout.GetControlRect(false, 32f);
                GUI.Label(new Rect(empty.x + 12, empty.y + 8, empty.width - 24, 20),
                          string.IsNullOrEmpty(_searchQuery)
                              ? "No entries yet. Use SimpleGroupValues.Set(key, value) to add one."
                              : $"No entries matching \"{_searchQuery}\".",
                          _styleBody);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Footer ────────────────────────────────────────────────────────
        void DrawFooter(SimpleGroupValues inst)
        {
            EditorGUI.DrawRect(
                new Rect(0, position.height - 28, position.width, 1), C_Separator);
            Rect footer = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(footer, C_Panel);

            int total = inst.entries.Count;
            string jsonPath = Path.Combine(Application.persistentDataPath,
                                           ".SimpleGroupValues.json");
            bool saved = File.Exists(jsonPath);

            string info = $"{total} entr{(total == 1 ? "y" : "ies")}";
            if (saved)
            {
                var fi = new System.IO.FileInfo(jsonPath);
                info += $"  |  JSON: {FormatBytes(fi.Length)}";
            }
            else
            {
                info += "  |  No JSON saved yet";
            }

            GUI.Label(new Rect(footer.x + 8, footer.y + 6, footer.width - 120f, 18),
                      info, _styleBody);

            // Delete all
            GUI.backgroundColor = GVThemeManager.Current.buttonDanger;
            if (GUI.Button(new Rect(footer.xMax - 100f, footer.y + 3, 90f, 22f),
                           "Delete All", _styleBtn))
            {
                if (EditorUtility.DisplayDialog("Delete All",
                    "Delete all SimpleGroupValues entries?", "Delete", "Cancel"))
                {
                    SimpleGroupValues.DeleteAll();
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static void DrawVLine(Rect row, float x)
            => EditorGUI.DrawRect(new Rect(x - 1, row.y, 1, row.height), C_Separator);

        static string FormatBytes(long b)
        {
            if (b < 1024) return $"{b} B";
            if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
            return $"{b / (1024.0 * 1024):F2} MB";
        }

        GVTheme _lastTheme;

        void EnsureStyles()
        {
            var t = GVThemeManager.Current;
            if (_styleHeader != null && _lastTheme == t) return;
            _lastTheme = t;

            _styleHeader = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = t.fontSizeBody };
            _styleHeader.normal.textColor = t.textPrimary;

            _styleKey = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall, fontStyle = FontStyle.Bold };
            _styleKey.normal.textColor = t.textPrimary;

            _styleType = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall - 1 };
            _styleType.normal.textColor = t.textSecondary;

            var monoFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "Lucida Console", "monospace" },
                t.fontSizeCode);
            _styleValue = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeCode, font = monoFont };
            _styleValue.normal.textColor = t.valid;

            _styleBody = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall };
            _styleBody.normal.textColor = t.textSecondary;

            _styleBtn = new GUIStyle(EditorStyles.miniButton)
            { fontSize = t.fontSizeSmall, fontStyle = FontStyle.Bold };
            _styleBtn.normal.textColor = t.textPrimary;

            _styleSearch = new GUIStyle(EditorStyles.textField)
            { fontSize = t.fontSizeSmall };
        }
    }
}
#endif