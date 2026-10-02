using UnityEngine;
using GVLoadSystem.GVEditor;
using GVLoadSystem.Core;



#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;

namespace GVLoadSystem.GVDebug
{

    /// <summary>
    /// Runtime debug overlay for GroupValues inspection and optional editing.
    /// Activated via the configured KeyCode. Registered automatically on scene load
    /// when enabled in GroupValuesProjectSettings.
    /// </summary>
    internal class GroupValuesDebugOverlay : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        static GroupValuesDebugOverlay _instance;

        // ── Settings helpers — works in editor and builds ─────────────────
        static bool S_EnableOverlay =>
#if UNITY_EDITOR
            GroupValuesProjectSettings.instance?.enableDebugOverlay ?? GroupValuesDebugOverlayConfig.enableOverlay;
#else
        GroupValuesDebugOverlayConfig.enableOverlay;
#endif
        static KeyCode S_DebugKey =>
#if UNITY_EDITOR
            GroupValuesProjectSettings.instance != null
                ? GroupValuesProjectSettings.instance.debugKey
                : GroupValuesDebugOverlayConfig.debugKey;
#else
        GroupValuesDebugOverlayConfig.debugKey;
#endif
        static bool S_EditMode =>
#if UNITY_EDITOR
            GroupValuesProjectSettings.instance != null
                ? GroupValuesProjectSettings.instance.overlayEditMode
                : GroupValuesDebugOverlayConfig.editMode;
#else
        GroupValuesDebugOverlayConfig.editMode;
#endif
        static bool S_FreezeOnOpen =>
#if UNITY_EDITOR
            GroupValuesProjectSettings.instance != null
                ? GroupValuesProjectSettings.instance.freezeTimeOnOverlay
                : GroupValuesDebugOverlayConfig.freezeOnOpen;
#else
        GroupValuesDebugOverlayConfig.freezeOnOpen;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstantiate()
        {
            if (!S_EnableOverlay) return;
            if (_instance != null) return;

            var go = new GameObject("[GV Debug Overlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GroupValuesDebugOverlay>();
        }

        // ── State ──────────────────────────────────────────────────────────
        bool _visible;
        float _savedTimeScale = 1f;

        // Selected GV
        int _selectedGVIndex = -1;
        GroupValues _selectedGV;
        GroupValues _editCopy;       // clone for editing

        // UI state
        Vector2 _gvListScroll;
        Vector2 _contentScroll;
        string _searchQuery = "";
        bool _searchFocus;

        // Dirty tracking per GV name
        readonly HashSet<string> _dirtyGVs = new();

        // Cached GV list — refreshed each time overlay opens
        List<GroupValues> _gvList = new();

        // ── Layout constants ──────────────────────────────────────────────
        // Overlay size is proportional to screen — 85% width, 80% height, min 640x480
        float OverlayW => Mathf.Max(Screen.width * 0.85f, 640f);
        float OverlayH => Mathf.Max(Screen.height * 0.80f, 480f);
        float ListW => Mathf.Max(OverlayW * 0.22f, 160f);
        const float HeaderH = 36f;
        const float FooterH = 36f;
        const float RowH = 22f;

        // ── Colors ────────────────────────────────────────────────────────
        // Colors from GVTheme.Current — available in runtime via GVTheme.Current
        static Color C_Bg => WithAlpha(GVTheme.Current?.backgroundDeep ?? new Color(0.08f, 0.09f, 0.12f), 0.97f);
        static Color C_Panel => GVTheme.Current?.backgroundPanel ?? new Color(0.11f, 0.12f, 0.16f);
        static Color C_Header => GVTheme.Current?.backgroundPanel ?? new Color(0.14f, 0.16f, 0.22f);
        static Color C_Accent => GVTheme.Current?.accent ?? new Color(0.25f, 0.55f, 0.90f);
        static Color C_Selected => WithAlpha(GVTheme.Current?.selected ?? new Color(0.18f, 0.35f, 0.62f), 0.90f);
        static Color C_RowEven => GVTheme.Current?.backgroundRow0 ?? new Color(0.12f, 0.14f, 0.18f);
        static Color C_RowOdd => GVTheme.Current?.backgroundRow1 ?? new Color(0.10f, 0.12f, 0.16f);
        static Color C_Separator => GVTheme.Current?.separator ?? new Color(0.22f, 0.25f, 0.32f);
        static Color C_Dirty => GVTheme.Current?.dirty ?? new Color(0.90f, 0.60f, 0.10f);
        static Color C_Value => GVTheme.Current?.valid ?? new Color(0.45f, 0.85f, 0.55f);
        static Color C_Key => GVTheme.Current?.textPrimary ?? new Color(0.75f, 0.85f, 1.00f);
        static Color C_FieldName => GVTheme.Current?.accent ?? new Color(0.55f, 0.70f, 1.00f);

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ── Styles ────────────────────────────────────────────────────────
        GUIStyle _styleHeader;
        GUIStyle _styleBody;
        GUIStyle _styleKey;
        GUIStyle _styleValue;
        GUIStyle _styleFieldName;
        GUIStyle _styleGVBtn;
        GUIStyle _styleGVBtnSelected;
        GUIStyle _styleSearch;
        GUIStyle _styleBtn;
        GUIStyle _styleDirtyLabel;
        bool _stylesBuilt;
        GVTheme _lastTheme;

        // ── Unity events ──────────────────────────────────────────────────
        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(GroupValuesProjectSettings.instance?.debugKey ?? S_DebugKey))
#else
        if (Input.GetKeyDown(S_DebugKey))
#endif
                ToggleOverlay();
        }

        // ── Toggle ────────────────────────────────────────────────────────
        void ToggleOverlay()
        {
            _visible = !_visible;
#if UNITY_EDITOR
            bool freezeTime = GroupValuesProjectSettings.instance?.freezeTimeOnOverlay ?? S_FreezeOnOpen;
#else
        bool freezeTime = S_FreezeOnOpen;
#endif

            if (_visible)
            {
                RefreshGVList();
                if (freezeTime)
                {
                    _savedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }
            else
            {
                if (freezeTime)
                    Time.timeScale = _savedTimeScale;
            }
        }

        void RefreshGVList()
        {
#if UNITY_EDITOR
            _gvList = GroupValuesRegistry.GetAll();
#else
        _gvList = new System.Collections.Generic.List<GroupValues>(
            UnityEngine.Resources.LoadAll<GroupValues>(""));
#endif
            if (_selectedGVIndex >= _gvList.Count) _selectedGVIndex = -1;
        }

        // ── OnGUI ─────────────────────────────────────────────────────────
        void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            // Dim background
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Center the overlay
            float ox = (Screen.width - OverlayW) * 0.5f;
            float oy = (Screen.height - OverlayH) * 0.5f;
            Rect overlayRect = new Rect(ox, oy, OverlayW, OverlayH);

            // Shadow
            GUI.color = new Color(0, 0, 0, 0.4f);
            GUI.DrawTexture(new Rect(ox + 4, oy + 4, OverlayW, OverlayH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Background
            DrawRect(overlayRect, C_Bg);
            DrawRect(new Rect(ox, oy, OverlayW, 2), C_Accent); // top accent line

            // Header
            DrawHeader(new Rect(ox, oy, OverlayW, HeaderH));

            // Body
            float bodyY = oy + HeaderH;
            float bodyH = OverlayH - HeaderH - FooterH;

            DrawGVList(new Rect(ox, bodyY, ListW, bodyH));
            DrawRect(new Rect(ox + ListW, bodyY, 1, bodyH), C_Separator);
            DrawContent(new Rect(ox + ListW + 1, bodyY, OverlayW - ListW - 1, bodyH));

            // Footer
            DrawFooter(new Rect(ox, oy + OverlayH - FooterH, OverlayW, FooterH));

            // Consume input so game doesn't receive it while overlay is open
            if (Event.current.type != EventType.Repaint &&
                Event.current.type != EventType.Layout)
            {
                if (overlayRect.Contains(Event.current.mousePosition))
                    Event.current.Use();
            }
        }

        // ── Header ────────────────────────────────────────────────────────
        void DrawHeader(Rect r)
        {
            DrawRect(r, C_Header);
            DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), C_Separator);
            DrawRect(new Rect(r.x, r.y, 3, r.height), C_Accent);

            GUI.Label(new Rect(r.x + 12, r.y + 8, 220, 20),
                      "GroupValues Debug Overlay", _styleHeader);

            // Search field
            float sw = 220f;
            GUI.SetNextControlName("GVDebugSearch");
            _searchQuery = GUI.TextField(
                new Rect(r.xMax - sw - 48f, r.y + 7f, sw, 22f),
                _searchQuery, _styleSearch);
            GUI.Label(new Rect(r.xMax - sw - 68f, r.y + 8f, 20f, 20f), "🔍", _styleBody);

            // Close button
            GUI.backgroundColor = (GVTheme.Current?.buttonDanger ?? new Color(0.55f, 0.15f, 0.15f));
            if (GUI.Button(new Rect(r.xMax - 36f, r.y + 6f, 28f, 24f), "✕", _styleBtn))
                ToggleOverlay();
            GUI.backgroundColor = Color.white;
        }

        // ── GV List ───────────────────────────────────────────────────────
        void DrawGVList(Rect r)
        {
            DrawRect(r, C_Panel);

            // Sub-header
            Rect subHdr = new Rect(r.x, r.y, r.width, 24f);
            DrawRect(subHdr, C_Header);
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width, 16), "Group Values", _styleFieldName);

            // Refresh is in the footer — no duplicate button here

            // Scrollable list
            Rect listArea = new Rect(r.x, r.y + 24, r.width, r.height - 24);
            GUI.BeginGroup(listArea);
            _gvListScroll = GUI.BeginScrollView(
                new Rect(0, 0, listArea.width, listArea.height),
                _gvListScroll,
                new Rect(0, 0, listArea.width - 14, _gvList.Count * RowH));

            for (int i = 0; i < _gvList.Count; i++)
            {
                var gv = _gvList[i];
                bool active = i == _selectedGVIndex;
                bool dirty = _dirtyGVs.Contains(gv.name);

                Rect rowR = new Rect(0, i * RowH, listArea.width - 14, RowH);
                DrawRect(rowR, active ? C_Selected : (i % 2 == 0 ? C_RowEven : C_RowOdd));

                if (active)
                    DrawRect(new Rect(0, i * RowH, 3, RowH), C_Accent);

                if (dirty)
                    GUI.Label(new Rect(rowR.xMax - 14, rowR.y + 4, 12, 14), "●", _styleDirtyLabel);

                if (GUI.Button(new Rect(4, i * RowH, rowR.width - 18, RowH),
                               gv.name, active ? _styleGVBtnSelected : _styleGVBtn))
                {
                    SelectGV(i);
                }
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        void SelectGV(int index)
        {
            _selectedGVIndex = index;
            _selectedGV = _gvList[index];
            _editCopy = _selectedGV.Clone();
            _contentScroll = Vector2.zero;
        }

        // ── Content panel ─────────────────────────────────────────────────
        void DrawContent(Rect r)
        {
            if (_selectedGV == null)
            {
                GUI.Label(new Rect(r.x + 16, r.y + 16, r.width - 32, 24),
                          "← Select a GroupValues to inspect", _styleBody);
                return;
            }

#if UNITY_EDITOR
            bool editMode = GroupValuesProjectSettings.instance?.overlayEditMode ?? S_EditMode;
#else
        bool editMode = S_EditMode;
#endif

            // Sub-header
            Rect subHdr = new Rect(r.x, r.y, r.width, 24f);
            DrawRect(subHdr, C_Header);
            DrawRect(new Rect(r.x, r.y, 3, 24), C_Accent);

            bool isDirty = _dirtyGVs.Contains(_selectedGV.name);
            string title = _selectedGV.name + (isDirty ? "  ●" : "");
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 80, 16), title, _styleFieldName);

            // Apply button (edit mode only)
            if (editMode && isDirty)
            {
                GUI.backgroundColor = (GVTheme.Current?.buttonPrimary ?? new Color(0.20f, 0.55f, 0.85f));
                if (GUI.Button(new Rect(r.xMax - 60f, r.y + 2f, 54f, 20f), "Apply", _styleBtn))
                    ApplyEdits();
                GUI.backgroundColor = Color.white;
            }

            // Scrollable content
            Rect contentArea = new Rect(r.x, r.y + 24, r.width, r.height - 24);
            GUI.BeginGroup(contentArea);

            // Calculate total content height
            float totalH = CalculateContentHeight(editMode);

            _contentScroll = GUI.BeginScrollView(
                new Rect(0, 0, contentArea.width, contentArea.height),
                _contentScroll,
                new Rect(0, 0, contentArea.width - 14, totalH));

            float y = 8f;
            float contentW = contentArea.width - 14f;
            var source = editMode ? _editCopy : _selectedGV;

            foreach (var field in source.fields)
            {
                // Field header
                DrawRect(new Rect(0, y, contentW, 20f), C_Header);
                DrawRect(new Rect(0, y, 3, 20f), C_Accent);
                GUI.Label(new Rect(6, y + 2, contentW - 8, 16), field.fieldName, _styleFieldName);
                y += 22f;

                foreach (var entry in field.entries)
                {
                    // Filter by search
                    if (!string.IsNullOrEmpty(_searchQuery) &&
                        !entry.name.ToLower().Contains(_searchQuery.ToLower()) &&
                        !(entry.value?.GetValue()?.ToString() ?? "").ToLower()
                            .Contains(_searchQuery.ToLower()))
                        continue;

                    bool rowEven = (int)(y / RowH) % 2 == 0;
                    DrawRect(new Rect(0, y, contentW, RowH), rowEven ? C_RowEven : C_RowOdd);

                    // Key
                    GUI.Label(new Rect(8, y + 3, 160, 16), entry.name, _styleKey);

                    // Type badge
                    string typeName = entry.type.ToString();
                    GUI.Label(new Rect(172, y + 3, 60, 16), typeName, _styleBody);

                    // Value
                    string rawVal = entry.value?.GetValue()?.ToString() ?? "null";
                    float valX = 236f;
                    float valW = contentW - valX - 8f;

                    if (editMode)
                    {
                        bool changed = DrawEntryEditControl(
                            new Rect(valX, y + 2, valW, RowH - 4), entry);
                        if (changed) _dirtyGVs.Add(_selectedGV.name);
                    }
                    else
                    {
                        GUI.Label(new Rect(valX, y + 3, valW, 16), rawVal, _styleValue);
                    }

                    y += RowH;
                }

                y += 6f; // space between fields
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        float CalculateContentHeight(bool editMode)
        {
            if (_selectedGV == null) return 0;
            var source = editMode ? _editCopy : _selectedGV;
            float h = 8f;
            foreach (var field in source.fields)
            {
                h += 22f; // field header
                foreach (var entry in field.entries)
                {
                    if (!string.IsNullOrEmpty(_searchQuery) &&
                        !entry.name.ToLower().Contains(_searchQuery.ToLower()) &&
                        !(entry.value?.GetValue()?.ToString() ?? "").ToLower()
                            .Contains(_searchQuery.ToLower()))
                        continue;
                    h += RowH;
                }
                h += 6f;
            }
            return h;
        }

        // ── Footer ────────────────────────────────────────────────────────
        void DrawFooter(Rect r)
        {
            DrawRect(r, C_Header);
            DrawRect(new Rect(r.x, r.y, r.width, 1), C_Separator);

#if UNITY_EDITOR
            var _s = GroupValuesProjectSettings.instance;
            string info = $"Key: {(_s?.debugKey ?? S_DebugKey)}";
            if (_s?.freezeTimeOnOverlay ?? S_FreezeOnOpen) info += "  |  Time frozen";
            if (_s?.overlayEditMode ?? S_EditMode) info += "  |  Edit mode ON";
#else
        string info = $"Key: {S_DebugKey}";
        if (S_FreezeOnOpen) info += "  |  Time frozen";
        if (S_EditMode) info += "  |  Edit mode ON";
#endif

            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 20), info, _styleBody);

            // Refresh all values button
            GUI.backgroundColor = (GVTheme.Current?.buttonNeutral ?? new Color(0.22f, 0.28f, 0.40f));
            if (GUI.Button(new Rect(r.xMax - 110f, r.y + 6f, 100f, 24f), "↻ Refresh", _styleBtn))
            {
                RefreshGVList();
                if (_selectedGVIndex >= 0 && _selectedGVIndex < _gvList.Count)
                    SelectGV(_selectedGVIndex);
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Type-aware edit controls ──────────────────────────────────────
        // Returns true if the value changed.
        bool DrawEntryEditControl(Rect r, GVEntry entry)
        {
            object cur = entry.value?.GetValue();
            if (cur == null) return false;

            switch (entry.type)
            {
                case VALUE_TYPE.BOOL:
                    {
                        bool v = cur is bool b && b;
                        bool n = GUI.Toggle(new Rect(r.x, r.y + 2, 20, r.height - 4), v, "");
                        if (n != v) { entry.value.SetValue(n); return true; }
                        GUI.Label(new Rect(r.x + 22, r.y + 2, r.width - 22, 16),
                                  v ? "true" : "false", _styleValue);
                        return false;
                    }
                case VALUE_TYPE.INT:
                case VALUE_TYPE.SHORT:
                case VALUE_TYPE.BYTE:
                case VALUE_TYPE.LONG:
                case VALUE_TYPE.FLOAT:
                case VALUE_TYPE.DOUBLE:
                case VALUE_TYPE.CHAR:
                case VALUE_TYPE.STRING:
                    {
                        string raw = cur.ToString();
                        string edited = GUI.TextField(r, raw, _styleValue);
                        if (edited == raw) return false;
                        try { entry.ConvertStringToValue(edited); return true; }
                        catch { return false; }
                    }
                case VALUE_TYPE.VECTOR2:
                    {
                        Vector2 v = cur is Vector2 v2 ? v2 : Vector2.zero;
                        float hw = (r.width - 4) / 2f;
                        string sx = GUI.TextField(new Rect(r.x, r.y, hw, r.height), v.x.ToString("F2"), _styleValue);
                        string sy = GUI.TextField(new Rect(r.x + hw + 4, r.y, hw, r.height), v.y.ToString("F2"), _styleValue);
                        bool changed = false;
                        if (float.TryParse(sx, out float nx) && nx != v.x) { v.x = nx; changed = true; }
                        if (float.TryParse(sy, out float ny) && ny != v.y) { v.y = ny; changed = true; }
                        if (changed) { entry.value.SetValue(v); return true; }
                        return false;
                    }
                case VALUE_TYPE.VECTOR3:
                    {
                        Vector3 v = cur is Vector3 v3 ? v3 : Vector3.zero;
                        float tw = (r.width - 8) / 3f;
                        string sx = GUI.TextField(new Rect(r.x, r.y, tw, r.height), v.x.ToString("F2"), _styleValue);
                        string sy = GUI.TextField(new Rect(r.x + tw + 4, r.y, tw, r.height), v.y.ToString("F2"), _styleValue);
                        string sz = GUI.TextField(new Rect(r.x + tw * 2 + 8, r.y, tw, r.height), v.z.ToString("F2"), _styleValue);
                        bool changed = false;
                        if (float.TryParse(sx, out float nx) && nx != v.x) { v.x = nx; changed = true; }
                        if (float.TryParse(sy, out float ny) && ny != v.y) { v.y = ny; changed = true; }
                        if (float.TryParse(sz, out float nz) && nz != v.z) { v.z = nz; changed = true; }
                        if (changed) { entry.value.SetValue(v); return true; }
                        return false;
                    }
                case VALUE_TYPE.CUSTOM:
                    {
                        // Show JSON truncated — full editing of custom types not supported in overlay
                        string json = cur.ToString();
                        string display = json.Length > 60 ? json.Substring(0, 57) + "..." : json;
                        GUI.Label(r, display, _styleValue);
                        return false;
                    }
            }
            return false;
        }

        // ── Apply edits ───────────────────────────────────────────────────
        void ApplyEdits()
        {
            if (_editCopy == null || _selectedGV == null) return;

            // Find a LoaderMono in scene that references this GV
            var loaders = FindObjectsByType<LoaderMono>(FindObjectsSortMode.None);
            LoaderMono loader = null;
            foreach (var l in loaders)
            {
                // LoaderMono contains an ALoader field — find it first, then get 'values' from it
                object aLoaderInstance = null;
                foreach (var f in typeof(LoaderMono).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (f.FieldType == typeof(ALoader) ||
                        f.FieldType.IsSubclassOf(typeof(ALoader)))
                    {
                        aLoaderInstance = f.GetValue(l);
                        break;
                    }
                }

                if (aLoaderInstance == null) continue;

                var valuesField = typeof(ALoader).GetField("values",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (valuesField?.GetValue(aLoaderInstance) is GroupValues gv &&
                    gv.name == _selectedGV.name)
                {
                    loader = l;
                    break;
                }
            }

            _selectedGV.CopyFrom(_editCopy);

            if (loader != null)
            {
                loader.SaveData();
#if LOG_LOADSYSTEM
            Debug.Log($"[GV Overlay] Applied and saved '{_selectedGV.name}' via LoaderMono.");
#endif
            }
            else
            {
                Debug.LogWarning($"[GV Overlay] Applied '{_selectedGV.name}' in memory only — " +
                                 $"no LoaderMono found. Changes will be lost on next Load.");
            }

            _dirtyGVs.Remove(_selectedGV.name);
            _editCopy = _selectedGV.Clone();
        }

        // ── Draw helpers ──────────────────────────────────────────────────
        static void DrawRect(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ── Styles ────────────────────────────────────────────────────────
        void EnsureStyles()
        {
            var current = GVTheme.Current;
            if (_stylesBuilt && _lastTheme == current) return;
            _stylesBuilt = true;
            _lastTheme = current;

            var monoFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "Lucida Console", "monospace" }, 11);

            _styleHeader = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold };
            _styleHeader.normal.textColor = GVTheme.Current?.textPrimary ?? new Color(0.85f, 0.92f, 1.00f);

            _styleBody = new GUIStyle(GUI.skin.label) { fontSize = GVTheme.Current?.fontSizeSmall ?? 11 };
            _styleBody.normal.textColor = GVTheme.Current?.textSecondary ?? new Color(0.70f, 0.75f, 0.85f);

            _styleKey = new GUIStyle(GUI.skin.label)
            { fontSize = 11, fontStyle = FontStyle.Bold, font = monoFont };
            _styleKey.normal.textColor = C_Key;

            _styleValue = new GUIStyle(GUI.skin.label) { fontSize = 11, font = monoFont };
            _styleValue.normal.textColor = C_Value;
            _styleValue.focused.textColor = Color.white;
            _styleValue.active.textColor = Color.white;
            // Make textfield background transparent
            var vBg = GVTheme.Current?.backgroundCode ?? new Color(0.08f, 0.14f, 0.08f);
            _styleValue.normal.background = MakeTex(new Color(vBg.r, vBg.g, vBg.b, 0.6f));
            _styleValue.focused.background = MakeTex(new Color(vBg.r, vBg.g, vBg.b, 0.9f));

            _styleFieldName = new GUIStyle(GUI.skin.label)
            { fontSize = 11, fontStyle = FontStyle.Bold };
            _styleFieldName.normal.textColor = C_FieldName;

            _styleGVBtn = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 2, 2)
            };
            _styleGVBtn.normal.textColor = GVTheme.Current?.textSecondary ?? new Color(0.75f, 0.80f, 0.90f);
            _styleGVBtn.hover.textColor = Color.white;

            _styleGVBtnSelected = new GUIStyle(_styleGVBtn) { fontStyle = FontStyle.Bold };
            _styleGVBtnSelected.normal.textColor = Color.white;

            _styleSearch = new GUIStyle(GUI.skin.textField) { fontSize = 11 };
            _styleSearch.normal.textColor = GVTheme.Current?.textPrimary ?? new Color(0.85f, 0.90f, 1.00f);

            _styleBtn = new GUIStyle(GUI.skin.button) { fontSize = 10, fontStyle = FontStyle.Bold };
            _styleBtn.normal.textColor = GVTheme.Current?.textPrimary ?? new Color(0.85f, 0.92f, 1.00f);
            _styleBtn.hover.textColor = Color.white;

            _styleDirtyLabel = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            _styleDirtyLabel.normal.textColor = C_Dirty;
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
#endif
namespace GVLoadSystem.GVDebug{
    /// <summary>
    /// Baked configuration for the debug overlay.
    /// Set by GroupValuesBuildProcessor before building.
    /// Allows GroupValuesProjectSettings (editor-only) settings to persist into builds.
    /// </summary>
    internal static class GroupValuesDebugOverlayConfig
    {
        // These are set by GroupValuesBuildProcessor.BakeOverlayConfig()
        // before making a build. In editor they are overridden by ProjectSettings directly.
        public static bool enableOverlay = false;
        public static KeyCode debugKey = KeyCode.F1;
        public static bool editMode = false;
        public static bool freezeOnOpen = false;
    }
}