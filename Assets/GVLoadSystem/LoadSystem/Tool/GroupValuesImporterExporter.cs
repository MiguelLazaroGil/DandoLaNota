#if UNITY_EDITOR
using System.IO;
using System.Xml.Serialization;
using GVLoadSystem;
using GVLoadSystem.Core;
using GVLoadSystem.GVEditor;
using UnityEditor;
using UnityEngine;
using static GVLoadSystem.Core.ALoader;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Editor windows for importing/exporting GroupValues to/from JSON, XML, and CSV.
    /// Accessible via Tools > LoadSystem menu.
    /// </summary>
    // ─────────────────────────────────────────────────────────────────────────────
    // EXPORTER
    // ─────────────────────────────────────────────────────────────────────────────
    internal class GroupValuesExporterWindow : EditorWindow
    {
        internal enum FileType { JSON, XML, CSV }

        FileType _fileType;
        GroupValues _gvToExport;
        GVTheme _lastTheme;
        GUIStyle _titleStyle;
        GUIStyle _labelStyle;

        [MenuItem("Tools/LoadSystem/Export GroupValues",priority=100)]
        public static void Open() => GetWindow<GroupValuesExporterWindow>("Export GroupValues");

        void OnGUI()
        {
            EnsureStyles();
            var t = GVThemeManager.Current;

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), t.backgroundDeep);

            // Header
            Rect hdr = new Rect(0, 0, position.width, 36f);
            EditorGUI.DrawRect(hdr, t.backgroundPanel);
            EditorGUI.DrawRect(new Rect(0, 0, 3, 36f), t.accent);
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), t.separator);
            GUI.Label(new Rect(12, 8, position.width - 16, 20), "Export GroupValues", _titleStyle);

            EditorGUILayout.Space(44);

            // GV field
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.LabelField("GroupValues", _labelStyle, GUILayout.Width(100));
            _gvToExport = (GroupValues)EditorGUILayout.ObjectField(
                _gvToExport, typeof(GroupValues), false);
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // File type tabs
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            DrawTab("JSON", 0);
            DrawTab("XML", 1);
            DrawTab("CSV", 2);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(16);

            // Export button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.enabled = _gvToExport != null;
            GUI.backgroundColor = _gvToExport != null ? t.buttonSuccess : t.buttonNeutral;
            if (GUILayout.Button("Export", GVEditorStyles.StyleToolbarButton(),
                                 GUILayout.Height(28)))
                Export();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        void DrawTab(string label, int idx)
        {
            var t = GVThemeManager.Current;
            bool active = (int)_fileType == idx;
            Rect r = GUILayoutUtility.GetRect(60f, 24f,
                               GUILayout.Width(60f), GUILayout.Height(24f));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, active ? t.selected : t.backgroundPanel);
                if (active)
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), t.accent);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), t.separator);
            }

            var style = new GUIStyle(GUIStyle.none)
            {
                fontSize = t.fontSizeSmall,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = active ? t.textPrimary : t.textSecondary;

            if (GUI.Button(r, label, style))
                _fileType = (FileType)idx;
        }

        void Export()
        {
            string ext = _fileType == FileType.JSON ? "json"
                       : _fileType == FileType.XML ? "xml" : "csv";
            string path = EditorUtility.SaveFilePanel(
                "Export GroupValues", Application.dataPath, _gvToExport.name, ext);
            if (string.IsNullOrEmpty(path)) return;

            if (_fileType == FileType.JSON)
            {
                var sgs = new SerializableGroupValues();
                sgs.CopyFrom(_gvToExport);
                File.WriteAllText(path, JsonUtility.ToJson(sgs, true));
            }
            else if (_fileType == FileType.XML)
            {
                File.WriteAllText(path, ConvertToXML(_gvToExport),
                                  System.Text.Encoding.Unicode);
            }
            else // CSV
            {
                File.WriteAllText(path, GroupValuesCSV.Export(_gvToExport),
                                  System.Text.Encoding.UTF8);
            }

            EditorUtility.RevealInFinder(path);
#if LOG_LOADSYSTEM
        Debug.Log($"GroupValues exported to: {path}");
#endif
        }

        string ConvertToXML(GroupValues gv)
        {
            var sgs = new SerializableGroupValuesXML();
            sgs.CopyFrom(gv);
            var serializer = new XmlSerializer(typeof(SerializableGroupValuesXML));
            using var writer = new StringWriter();
            serializer.Serialize(writer, sgs);
            return writer.ToString();
        }

        void EnsureStyles()
        {
            var t = GVThemeManager.Current;
            if (_titleStyle != null && _lastTheme == t) return;
            _lastTheme = t;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = t.fontSizeBody };
            _titleStyle.normal.textColor = t.textPrimary;

            _labelStyle = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall };
            _labelStyle.normal.textColor = t.textSecondary;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // IMPORTER
    // ─────────────────────────────────────────────────────────────────────────────
    internal class GroupValuesImporterWindow : EditorWindow
    {
        internal enum FileType { JSON, XML, CSV }

        FileType _fileType;
        string _selectedFilePath;
        GVTheme _lastTheme;
        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _pathStyle;

        [MenuItem("Tools/LoadSystem/Import GroupValues",priority = 100)]
        public static void Open() => GetWindow<GroupValuesImporterWindow>("Import GroupValues");

        void OnGUI()
        {
            EnsureStyles();
            var t = GVThemeManager.Current;

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), t.backgroundDeep);

            // Header
            Rect hdr = new Rect(0, 0, position.width, 36f);
            EditorGUI.DrawRect(hdr, t.backgroundPanel);
            EditorGUI.DrawRect(new Rect(0, 0, 3, 36f), t.accent);
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), t.separator);
            GUI.Label(new Rect(12, 8, position.width - 16, 20), "Import GroupValues", _titleStyle);

            EditorGUILayout.Space(44);

            // File type tabs
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            DrawTab("JSON", 0);
            DrawTab("XML", 1);
            DrawTab("CSV", 2);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);

            // Select file button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.backgroundColor = t.buttonNeutral;
            if (GUILayout.Button("Select File", GVEditorStyles.StyleToolbarButton(),
                                 GUILayout.Height(28), GUILayout.Width(120)))
            {
                string ext = _fileType == FileType.JSON ? "json"
                                   : _fileType == FileType.XML ? "xml" : "csv";
                _selectedFilePath = EditorUtility.OpenFilePanel(
                    "Select GroupValues file", "", ext);
            }
            GUI.backgroundColor = Color.white;

            if (!string.IsNullOrEmpty(_selectedFilePath))
            {
                GUILayout.Space(8);
                GUILayout.Label(Path.GetFileName(_selectedFilePath), _pathStyle);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(16);

            // Import button
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.enabled = !string.IsNullOrEmpty(_selectedFilePath);
            GUI.backgroundColor = GUI.enabled ? t.buttonSuccess : t.buttonNeutral;
            if (GUILayout.Button("Import", GVEditorStyles.StyleToolbarButton(),
                                 GUILayout.Height(28)))
                ImportFile();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        void DrawTab(string label, int idx)
        {
            var t = GVThemeManager.Current;
            bool active = (int)_fileType == idx;
            Rect r = GUILayoutUtility.GetRect(60f, 24f,
                              GUILayout.Width(60f), GUILayout.Height(24f));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, active ? t.selected : t.backgroundPanel);
                if (active)
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), t.accent);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), t.separator);
            }

            var style = new GUIStyle(GUIStyle.none)
            {
                fontSize = t.fontSizeSmall,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = active ? t.textPrimary : t.textSecondary;

            if (GUI.Button(r, label, style))
                _fileType = (FileType)idx;
        }

        void ImportFile()
        {
            if (!File.Exists(_selectedFilePath))
            {
                Debug.LogError("File not found");
                return;
            }

            var gv = ScriptableObject.CreateInstance<GroupValues>();

            if (_fileType == FileType.JSON)
            {
                string json = File.ReadAllText(_selectedFilePath);
                var sgs = JsonUtility.FromJson<SerializableGroupValues>(json);
                if (sgs == null) { Debug.LogError("Failed to deserialize JSON"); return; }
                sgs.ApplyTo(gv);
            }
            else if (_fileType == FileType.XML)
            {
                SerializableGroupValuesXML sgs;
                var serializer = new XmlSerializer(typeof(SerializableGroupValuesXML));
                using (var reader = new StreamReader(_selectedFilePath,
                                                      detectEncodingFromByteOrderMarks: true))
                    sgs = (SerializableGroupValuesXML)serializer.Deserialize(reader);
                if (sgs == null) { Debug.LogError("Failed to deserialize XML"); return; }
                sgs.ApplyTo(gv);
            }
            else // CSV
            {
                string csv = File.ReadAllText(_selectedFilePath, System.Text.Encoding.UTF8);
                GroupValuesCSV.Import(csv, gv);
            }

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Save GroupValues Asset", "NewGroupValues", "asset",
                "Choose where to save the GroupValues asset");
            if (string.IsNullOrEmpty(assetPath)) return;

            AssetDatabase.CreateAsset(gv, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = gv;
#if LOG_LOADSYSTEM
        Debug.Log("GroupValues imported successfully");
#endif
        }

        void EnsureStyles()
        {
            var t = GVThemeManager.Current;
            if (_titleStyle != null && _lastTheme == t) return;
            _lastTheme = t;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = t.fontSizeBody };
            _titleStyle.normal.textColor = t.textPrimary;

            _labelStyle = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall };
            _labelStyle.normal.textColor = t.textSecondary;

            _pathStyle = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeSmall, fontStyle = FontStyle.Italic };
            _pathStyle.normal.textColor = t.textDim;
        }
    }
}
#endif