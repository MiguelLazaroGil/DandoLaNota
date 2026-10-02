#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using GVLoadSystem.Core;
using GVLoadSystem.Settings;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{

    /// <summary>
    /// Persists UISettings prefab defaults across editor sessions using EditorPrefs + GUIDs.
    /// ScriptableSingleton cannot reliably serialize cross-asset GameObject references,
    /// so we store GUIDs and resolve them on load.
    /// </summary>
    internal static class UISettingsPrefabConfig
    {
        const string K_Toggle = "GV_UIDefaults_Toggle";
        const string K_Slider = "GV_UIDefaults_Slider";
        const string K_Drawer = "GV_UIDefaults_Drawer";
        const string K_TMPDrawer = "GV_UIDefaults_TMPDrawer";
        const string K_TMPInput = "GV_UIDefaults_TMPInput";

        public static GameObject prefabToggle
        {
            get => Load(K_Toggle);
            set => Store(K_Toggle, value);
        }
        public static GameObject prefabSlider
        {
            get => Load(K_Slider);
            set => Store(K_Slider, value);
        }
        public static GameObject prefabDrawer
        {
            get => Load(K_Drawer);
            set => Store(K_Drawer, value);
        }
        public static GameObject prefabTMPDrawer
        {
            get => Load(K_TMPDrawer);
            set => Store(K_TMPDrawer, value);
        }
        public static GameObject prefabTMPInput
        {
            get => Load(K_TMPInput);
            set => Store(K_TMPInput, value);
        }

        static GameObject Load(string key)
        {
            string guid = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void Store(string key, GameObject go)
        {
            if (go == null) { EditorPrefs.DeleteKey(key); return; }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go));
            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(key, guid);
        }

        // No-op Save() for compatibility with existing callers
        public static void Save() { }
    }

    /// <summary>
    /// Editor window for creating and managing UIGVElements in the scene.
    /// Single ReorderableList with visual group separators per canvas parent.
    /// </summary>
    internal class UIGVElementWindow : EditorWindow
    {
        // ── Data ──────────────────────────────────────────────────────────
        [System.Serializable]
        class Row
        {
            public string name = "NewElement";
            public Canvas canvasParent;
            public GameObject sceneGO;      // null if not instantiated yet
            public GameObject prefabRef;    // prefab to instantiate
            public VALUE_TYPE dataType = VALUE_TYPE.BOOL;
            public UIElement uiElement = UIElement.TOGGLE;
            public GroupValues groupValues;
            public string fieldName = "";
            public string entryKey = "";

            public bool IsInstantiated => sceneGO != null;
        }

        List<Row> _rows = new();
        ReorderableList _list;
        Vector2 _scroll;

        // Prefab references — backed by UISettingsPrefabDefaults (persisted)
        GameObject _prefabToggle { get => UISettingsPrefabConfig.prefabToggle; set => UISettingsPrefabConfig.prefabToggle = value; }
        GameObject _prefabSlider { get => UISettingsPrefabConfig.prefabSlider; set => UISettingsPrefabConfig.prefabSlider = value; }
        GameObject _prefabDrawer { get => UISettingsPrefabConfig.prefabDrawer; set => UISettingsPrefabConfig.prefabDrawer = value; }
        GameObject _prefabTMPDrawer { get => UISettingsPrefabConfig.prefabTMPDrawer; set => UISettingsPrefabConfig.prefabTMPDrawer = value; }
        GameObject _prefabTMPInput { get => UISettingsPrefabConfig.prefabTMPInput; set => UISettingsPrefabConfig.prefabTMPInput = value; }

        // GV picker state per row (index → expanded)
        Dictionary<int, bool> _entryPickerExpanded = new();

        // ── Colors ────────────────────────────────────────────────────────
        static Color C_Bg => GVThemeManager.Current.backgroundDeep;
        static Color C_Panel => GVThemeManager.Current.backgroundPanel;
        static Color C_GroupHdr => GVThemeManager.Current.backgroundPanel;
        static Color C_Accent => GVThemeManager.Current.accent;
        static Color C_RowEven => GVThemeManager.Current.backgroundRow0;
        static Color C_RowOdd => GVThemeManager.Current.backgroundRow1;
        static Color C_Separator => GVThemeManager.Current.separator;
        static Color C_Valid => GVThemeManager.Current.valid;
        static Color C_Invalid => GVThemeManager.Current.invalid;

        const float RowH = 24f;

        [MenuItem("Tools/LoadSystem/UI Settings Elements", priority = 13)]
        public static void Open() => GetWindow<UIGVElementWindow>("UI Settings");

        // ── Enable / Disable ──────────────────────────────────────────────
        void OnEnable()
        {
            SyncFromScene();
            BuildList();
        }

        void OnFocus() => SyncFromScene();

        // ── Sync from scene ───────────────────────────────────────────────
        void SyncFromScene()
        {
           /* var existing = FindObjectsByType<UIGVElement>(FindObjectsSortMode.None);
            var keys = new HashSet<int>(existing.Select(e => e.gameObject.GetInstanceID()));

            // Remove rows whose GO no longer exists
            _rows.RemoveAll(r => r.IsInstantiated &&
                                 !keys.Contains(r.sceneGO.GetInstanceID()));

            // Add rows for elements found in scene but not tracked
            foreach (var elem in existing)
            {
                if (_rows.Any(r => r.sceneGO == elem.gameObject)) continue;
                var row = new Row
                {
                    name = elem.gameObject.name,
                    sceneGO = elem.gameObject,
                    canvasParent = elem.GetComponentInParent<Canvas>(),
                    dataType = elem.DataType,
                    uiElement = elem.UIElem,
                    groupValues = elem.Entry?.GroupValues,
                    fieldName = elem.Entry?.FieldName ?? "",
                    entryKey = elem.Entry?.EntryKey ?? "",
                };
                _rows.Add(row);
            }

            // Sort by canvas parent name for consistent grouping
            _rows = _rows
                .OrderBy(r => r.canvasParent != null ? r.canvasParent.name : "zzz")
                .ToList();

            BuildList();
            Repaint();*/
        }

        // ── List builder ──────────────────────────────────────────────────
        void BuildList()
        {
            _list = new ReorderableList(_rows, typeof(Row), true, false, true, true)
            {
                showDefaultBackground = false,
                elementHeightCallback = idx =>
                {
                    bool showGroupHeader = ShouldShowGroupHeader(idx);
                    return (showGroupHeader ? 28f : 0f) + RowH + 2f;
                },
                drawElementCallback = DrawRow,
                drawNoneElementCallback = _ =>
                    EditorGUI.LabelField(
                        new Rect(0, 0, 400, 20),
                        "No UIGVElements. Click '+' to add one.",
                        EditorStyles.centeredGreyMiniLabel),
                onAddCallback = _ => AddRow(),
                onRemoveCallback = l =>
                {
                    var row = _rows[l.index];
                    if (row.IsInstantiated &&
                        EditorUtility.DisplayDialog("Remove",
                            $"Also destroy '{row.name}' from the scene?", "Yes", "No"))
                    {
                        Undo.DestroyObjectImmediate(row.sceneGO);
                    }
                    _rows.RemoveAt(l.index);
                    BuildList();
                },
            };
        }

        bool ShouldShowGroupHeader(int idx)
        {
            if (idx == 0) return true;
            var prev = _rows[idx - 1].canvasParent;
            var curr = _rows[idx].canvasParent;
            return prev != curr;
        }

        // ── Row drawing ───────────────────────────────────────────────────
        void DrawRow(Rect rect, int idx, bool active, bool focused)
        {
            if (idx >= _rows.Count) return;
            var row = _rows[idx];

            float y = rect.y;

            // Group header separator
            if (ShouldShowGroupHeader(idx))
            {
                Rect hdrRect = new Rect(rect.x, y, rect.width, 26f);
                EditorGUI.DrawRect(hdrRect, C_GroupHdr);
                EditorGUI.DrawRect(new Rect(hdrRect.x, hdrRect.y, 3, hdrRect.height), C_Accent);

                string groupName = row.canvasParent != null
                    ? row.canvasParent.name : "— No Canvas Parent —";

                EditorGUI.LabelField(
                    new Rect(hdrRect.x + 8, hdrRect.y + 5, 200, 16),
                    groupName, GVEditorStyles.StyleSectionHeader());

                // Canvas picker inline in header
                var newCanvas = (Canvas)EditorGUI.ObjectField(
                    new Rect(hdrRect.x + 210, hdrRect.y + 4, 180, 18),
                    row.canvasParent, typeof(Canvas), true);

                if (newCanvas != row.canvasParent)
                {
                    // Apply new canvas to all rows in this group and reparent their GOs
                    var oldCanvas = row.canvasParent;
                    foreach (var r in _rows.Where(r => r.canvasParent == oldCanvas))
                    {
                        r.canvasParent = newCanvas;
                        if (r.IsInstantiated)
                        {
                            Transform newParent = newCanvas != null
                                ? newCanvas.transform : null;
                            if (r.sceneGO.transform.parent != newParent)
                                Undo.SetTransformParent(r.sceneGO.transform, newParent,
                                                        "Reparent UIGVElement");
                        }
                    }
                    Repaint();
                }

                y += 28f;
            }

            // Row background
            Rect rowRect = new Rect(rect.x, y, rect.width, RowH);
            EditorGUI.DrawRect(rowRect, idx % 2 == 0 ? C_RowEven : C_RowOdd);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height),
                               GVEditorStyles.GetTypeColor(row.dataType));

            float x = rowRect.x + 6f;
            float ry = rowRect.y + (RowH - EditorGUIUtility.singleLineHeight) * 0.5f;
            float w = rowRect.width - 8f;

            // Column widths
            const float NameW = 110f;
            const float TypeW = 64f;
            const float UIElemW = 80f;
            const float EntryW = 160f;
            const float BtnW = 60f;
            const float Gap = 4f;

            // Name — sync to scene GO with Undo
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(
                new Rect(x, ry, NameW, EditorGUIUtility.singleLineHeight), row.name);
            if (EditorGUI.EndChangeCheck() && newName != row.name)
            {
                row.name = newName;
                if (row.IsInstantiated)
                {
                    Undo.RecordObject(row.sceneGO, "Rename UIGVElement");
                    row.sceneGO.name = newName;
                    EditorUtility.SetDirty(row.sceneGO);
                }
            }
            x += NameW + Gap;

            // DATA TYPE — when changed, auto-update UIElement and prefab
            EditorGUI.BeginChangeCheck();
            var newType = (VALUE_TYPE)EditorGUI.EnumPopup(
                new Rect(x, ry, TypeW, EditorGUIUtility.singleLineHeight), row.dataType);
            if (EditorGUI.EndChangeCheck() && newType != row.dataType)
            {
                row.dataType = newType;
                row.uiElement = DefaultUIElement(newType);
                row.prefabRef = null; // clear so default is re-resolved
                ApplyRowToComponent(row);
            }
            x += TypeW + Gap;

            // UI ELEMENT — when changed, also clear prefabRef so default re-resolves
            EditorGUI.BeginChangeCheck();
            var newElem = (UIElement)EditorGUI.EnumPopup(
                new Rect(x, ry, UIElemW, EditorGUIUtility.singleLineHeight), row.uiElement);
            if (EditorGUI.EndChangeCheck() && newElem != row.uiElement)
            {
                row.uiElement = newElem;
                row.prefabRef = null; // clear so default is re-resolved
                ApplyRowToComponent(row);
            }
            x += UIElemW + Gap;

            // ENTRY REFERENCE — GV + key picker
            DrawEntryPicker(new Rect(x, ry, EntryW, EditorGUIUtility.singleLineHeight), row, idx);
            x += EntryW + Gap;

            // STATUS indicator
            bool valid = row.groupValues != null && !string.IsNullOrEmpty(row.entryKey);
            Rect dotR = new Rect(x, ry + 3, 8, 8);
            EditorGUI.DrawRect(dotR, valid ? C_Valid : C_Invalid);
            x += 12f;

            // GO / Prefab + Create button
            const float DeleteBtnW = 20f;
            float remainW = w - x + rowRect.x + 8f - DeleteBtnW - Gap;

            // Resolve prefab: use assigned one, fall back to default for this UIElement
            var resolvedPrefab = row.prefabRef ?? GetDefaultPrefab(row.uiElement);

            if (row.IsInstantiated)
            {
                EditorGUI.ObjectField(
                    new Rect(x, ry, remainW, EditorGUIUtility.singleLineHeight),
                    row.sceneGO, typeof(GameObject), true);
            }
            else
            {
                float prefW = remainW - BtnW - Gap;

                EditorGUI.BeginChangeCheck();
                var newPrefab = (GameObject)EditorGUI.ObjectField(
                    new Rect(x, ry, prefW, EditorGUIUtility.singleLineHeight),
                    resolvedPrefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                    row.prefabRef = newPrefab; // explicit override

                GUI.backgroundColor = GVThemeManager.Current.buttonPrimary;
                if (GUI.Button(new Rect(x + prefW + Gap, ry, BtnW,
                               EditorGUIUtility.singleLineHeight), "Create"))
                {
                    // Use resolved prefab for instantiation
                    if (row.prefabRef == null) row.prefabRef = resolvedPrefab;
                    InstantiateRow(row);
                }
                GUI.backgroundColor = Color.white;
            }

            // X — delete this row
            float xBtnX = rowRect.x + rowRect.width - DeleteBtnW - 2f;
            GUI.backgroundColor = GVThemeManager.Current.buttonDanger;
            if (GUI.Button(new Rect(xBtnX, ry, DeleteBtnW, EditorGUIUtility.singleLineHeight),
                           "×", GVEditorStyles.StyleDeleteButton()))
            {
                if (row.IsInstantiated &&
                    EditorUtility.DisplayDialog("Remove",
                        $"Also destroy '{row.name}' from the scene?", "Yes", "No"))
                    Undo.DestroyObjectImmediate(row.sceneGO);

                _rows.RemoveAt(idx);
                BuildList();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
        }

        static Color C_EntryWarn => GVThemeManager.Current.warning;

        void DrawEntryPicker(Rect rect, Row row, int idx)
        {
            bool hasEntry = row.groupValues != null && !string.IsNullOrEmpty(row.entryKey);
            bool valid = hasEntry && EntryExists(row);
            string label = !hasEntry ? "— pick entry —" :
                              !valid ? $"⚠  {row.groupValues.name} / {row.entryKey} (not found)" :
                                           $"{row.groupValues.name} / {row.entryKey}";

            Color bg = !hasEntry ? GVThemeManager.Current.textDim :
                       !valid ? C_EntryWarn :
                                   GVEditorStyles.C_HeaderBdr;

            GVEditorStyles.DrawBadge(rect, label, bg);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                if (!valid && hasEntry)
                    Debug.LogWarning(
                        $"[UISettings] Entry '{row.entryKey}' not found in " +
                        $"'{row.groupValues.name}'. Re-link it.");
                ShowEntryPickerMenu(rect, row);
            }
        }

        static bool EntryExists(Row row)
        {
            if (row.groupValues == null || string.IsNullOrEmpty(row.entryKey)) return false;
            foreach (var field in row.groupValues.fields)
                foreach (var entry in field.entries)
                    if (entry.name == row.entryKey) return true;
            return false;
        }

        void ShowEntryPickerMenu(Rect anchor, Row row)
        {
            var menu = new GenericMenu();

            // Get all GVs from registry
            var allGVs = GroupValuesRegistry.GetAll();
            if (allGVs == null || allGVs.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No GroupValues found in Registry"));
                menu.ShowAsContext();
                return;
            }

            foreach (var gv in allGVs)
            {
                foreach (var field in gv.fields)
                {
                    foreach (var entry in field.entries)
                    {
                        var captGV = gv;
                        var captField = field.fieldName;
                        var captKey = entry.name;
                        var captType = entry.type;

                        menu.AddItem(
                            new GUIContent($"{gv.name}/{field.fieldName}/{entry.name} [{entry.type}]"),
                            row.entryKey == captKey && row.groupValues == captGV,
                            () =>
                            {
                                Undo.RecordObject(
                                    row.IsInstantiated ? (UnityEngine.Object)row.sceneGO
                                                       : this,
                                    "Assign GVEntryReference");

                                row.groupValues = captGV;
                                row.fieldName = captField;
                                row.entryKey = captKey;

                                // Only update type/prefab if type actually changed
                                if (row.dataType != captType)
                                {
                                    row.dataType = captType;
                                    row.uiElement = DefaultUIElement(captType);
                                    row.prefabRef = null; // force re-resolve to new default
                                }

                                ApplyRowToComponent(row);
                                Repaint();
                            });
                    }
                }
            }

            menu.ShowAsContext();
        }

        // ── GUI ───────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), C_Bg);

            // Toolbar
            Rect toolbar = new Rect(0, 0, position.width, 36f);
            EditorGUI.DrawRect(toolbar, C_Panel);
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), C_Separator);
            EditorGUI.DrawRect(new Rect(0, 0, 3, 36), C_Accent);
            GUI.Label(new Rect(12, 9, 220, 18), "UI Settings Elements",
                      GVEditorStyles.StyleSectionHeader());

            // Prefab Defaults popup button
            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUI.Button(new Rect(position.width - 230f, 6f, 114f, 24f), "Prefab Defaults ▾"))
                ShowPrefabDefaultsPopup();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = GVThemeManager.Current.buttonSuccess;
            if (GUI.Button(new Rect(position.width - 110f, 6f, 100f, 24f), "Apply All"))
                ApplyAll();
            GUI.backgroundColor = Color.white;

            // Column headers
            DrawColumnHeaders(36f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.Width(position.width));
            GUILayout.Space(36f + 22f);
            if (_list != null) _list.DoLayoutList();
            EditorGUILayout.EndScrollView();
        }

        // ── Prefab slot helpers ───────────────────────────────────────────
        const int SLOT_TOGGLE = 0;
        const int SLOT_SLIDER = 1;
        const int SLOT_DRAWER = 2;
        const int SLOT_TMPDRAWER = 3;
        const int SLOT_TMPINPUT = 4;

        GameObject GetPrefabSlot(int slot)
        {
            switch (slot)
            {
                case SLOT_TOGGLE: return _prefabToggle;
                case SLOT_SLIDER: return _prefabSlider;
                case SLOT_DRAWER: return _prefabDrawer;
                case SLOT_TMPDRAWER: return _prefabTMPDrawer;
                case SLOT_TMPINPUT: return _prefabTMPInput;
                default: return null;
            }
        }

        void SetPrefabSlot(int slot, GameObject value)
        {
            switch (slot)
            {
                case SLOT_TOGGLE: _prefabToggle = value; break;
                case SLOT_SLIDER: _prefabSlider = value; break;
                case SLOT_DRAWER: _prefabDrawer = value; break;
                case SLOT_TMPDRAWER: _prefabTMPDrawer = value; break;
                case SLOT_TMPINPUT: _prefabTMPInput = value; break;
            }
        }

        // ── Prefab Defaults popup ─────────────────────────────────────────
        void ShowPrefabDefaultsPopup()
        {
            var popup = new PrefabDefaultsPopup(this);
            PopupWindow.Show(
                new Rect(position.width - 230f, 30f, 114f, 0f),
                popup);
        }

        // Inner class for the popup content
        class PrefabDefaultsPopup : PopupWindowContent
        {
            readonly UIGVElementWindow _win;
            const float RowH = 24f;
            const float LW = 80f;

            static readonly (int slot, string label, System.Type type)[] s_rows =
            {
            (SLOT_TOGGLE,    "Toggle",     typeof(UnityEngine.UI.Toggle)),
            (SLOT_SLIDER,    "Slider",     typeof(UnityEngine.UI.Slider)),
            (SLOT_DRAWER,    "Drawer",     typeof(UnityEngine.UI.Dropdown)),
            (SLOT_TMPDRAWER, "TMP Drawer", typeof(TMPro.TMP_Dropdown)),
            (SLOT_TMPINPUT,  "TMP Input",  typeof(TMPro.TMP_InputField)),
        };

            public PrefabDefaultsPopup(UIGVElementWindow win) => _win = win;

            public override Vector2 GetWindowSize()
                => new Vector2(340f, s_rows.Length * RowH + 12f);

            public override void OnGUI(Rect rect)
            {
                EditorGUI.DrawRect(rect, GVThemeManager.Current.backgroundDeep);

                float y = rect.y + 6f;
                foreach (var (slot, label, type) in s_rows)
                {
                    DrawRow(slot, label, type, rect.x, y, rect.width);
                    y += RowH;
                }
            }

            void DrawRow(int slot, string label, System.Type type, float x, float y, float width)
            {
                float btnW = 22f;
                float gap = 4f;
                float fw = width - LW - btnW - gap * 2 - 8f;
                var curr = _win.GetPrefabSlot(slot);

                // Label
                EditorGUI.LabelField(new Rect(x + 6, y + 4, LW, 16), label,
                    GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textPrimary));

                // Read-only name field — no object picker circle
                string display = curr != null ? curr.name : "None";
                GUI.backgroundColor = GVThemeManager.Current.backgroundCode;
                EditorGUI.LabelField(
                    new Rect(x + LW + 6, y + 3, fw, 18),
                    display,
                    new GUIStyle(EditorStyles.miniTextField)
                    {
                        normal = { textColor = curr != null
                        ? GVThemeManager.Current.textSecondary
                        : GVThemeManager.Current.textDim }
                    });
                GUI.backgroundColor = Color.white;

                // Search button
                GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
                if (GUI.Button(
                        new Rect(x + LW + fw + gap + 6, y + 3, btnW, 18),
                        "⌕", EditorStyles.miniButton))
                    _win.ShowPrefabPicker(type, slot);
                GUI.backgroundColor = Color.white;
            }
        }

        internal void ShowPrefabPicker(System.Type componentType, int slot)
        {
            var menu = new GenericMenu();
            bool found = false;

            // None option
            menu.AddItem(new GUIContent("None"), GetPrefabSlot(slot) == null,
                () => SetPrefabSlot(slot, null));
            menu.AddSeparator("");

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent(componentType) == null) continue;

                found = true;
                var captGO = go;
                int captSlot = slot;
                menu.AddItem(new GUIContent(go.name), GetPrefabSlot(slot) == go,
                             () => SetPrefabSlot(captSlot, captGO));
            }

            if (!found)
                menu.AddDisabledItem(
                    new GUIContent($"No prefabs with {componentType.Name} found in project"));

            menu.ShowAsContext();
        }

        void DrawColumnHeaders(float y)
        {
            Rect hdr = new Rect(0, y, position.width, 20f);
            EditorGUI.DrawRect(hdr, C_Panel);

            float x = 24f;
            void Col(string label, float w)
            {
                EditorGUI.LabelField(new Rect(x, y + 2, w, 16), label,
                                     GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textSecondary));
                x += w + 4f;
            }
            Col("Name", 110); Col("Type", 64); Col("UI", 80);
            Col("Entry", 160); Col("", 12); Col("GO / Prefab", 140);
        }

        // ── Actions ───────────────────────────────────────────────────────
        void AddRow()
        {
            _rows.Add(new Row());
            BuildList();
        }

        void InstantiateRow(Row row)
        {
            var prefab = row.prefabRef ?? GetDefaultPrefab(row.uiElement);
            if (prefab == null)
            {
                Debug.LogWarning($"[UISettings] No prefab assigned for {row.uiElement}. " +
                                 "Assign one in the Prefab Defaults section.");
                return;
            }

            Transform parent = row.canvasParent != null
                ? row.canvasParent.transform : null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create UIGVElement");
            go.name = row.name;

            var elem = go.GetComponent<UIGVElement>()
                       ?? go.AddComponent<UIGVElement>();

            elem.DataType = row.dataType;
            elem.UIElem = row.uiElement;

            if (row.groupValues != null && !string.IsNullOrEmpty(row.entryKey))
                elem.Entry.SetTarget(row.groupValues, row.fieldName, row.entryKey);

            row.sceneGO = go;
            EditorUtility.SetDirty(go);
            Repaint();
        }

        void ApplyRowToComponent(Row row)
        {
            if (!row.IsInstantiated) return;
            var elem = row.sceneGO.GetComponent<UIGVElement>();
            if (elem == null) return;

            elem.DataType = row.dataType;
            elem.UIElem = row.uiElement;

            if (row.groupValues != null && !string.IsNullOrEmpty(row.entryKey))
            {
                if (elem.Entry == null) elem.InitEntry();
                elem.Entry.SetTarget(row.groupValues, row.fieldName, row.entryKey);
            }

            // Reparent GO if canvas parent changed
            Transform newParent = row.canvasParent != null
                ? row.canvasParent.transform : null;

            if (row.sceneGO.transform.parent != newParent)
            {
                Undo.SetTransformParent(row.sceneGO.transform, newParent,
                                        "Reparent UIGVElement");
            }

            EditorUtility.SetDirty(row.sceneGO);
        }

        void ApplyAll()
        {
            foreach (var row in _rows.Where(r => r.IsInstantiated))
                ApplyRowToComponent(row);
            Debug.Log("[UISettings] Applied all rows to scene components.");
        }

        // ── Helpers ───────────────────────────────────────────────────────
        GameObject GetDefaultPrefab(UIElement elem)
        {
            switch (elem)
            {
                case UIElement.TOGGLE: return _prefabToggle;
                case UIElement.SLIDER: return _prefabSlider;
                case UIElement.DRAWER: return _prefabDrawer;
                case UIElement.TMP_DRAWER: return _prefabTMPDrawer;
                case UIElement.TMP_INPUT: return _prefabTMPInput;
                default: return null;
            }
        }

        UIElement DefaultUIElement(VALUE_TYPE type)
        {
            switch (type)
            {
                case VALUE_TYPE.BOOL: return UIElement.TOGGLE;
                case VALUE_TYPE.FLOAT:
                case VALUE_TYPE.DOUBLE: return UIElement.SLIDER;
                case VALUE_TYPE.INT:
                case VALUE_TYPE.SHORT:
                case VALUE_TYPE.LONG: return UIElement.TMP_DRAWER;
                case VALUE_TYPE.STRING: return UIElement.TMP_INPUT;
                default: return UIElement.TMP_INPUT;
            }
        }
    }
}
#endif