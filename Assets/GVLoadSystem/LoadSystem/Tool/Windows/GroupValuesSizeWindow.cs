#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{
    internal class GroupValuesSizeWindow : EditorWindow
    {
        // ── Data ──────────────────────────────────────────────────────────
        struct Row
        {
            public string label;       // asset name
            public bool isTemplate;
            public long assetBytes;
            public long jsonBytes;
            public long total => assetBytes + jsonBytes;
            public string assetPath;
            public string jsonPath;
        }

        List<Row> _rows = new();
        Vector2 _scroll;

        // Column widths
        const float ColName = 220f;
        // Numeric columns share the remaining width equally — computed at draw time
        const float RowH = 26f;
        const float HeaderH = 26f;
        const float Pad = 6f;

        // Colors from theme
        static Color C_Header => GVThemeManager.Current.backgroundPanel;
        static Color C_HeaderBdr => GVThemeManager.Current.accent;
        static Color C_RowEven => GVThemeManager.Current.backgroundRow0;
        static Color C_RowOdd => GVThemeManager.Current.backgroundRow1;
        static Color C_Template => GVThemeManager.Current.backgroundPanel;
        static Color C_TemplateTag => GVThemeManager.Current.buttonPrimary;
        static Color C_Footer => GVThemeManager.Current.backgroundPanel;
        static Color C_Separator => GVThemeManager.Current.separator;

        GUIStyle _styleHeader;
        GUIStyle _styleCell;
        GUIStyle _styleCellRight;
        GUIStyle _styleTag;
        GUIStyle _styleTotal;
        GUIStyle _styleTemplateTag;
        GUIStyle _styleHeaderRight;

        [MenuItem("Tools/LoadSystem/GroupValues Size Inspector", priority = 1)]
        public static void Open()
        {
            var w = GetWindow<GroupValuesSizeWindow>("GV Size Inspector");
            w.Refresh();
        }

        void OnEnable() => Refresh();
        void OnFocus() => Refresh();

        // ── Refresh ───────────────────────────────────────────────────────
        void Refresh()
        {
            _rows.Clear();

            // GroupValues
            foreach (var gv in GroupValuesRegistry.GetAll())
            {
                string assetPath = AssetDatabase.GetAssetPath(gv);
                string jsonPath = GetJsonPath(assetPath);
                _rows.Add(new Row
                {
                    label = gv.name,
                    isTemplate = false,
                    assetBytes = FileSize(assetPath),
                    jsonBytes = FileSize(jsonPath),
                    assetPath = assetPath,
                    jsonPath = jsonPath,
                });
            }

            // Templates
            foreach (var t in GroupValuesRegistry.GetAllTemplates())
            {
                string assetPath = AssetDatabase.GetAssetPath(t);
                _rows.Add(new Row
                {
                    label = t.name,
                    isTemplate = true,
                    assetBytes = FileSize(assetPath),
                    jsonBytes = 0,   // templates have no json
                    assetPath = assetPath,
                    jsonPath = "",
                });
            }

            Repaint();
        }

        // ── GUI ───────────────────────────────────────────────────────────
        // Returns the width for each of the 3 numeric columns based on available space
        float NumColW() => Mathf.Max(70f, (position.width - ColName) / 3f);

        void OnGUI()
        {
            EnsureStyles();

            // Background
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                               GVThemeManager.Current.backgroundDeep);

            // Toolbar
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUILayout.Button("Refresh", GUILayout.Height(24), GUILayout.Width(80)))
                Refresh();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Table header
            Rect headerRect = EditorGUILayout.GetControlRect(false, HeaderH);
            DrawTableHeader(headerRect);

            // Separator
            Rect sepRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(sepRect, C_Separator);

            // Rows
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool shownGVLabel = false;
            bool shownTemplateLabel = false;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];

                // Section label — shown once before the first GV and once before the first template
                if (!row.isTemplate && !shownGVLabel)
                {
                    shownGVLabel = true;
                    Rect tagRect = EditorGUILayout.GetControlRect(false, 14f);
                    EditorGUI.DrawRect(tagRect, C_Header);
                    EditorGUI.DrawRect(new Rect(tagRect.x, tagRect.y, 3, tagRect.height), C_HeaderBdr);
                    GUI.Label(new Rect(tagRect.x + Pad, tagRect.y, tagRect.width, tagRect.height),
                              "GROUP VALUES", _styleTag);
                }
                else if (row.isTemplate && !shownTemplateLabel)
                {
                    shownTemplateLabel = true;
                    Rect tagRect = EditorGUILayout.GetControlRect(false, 14f);
                    EditorGUI.DrawRect(tagRect, C_Template);
                    EditorGUI.DrawRect(new Rect(tagRect.x, tagRect.y, 3, tagRect.height), C_TemplateTag);
                    GUI.Label(new Rect(tagRect.x + Pad, tagRect.y, tagRect.width, tagRect.height),
                              "TEMPLATES", _styleTemplateTag);
                }

                Rect rowRect = EditorGUILayout.GetControlRect(false, RowH);
                Color bg = row.isTemplate ? C_Template
                         : i % 2 == 0 ? C_RowEven
                                          : C_RowOdd;
                EditorGUI.DrawRect(rowRect, bg);

                // Left accent
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height),
                    row.isTemplate ? C_TemplateTag : C_HeaderBdr);

                float colW = NumColW();
                float x0 = rowRect.x + 3f;
                float x1 = x0 + ColName;
                float x2 = x1 + colW;
                float x3 = x2 + colW;
                float y = rowRect.y + (RowH - EditorGUIUtility.singleLineHeight) * 0.5f;
                float h = EditorGUIUtility.singleLineHeight;

                // Vertical separators
                DrawColSeparator(rowRect, x1);
                DrawColSeparator(rowRect, x2);
                DrawColSeparator(rowRect, x3);

                // Name — clickable, selects asset
                Rect nameR = new Rect(x0 + Pad, y, ColName - Pad * 2, h);
                if (GUI.Button(nameR, row.label, _styleCell))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(row.assetPath);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                DrawCell(new Rect(x1, y, colW, h),
                         FormatBytes(row.assetBytes), _styleCellRight);
                DrawCell(new Rect(x2, y, colW, h),
                         row.isTemplate ? "—" : FormatBytes(row.jsonBytes), _styleCellRight);
                DrawCell(new Rect(x3, y, colW, h),
                         FormatBytes(row.total), _styleTotal);
            }

            EditorGUILayout.EndScrollView();

            // Footer totals
            DrawFooter();
        }

        void DrawTableHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, C_Header);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), C_HeaderBdr);

            float colW = NumColW();
            float x0 = rect.x + 3f;
            float x1 = x0 + ColName;
            float x2 = x1 + colW;
            float x3 = x2 + colW;
            float y = rect.y + (HeaderH - EditorGUIUtility.singleLineHeight) * 0.5f;
            float h = EditorGUIUtility.singleLineHeight;

            GUI.Label(new Rect(x0 + Pad, y, ColName - Pad, h), "Name", _styleHeader);
            GUI.Label(new Rect(x1, y, colW, h), ".asset", _styleHeaderRight);
            GUI.Label(new Rect(x2, y, colW, h), ".json", _styleHeaderRight);
            GUI.Label(new Rect(x3, y, colW, h), "Total", _styleHeaderRight);

            // Vertical separators
            DrawColSeparator(rect, x1);
            DrawColSeparator(rect, x2);
            DrawColSeparator(rect, x3);
        }

        void DrawFooter()
        {
            long totalAsset = 0, totalJson = 0;
            foreach (var r in _rows) { totalAsset += r.assetBytes; totalJson += r.jsonBytes; }
            long grandTotal = totalAsset + totalJson;

            Rect sep = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(sep, C_Separator);

            Rect footer = EditorGUILayout.GetControlRect(false, HeaderH);
            EditorGUI.DrawRect(footer, C_Footer);
            EditorGUI.DrawRect(new Rect(footer.x, footer.y, 3, footer.height), C_HeaderBdr);

            float colW = NumColW();
            float x0 = footer.x + 3f;
            float x1 = x0 + ColName;
            float x2 = x1 + colW;
            float x3 = x2 + colW;
            float y = footer.y + (HeaderH - EditorGUIUtility.singleLineHeight) * 0.5f;
            float h = EditorGUIUtility.singleLineHeight;

            DrawColSeparator(footer, x1);
            DrawColSeparator(footer, x2);
            DrawColSeparator(footer, x3);

            GUI.Label(new Rect(x0 + Pad, y, ColName - Pad, h), $"TOTAL  ({_rows.Count} assets)", _styleHeader);
            GUI.Label(new Rect(x1, y, colW, h), FormatBytes(totalAsset), _styleHeaderRight);
            GUI.Label(new Rect(x2, y, colW, h), FormatBytes(totalJson), _styleHeaderRight);
            GUI.Label(new Rect(x3, y, colW, h), FormatBytes(grandTotal), _styleTotal);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static void DrawCell(Rect rect, string text, GUIStyle style)
            => GUI.Label(rect, text, style);

        static void DrawColSeparator(Rect rowRect, float x)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(x - 1, rowRect.y, 1, rowRect.height),
                               GVThemeManager.Current.separator);
        }

        static long FileSize(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            if (!File.Exists(path)) return 0;
            return new FileInfo(path).Length;
        }

        static string GetJsonPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            string dir = Path.GetDirectoryName(assetPath);
            string name = Path.GetFileNameWithoutExtension(assetPath);
            // Matches ALoader.jsonFileName = "." + baseName + ".json"
            return Path.Combine(dir, "." + name + ".json").Replace("\\", "/");
        }

        static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "—";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F2} MB";
        }

        GVTheme _lastTheme;

        void EnsureStyles()
        {
            var t = GVThemeManager.Current;
            if (_styleHeader != null && _lastTheme == t) return;
            _lastTheme = t;

            _styleHeader = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = t.fontSizeBody,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleHeader.normal.textColor = t.textPrimary;

            _styleHeaderRight = new GUIStyle(_styleHeader)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            _styleCell = new GUIStyle(EditorStyles.label)
            {
                fontSize = t.fontSizeBody,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleCell.normal.textColor = t.textSecondary;
            _styleCell.hover.textColor = Color.white;

            _styleCellRight = new GUIStyle(_styleCell)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            _styleTag = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = t.fontSizeSmall,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleTag.normal.textColor = t.accent;

            _styleTemplateTag = new GUIStyle(_styleTag);
            _styleTemplateTag.normal.textColor = t.buttonPrimary;

            _styleTotal = new GUIStyle(_styleCellRight)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _styleTotal.normal.textColor = t.textPrimary;
        }
    }
}
#endif