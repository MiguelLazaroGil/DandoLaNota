#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{


    internal class GroupValuesDocumentationWindow : EditorWindow
    {
        // ── State ──────────────────────────────────────────────────────────
        GroupValues _documentationGV;
        int _selectedIndex = -1;
        int _scrollToEntry = -1;
        Vector2 _indexScroll;
        Vector2 _contentScroll;

        readonly Dictionary<string, System.Action> _uiElements = new();

        // ── Styles ─────────────────────────────────────────────────────────
        GUIStyle _styleTitle;
        GUIStyle _styleBody;
        GUIStyle _styleCode;
        GUIStyle _styleFieldHeader;
        GUIStyle _styleTopicBtn;
        GUIStyle _styleTopicBtnSelected;
        GUIStyle _styleSectionNumber;
        GUIStyle _styleSubTitle;
        GUIStyle _styleSubTitle2;
        GUIStyle _styleSubTitle3;
        GUIStyle _styleLink;
        GUIStyle _styleCopyBtn;
        bool _stylesBuilt;

        static Color C_Bg => GVThemeManager.Current.backgroundDeep;
        static Color C_Panel => GVThemeManager.Current.backgroundPanel;
        static Color C_CodeBg => GVThemeManager.Current.backgroundCode;
        static Color C_Accent => GVThemeManager.Current.accent;
        static Color C_Selected => GVThemeManager.Current.selected;
        static Color C_Separator => GVThemeManager.Current.separator;

        [MenuItem("Help/LoadSystem Documentation", priority = 101)]
        internal static void Open()
        {
            var w = GetWindow<GroupValuesDocumentationWindow>("Documentation");
            w.minSize = new Vector2(640, 480);
        }

        /// <summary>Opens documentation and navigates to the section matching target.</summary>
        internal static void OpenAtTarget(string target)
        {
            var w = GetWindow<GroupValuesDocumentationWindow>("Documentation");
            w.minSize = new Vector2(640, 480);
            if (!string.IsNullOrEmpty(target))
                w.NavigateTo(target);
        }



        void OnEnable() => _documentationGV = GroupValuesRegistry.FindByName("Documentation");

        void OnGUI()
        {
            EnsureStyles();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), C_Bg);
            DrawTopBar();

            if (_documentationGV == null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.HelpBox("No Documentation GroupValues found. " +
                    "Assign one or create a GroupValues named 'Documentation'.",
                    MessageType.Info);
                return;
            }

            float topH = 36f;
            float bodyY = topH;
            float bodyH = position.height - topH;
            float indexW = 210f;

            DrawIndexPanel(new Rect(0, bodyY, indexW, bodyH));
            DrawContentPanel(new Rect(indexW + 1, bodyY, position.width - indexW - 1, bodyH));
            EditorGUI.DrawRect(new Rect(indexW, bodyY, 1, bodyH), C_Separator);
        }

        void DrawTopBar()
        {
            Rect bar = new Rect(0, 0, position.width, 36f);
            EditorGUI.DrawRect(bar, C_Panel);
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), C_Separator);
            GUI.Label(new Rect(12, 8, 200, 20), "LoadSystem Documentation", _styleFieldHeader);

            float objW = 220f;
            EditorGUI.LabelField(new Rect(position.width - objW - 120f, 8, 80f, 20), "Source:", _styleBody);
            _documentationGV = (GroupValues)EditorGUI.ObjectField(
                new Rect(position.width - objW - 36f, 8, objW, 18),
                _documentationGV, typeof(GroupValues), false);
        }

        void DrawIndexPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, C_Panel);

            Rect headerR = new Rect(rect.x, rect.y, rect.width, 28f);
            EditorGUI.DrawRect(headerR, C_Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), C_Separator);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, 28), C_Accent);
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, rect.width - 12, 18), "Topics", _styleFieldHeader);

            Rect scrollArea = new Rect(rect.x, rect.y + 28, rect.width, rect.height - 28);
            GUILayout.BeginArea(scrollArea);
            _indexScroll = GUILayout.BeginScrollView(_indexScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            for (int i = 0; i < _documentationGV.fields.Count; i++)
            {
                var field = _documentationGV.fields[i];
                bool active = i == _selectedIndex;
                string label = $"{i + 1}.  {field.fieldName}";

                if (active)
                {
                    Rect btnBg = GUILayoutUtility.GetRect(new GUIContent(label),
                        _styleTopicBtnSelected, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(btnBg, C_Selected);
                    EditorGUI.DrawRect(new Rect(btnBg.x, btnBg.y, 3, btnBg.height), C_Accent);
                    GUI.Label(btnBg, label, _styleTopicBtnSelected);
                    if (Event.current.type == EventType.MouseDown &&
                        btnBg.Contains(Event.current.mousePosition))
                    {
                        _selectedIndex = i;
                        _contentScroll = Vector2.zero;
                        Repaint();
                    }
                }
                else
                {
                    if (GUILayout.Button(label, _styleTopicBtn, GUILayout.ExpandWidth(true)))
                    {
                        _selectedIndex = i;
                        _contentScroll = Vector2.zero;
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawContentPanel(Rect rect)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _documentationGV.fields.Count)
            {
                GUI.Label(new Rect(rect.x + 20, rect.y + 20, rect.width - 40, 30),
                          "← Select a topic from the index.", _styleBody);
                return;
            }

            var field = _documentationGV.fields[_selectedIndex];

            Rect hdr = new Rect(rect.x, rect.y, rect.width, 36f);
            EditorGUI.DrawRect(hdr, C_Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), C_Separator);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, 36), C_Accent);
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, rect.width - 24, 26),
                      $"{_selectedIndex + 1}.  {field.fieldName}", _styleFieldHeader);

            Rect scrollRect = new Rect(rect.x, rect.y + 36, rect.width, rect.height - 36);
            GUILayout.BeginArea(scrollRect);
            _contentScroll = GUILayout.BeginScrollView(_contentScroll);
            GUILayout.Space(8);

            ResetSubCounters();
            for (int ei = 0; ei < field.entries.Count; ei++)
            {
                if (_scrollToEntry == ei && Event.current.type == EventType.Repaint)
                {
                    _contentScroll = new Vector2(0, GUILayoutUtility.GetLastRect().y);
                    _scrollToEntry = -1;
                }
                DrawEntry(field.entries[ei], _selectedIndex, ei);
                GUILayout.Space(12);
            }

            GUILayout.Space(16);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── Sub-entry helpers ──────────────────────────────────────────────
        int _lastMainEntryIndex = -1;
        int _currentSubIndex = 0;
        int _lastSub1Index = -1;
        int _currentSub2Index = 0;
        int _lastSub2Index = -1;
        int _currentSub3Index = 0;

        void ResetSubCounters()
        {
            _lastMainEntryIndex = -1; _currentSubIndex = 0;
            _lastSub1Index = -1; _currentSub2Index = 0;
            _lastSub2Index = -1; _currentSub3Index = 0;
        }

        static int SubLevel(string n)
        {
            string t = n.TrimStart();
            if (t.StartsWith("<sub3>", System.StringComparison.OrdinalIgnoreCase)) return 3;
            if (t.StartsWith("<sub2>", System.StringComparison.OrdinalIgnoreCase)) return 2;
            if (t.StartsWith("<sub>", System.StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        static string StripSubTag(string n) => Regex.Replace(n.Trim(), @"(?i)^<sub\d?>\s*", "");
        static string StripLeadingNum(string n) => Regex.Replace(n.Trim(), @"^\d+(\.\d+)*\.?\s*", "");

        void DrawEntry(GVEntry entry, int fieldIndex, int entryIndex)
        {
            string rawValue = entry.value?.GetValue()?.ToString() ?? "";
            int level = SubLevel(entry.name);
            string cleanName = StripLeadingNum(level > 0 ? StripSubTag(entry.name) : entry.name);

            string numberedTitle;
            float indent;
            GUIStyle titleStyle;

            switch (level)
            {
                case 1:
                    _currentSubIndex++;
                    _lastSub1Index = _currentSubIndex;
                    _currentSub2Index = 0;
                    numberedTitle = $"{fieldIndex + 1}.{_lastMainEntryIndex + 1}.{_currentSubIndex}  {cleanName}";
                    indent = 28f; titleStyle = _styleSubTitle; break;
                case 2:
                    _currentSub2Index++;
                    _lastSub2Index = _currentSub2Index;
                    _currentSub3Index = 0;
                    numberedTitle = $"{fieldIndex + 1}.{_lastMainEntryIndex + 1}.{_lastSub1Index}.{_currentSub2Index}  {cleanName}";
                    indent = 44f; titleStyle = _styleSubTitle2; break;
                case 3:
                    _currentSub3Index++;
                    numberedTitle = $"{fieldIndex + 1}.{_lastMainEntryIndex + 1}.{_lastSub1Index}.{_lastSub2Index}.{_currentSub3Index}  {cleanName}";
                    indent = 60f; titleStyle = _styleSubTitle3; break;
                default:
                    int mainIdx = entryIndex - _currentSubIndex - _currentSub2Index - _currentSub3Index;
                    _lastMainEntryIndex = mainIdx;
                    _currentSubIndex = _currentSub2Index = _currentSub3Index = 0;
                    numberedTitle = $"{fieldIndex + 1}.{mainIdx + 1}  {cleanName}";
                    indent = 12f; titleStyle = _styleTitle; break;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUILayout.Label(numberedTitle, titleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(20f);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            DrawRichContent(rawValue, indent);
        }

        // ── Rich content parser ────────────────────────────────────────────
        static readonly Regex s_codeOpen = new Regex(@"<code>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex s_codeClose = new Regex(@"</code>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex s_uiTag = new Regex(@"<ui=""([^""]+)""\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex s_linkRx = new Regex(@"<a=""([^""]+)"">([^<]*)</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex s_inlineUiRx = new Regex(@"<ui=""(Badge[^""]+)""\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        const string LinkColor = "#55BFFF";

        void DrawRichContent(string raw, float baseIndent = 16f)
        {
            int pos = 0;
            while (pos < raw.Length)
            {
                var nextCode = s_codeOpen.Match(raw, pos);
                var nextUi = s_uiTag.Match(raw, pos);
                int codeIdx = nextCode.Success ? nextCode.Index : int.MaxValue;
                int uiIdx = nextUi.Success ? nextUi.Index : int.MaxValue;
                int nextIdx = System.Math.Min(codeIdx, uiIdx);

                if (nextIdx == int.MaxValue)
                {
                    string tail = raw.Substring(pos).Trim();
                    if (tail.Length > 0) DrawBodyText(tail, baseIndent);
                    break;
                }

                if (nextIdx > pos)
                {
                    string before = raw.Substring(pos, nextIdx - pos).Trim();
                    if (before.Length > 0) DrawBodyText(before, baseIndent);
                }

                if (codeIdx <= uiIdx)
                {
                    int contentStart = nextCode.Index + nextCode.Length;
                    var closeMatch = s_codeClose.Match(raw, contentStart);
                    if (!closeMatch.Success) { DrawCodeBlock(raw.Substring(contentStart), baseIndent); break; }
                    DrawCodeBlock(raw.Substring(contentStart, closeMatch.Index - contentStart), baseIndent);
                    pos = closeMatch.Index + closeMatch.Length;
                }
                else
                {
                    DrawInlineUI(nextUi.Groups[1].Value, baseIndent);
                    pos = nextUi.Index + nextUi.Length;
                }
            }
        }

        void DrawBodyText(string text, float indent = 16f)
        {
            if (s_inlineUiRx.IsMatch(text))
            {
                DrawBodyTextWithBadges(text, indent);
                return;
            }

            var linkTargets = new List<string>();
            string richText = s_linkRx.Replace(text, m =>
            {
                linkTargets.Add(m.Groups[1].Value);
                return $"<color={LinkColor}><b>{m.Groups[2].Value}</b></color>";
            });

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent + 4f);
            GUILayout.BeginVertical();

            float realW = Mathf.Max(position.width - 210f - indent - 28f, 60f);
            float h = _styleBody.CalcHeight(new GUIContent(richText), realW) + 10;
            Rect lRect = EditorGUILayout.GetControlRect(false, h, GUILayout.ExpandWidth(true));

            GUI.Label(lRect, richText, _styleBody);

            if (linkTargets.Count > 0)
            {
                EditorGUIUtility.AddCursorRect(lRect, MouseCursor.Link);
                if (Event.current.type == EventType.MouseDown &&
                    lRect.Contains(Event.current.mousePosition))
                {
                    int li = 0;
                    foreach (Match m in s_linkRx.Matches(text))
                    {
                        string beforeRich = s_linkRx.Replace(
                            text.Substring(0, m.Index),
                            mm => $"<color={LinkColor}><b>{mm.Groups[2].Value}</b></color>");
                        var noWrap = new GUIStyle(_styleBody) { wordWrap = false };
                        float bW = noWrap.CalcSize(new GUIContent(beforeRich)).x;
                        float lW = new GUIStyle(_styleBody) { wordWrap = false, richText = false }
                                           .CalcSize(new GUIContent(m.Groups[2].Value)).x;
                        float lineX = bW % realW;
                        float mouseX = Event.current.mousePosition.x - lRect.x;

                        if (mouseX >= lineX && mouseX <= lineX + lW)
                        {
                            NavigateTo(linkTargets[li]);
                            Event.current.Use();
                            break;
                        }
                        li++;
                    }
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(20f);
            GUILayout.EndHorizontal();
        }

        void DrawBodyTextWithBadges(string text, float indent)
        {
            int pos = 0;
            var matches = s_inlineUiRx.Matches(text);

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent + 4f);

            foreach (Match m in matches)
            {
                if (m.Index > pos)
                {
                    string seg = text.Substring(pos, m.Index - pos);
                    string richSeg = s_linkRx.Replace(seg,
                        mm => $"<color={LinkColor}><b>{mm.Groups[2].Value}</b></color>");
                    GUILayout.Label(richSeg, _styleBody, GUILayout.ExpandWidth(false));
                }

                GUILayout.Space(2f);
                if (_uiElements.TryGetValue(m.Groups[1].Value, out var draw)) draw();
                GUILayout.Space(2f);
                pos = m.Index + m.Length;
            }

            if (pos < text.Length)
            {
                string richTail = s_linkRx.Replace(text.Substring(pos),
                    mm => $"<color={LinkColor}><b>{mm.Groups[2].Value}</b></color>");
                GUILayout.Label(richTail, _styleBody, GUILayout.ExpandWidth(true));
            }
            else GUILayout.FlexibleSpace();

            GUILayout.Space(20f);
            GUILayout.EndHorizontal();
        }

        // ── Navigation ────────────────────────────────────────────────────
        void NavigateTo(string target)
        {
            // action: → ExecuteMenuItem
            const string actionPrefix = "action:";
            const string settingsPrefix = "settings:";

            if (target.StartsWith(actionPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                string menuPath = target.Substring(actionPrefix.Length).Trim();
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                    Debug.LogWarning($"[Documentation] MenuItem not found: '{menuPath}'");
                return;
            }

            // settings: → SettingsService.OpenProjectSettings
            if (target.StartsWith(settingsPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                string settingsPath = target.Substring(settingsPrefix.Length).Trim();
                SettingsService.OpenProjectSettings(settingsPath);
                return;
            }

            // Section reference: "FieldName" or "FieldName/EntryName"
            string fieldName;
            string entryName = null;

            int slashIdx = target.LastIndexOf('/');
            if (slashIdx > 0)
            {
                fieldName = target.Substring(0, slashIdx).Trim();
                entryName = target.Substring(slashIdx + 1).Trim();
            }
            else
            {
                fieldName = target.Trim();
            }

            for (int fi = 0; fi < _documentationGV.fields.Count; fi++)
            {
                string cleanField = StripLeadingNum(_documentationGV.fields[fi].fieldName);
                if (!string.Equals(cleanField, fieldName,
                                   System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(_documentationGV.fields[fi].fieldName, fieldName,
                                   System.StringComparison.OrdinalIgnoreCase)) continue;

                _selectedIndex = fi;
                _contentScroll = Vector2.zero;

                if (entryName != null)
                {
                    var entries = _documentationGV.fields[fi].entries;
                    for (int ei = 0; ei < entries.Count; ei++)
                    {
                        string clean = StripLeadingNum(SubLevel(entries[ei].name) > 0
                                           ? StripSubTag(entries[ei].name) : entries[ei].name);
                        if (string.Equals(clean, entryName,
                                          System.StringComparison.OrdinalIgnoreCase))
                        {
                            _scrollToEntry = ei;
                            break;
                        }
                    }
                }
                Repaint();
                return;
            }
            Debug.LogWarning($"[Documentation] Reference not found: '{target}'");
        }

        // ── Inline UI ──────────────────────────────────────────────────────
        void DrawInlineUI(string name, float indent = 16f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent + 4f);
            if (_uiElements.TryGetValue(name, out var draw))
                draw();
            else
                GUILayout.Label($"[ui: {name}]", _styleBody);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        void DrawInlineBadge(string name)
        {
            if (_uiElements.TryGetValue(name, out var draw))
                draw();
            else
                GUILayout.Label($"[ui: {name}]", _styleBody, GUILayout.ExpandWidth(false));
        }

        void DrawFakeButton(string label, Color bg, float width = 180f, float height = 24f)
        {
            GUI.backgroundColor = bg;
            GUILayout.Button(label, GVEditorStyles.StyleToolbarButton(),
                             GUILayout.Width(width), GUILayout.Height(height));
            GUI.backgroundColor = Color.white;
        }

        void DrawFakeBadge(string label, VALUE_TYPE type)
        {
            float bh = EditorGUIUtility.singleLineHeight - 2f;
            Rect r = GUILayoutUtility.GetRect(56f, bh, GUILayout.Width(56f), GUILayout.Height(bh));
            r.y += 1f;
            GVEditorStyles.DrawBadge(r, label, GVEditorStyles.GetTypeColor(type));
        }

        // ── Code block ─────────────────────────────────────────────────────
        void DrawCodeBlock(string code, float indent = 16f)
        {
            GUILayout.Space(4);
            string highlighted = CSharpSyntaxHighlighter.Highlight(code.Trim());

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent + 4f);
            GUILayout.BeginVertical();

            Rect topLine = EditorGUILayout.GetControlRect(false, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(topLine, C_Separator);

            var drawStyle = new GUIStyle(_styleCode) { richText = true };
            var calcStyle = new GUIStyle(_styleCode) { richText = false };
            Rect labelRect = EditorGUILayout.GetControlRect(true,
                calcStyle.CalcHeight(new GUIContent(code.Trim()),
                                     position.width - indent - 40f) + 12f,
                GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(labelRect, C_CodeBg);
                EditorGUI.DrawRect(new Rect(labelRect.x, labelRect.y, 3, labelRect.height),
                                   new Color(GVThemeManager.Current.accent.r, GVThemeManager.Current.accent.g, GVThemeManager.Current.accent.b, 0.8f));
            }

            GUI.Label(new Rect(labelRect.x + 8, labelRect.y + 6,
                               labelRect.width - 12, labelRect.height - 12),
                      highlighted, drawStyle);

            Rect copyR = new Rect(labelRect.xMax - 58f, labelRect.y + 4f, 52f, 18f);
            GUI.backgroundColor = GVThemeManager.Current.backgroundCode;
            if (GUI.Button(copyR, "⎘ Copy", _styleCopyBtn))
            {
                GUIUtility.systemCopyBuffer = code.Trim();
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            Rect botLine = EditorGUILayout.GetControlRect(false, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(botLine, C_Separator);

            GUILayout.EndVertical();
            GUILayout.Space(16f);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // ── Styles ─────────────────────────────────────────────────────────
        GVTheme _lastTheme;

        void EnsureStyles()
        {
            var t = GVThemeManager.Current;
            if (_stylesBuilt && _lastTheme == t) return;
            _stylesBuilt = true;
            _lastTheme = t;

            var monoFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "Lucida Console", "monospace" },
                t.fontSizeCode);

            _styleTitle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = t.fontSizeTitle, wordWrap = true, richText = true };
            _styleTitle.normal.textColor = t.textPrimary;

            _styleBody = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeBody, wordWrap = true, richText = true };
            _styleBody.normal.textColor = t.textSecondary;

            _styleCode = new GUIStyle(EditorStyles.label)
            { fontSize = t.fontSizeCode, wordWrap = true, richText = true, font = monoFont };
            _styleCode.normal.textColor = t.textCode;

            _styleFieldHeader = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = t.fontSizeBody, wordWrap = false };
            _styleFieldHeader.normal.textColor = t.textPrimary;

            _styleSectionNumber = new GUIStyle(_styleFieldHeader);
            _styleSectionNumber.normal.textColor = t.accent;

            _styleTopicBtn = new GUIStyle(GUIStyle.none)
            {
                fontSize = t.fontSizeSmall,
                wordWrap = false,
                padding = new RectOffset(14, 8, 6, 6),
                alignment = TextAnchor.MiddleLeft,
            };
            _styleTopicBtn.normal.textColor = t.textSecondary;
            _styleTopicBtn.hover.textColor = Color.white;

            _styleTopicBtnSelected = new GUIStyle(_styleTopicBtn)
            { fontStyle = FontStyle.Bold, padding = new RectOffset(16, 8, 6, 6) };
            _styleTopicBtnSelected.normal.textColor = t.textPrimary;

            _styleSubTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = t.fontSizeBody,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true
            };
            _styleSubTitle.normal.textColor = t.textPrimary;

            _styleSubTitle2 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = t.fontSizeSmall,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true
            };
            _styleSubTitle2.normal.textColor = t.textSecondary;

            _styleSubTitle3 = new GUIStyle(EditorStyles.label)
            {
                fontSize = t.fontSizeSmall,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                richText = true
            };
            _styleSubTitle3.normal.textColor = t.textDim;

            _styleLink = new GUIStyle(EditorStyles.label)
            {
                fontSize = t.fontSizeBody,
                wordWrap = false,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0),
            };
            _styleLink.normal.textColor = t.accent;
            _styleLink.hover.textColor = Color.white;

            _styleCopyBtn = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = t.fontSizeMini,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _styleCopyBtn.normal.textColor = t.textSecondary;
            _styleCopyBtn.hover.textColor = Color.white;

            var green = t.buttonSuccess;
            var blue = t.buttonPrimary;
            var dark = t.buttonNeutral;
            var orange = t.buttonWarning;
            var red = t.buttonDanger;

            _uiElements["Apply"] = () => DrawFakeButton("Apply", green);
            _uiElements["CreateGroupValues"] = () => DrawFakeButton("Create Group Values", green);
            _uiElements["CreateTemplate"] = () => DrawFakeButton("Create Template", green);
            _uiElements["TemplateToGV"] = () => DrawFakeButton("Template to GV", green);
            _uiElements["TemplateFromGV"] = () => DrawFakeButton("Template from GV", dark);
            _uiElements["CopyFromCurrent"] = () => DrawFakeButton("Copy from current", dark);
            _uiElements["Save"] = () => DrawFakeButton("Save", green);
            _uiElements["Load"] = () => DrawFakeButton("Load", blue);
            _uiElements["ResetAll"] = () => DrawFakeButton("Reset All", orange);
            _uiElements["ResetToDefaults"] = () => DrawFakeButton("Reset to Defaults", orange);
            _uiElements["Reset"] = () => DrawFakeButton("Reset", orange);
            _uiElements["RefreshList"] = () => DrawFakeButton("Refresh List", dark);
            _uiElements["CreateGV"] = () => DrawFakeButton("Create Group Values", green, 180f);
            _uiElements["DeleteSelected"] = () => DrawFakeButton("Delete Selected", red, 180f);
            _uiElements["OpenSettings"] = () => DrawFakeButton("Open Project Settings", dark, 180f);
            _uiElements["BadgeBool"] = () => DrawFakeBadge("BOOL", VALUE_TYPE.BOOL);
            _uiElements["BadgeInt"] = () => DrawFakeBadge("INT", VALUE_TYPE.INT);
            _uiElements["BadgeFloat"] = () => DrawFakeBadge("FLOAT", VALUE_TYPE.FLOAT);
            _uiElements["BadgeString"] = () => DrawFakeBadge("STRING", VALUE_TYPE.STRING);
            _uiElements["BadgeCustom"] = () => DrawFakeBadge("CUSTOM", VALUE_TYPE.CUSTOM);
        }
    }
    #region CSHARPSYNTAXHIGHLIGHTER
    /// <summary>
    /// Converts a C# code string into Unity RichText matching VS Code Dark+ theme.
    /// </summary>
    internal static class CSharpSyntaxHighlighter
    {
        // ── VS Code Dark+ palette (exact match) ───────────────────────────
        const string C_Keyword = "#569CD6"; // blue       — public, class, void, using...
        const string C_Control = "#C586C0"; // purple     — if, for, return, foreach...
        const string C_Type = "#4EC9B0"; // teal       — class names, Unity types
        const string C_Field = "#9CDCFE"; // light blue — variable/field names
        const string C_Method = "#DCDCAA"; // yellow     — method names + attributes
        const string C_String = "#CE9178"; // orange     — string and char literals
        const string C_Number = "#B5CEA8"; // light green— numeric literals
        const string C_Comment = "#6A9955"; // green      — // and /* */ comments
        const string C_Plain = "#D4D4D4"; // off-white  — default / punctuation
        const string C_Attribute = "#9CDCFE"; // light blue — [Attribute] fallback
        const string C_Punct = "#808080"; // grey       — brackets, punctuation

        // ── Keyword sets ──────────────────────────────────────────────────
        static readonly HashSet<string> s_control = new()
    {
        "if","else","for","foreach","while","do","switch","case","default",
        "break","continue","return","goto","throw","try","catch","finally",
        "yield","await","when","in",
    };

        static readonly HashSet<string> s_keywords = new()
    {
        "public","private","protected","internal","static","readonly","const",
        "abstract","virtual","override","sealed","partial","new","this","base",
        "null","true","false","void","var","ref","out","params","is","as",
        "typeof","sizeof","checked","unchecked","unsafe","fixed","lock","async",
        "delegate","event","operator","implicit","explicit","get","set","value",
        "where","using","namespace","class","struct","interface","enum",
    };

        // These get the teal type color
        static readonly HashSet<string> s_types = new()
    {
        "int","float","double","bool","string","char","byte","short","long",
        "uint","ulong","ushort","sbyte","decimal","object","dynamic",
        "Vector2","Vector3","Vector4","Quaternion","Color","Color32","Rect",
        "Bounds","Transform","GameObject","MonoBehaviour","ScriptableObject",
        "Component","Mathf","Debug","Time","String","Boolean","Int32","Single",
        "List","Dictionary","HashSet","IEnumerable","IList","Action","Func",
        "Task","Coroutine","WaitForSeconds","WaitUntil","Texture2D","Sprite",
        "AudioClip","AnimationCurve","Gradient","LayerMask","RaycastHit",
        "EditorWindow","Editor","SerializedObject","SerializedProperty",
        "ScriptableObject","AssetDatabase","EditorUtility","Selection",
        "GUILayout","EditorGUILayout","EditorGUI","GUI","GUIStyle","GUIContent",
        "GUILayoutOption","Rect","Event","EventType","KeyCode","GUIUtility",
    };

        // ── Token types ───────────────────────────────────────────────────
        enum TT
        {
            LineComment, BlockComment,
            VerbatimString, InterpolatedString, StringLit, CharLit,
            Attribute,
            Number,
            Identifier,
            Punctuation,
            Whitespace,
        }

        static readonly (TT type, Regex rx)[] s_tokens = new (TT, Regex)[]
        {
        (TT.LineComment,        new Regex(@"//[^\n]*",
                                    RegexOptions.Compiled)),
        (TT.BlockComment,       new Regex(@"/\*[\s\S]*?\*/",
                                    RegexOptions.Compiled)),
        (TT.VerbatimString,     new Regex(@"@""(?:""""|[^""])*""",
                                    RegexOptions.Compiled)),
        (TT.InterpolatedString, new Regex(@"\$""(?:[^""\\]|\\.)*""",
                                    RegexOptions.Compiled)),
        (TT.StringLit,          new Regex(@"""(?:[^""\\]|\\.)*""",
                                    RegexOptions.Compiled)),
        (TT.CharLit,            new Regex(@"'(?:[^'\\]|\\.)'",
                                    RegexOptions.Compiled)),
        (TT.Attribute,          new Regex(@"\[[A-Za-z_]\w*(?:\([^)]*\))?\]",
                                    RegexOptions.Compiled)),
        (TT.Number,             new Regex(@"\b\d+(?:\.\d+)?(?:[fFdDlLuU])?\b",
                                    RegexOptions.Compiled)),
        (TT.Identifier,         new Regex(@"\b[A-Za-z_]\w*\b",
                                    RegexOptions.Compiled)),
        (TT.Punctuation,        new Regex(@"[{}()\[\];,.<>!&|^~+\-*/%=?:]",
                                    RegexOptions.Compiled)),
        (TT.Whitespace,         new Regex(@"[ \t\r\n]+",
                                    RegexOptions.Compiled)),
        };

        // ── Public API ────────────────────────────────────────────────────
        internal static string Highlight(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;

            var sb = new StringBuilder(code.Length * 3);
            int pos = 0;

            while (pos < code.Length)
            {
                (TT type, Match m) best = (TT.Whitespace, null);
                int bestStart = int.MaxValue;

                foreach (var (type, rx) in s_tokens)
                {
                    var m = rx.Match(code, pos);
                    if (m.Success && m.Index < bestStart)
                    {
                        bestStart = m.Index;
                        best = (type, m);
                        if (m.Index == pos) break;
                    }
                }

                if (best.m == null)
                {
                    sb.Append(Col(Esc(code.Substring(pos)), C_Plain));
                    break;
                }

                if (bestStart > pos)
                    sb.Append(Col(Esc(code.Substring(pos, bestStart - pos)), C_Plain));

                string raw = best.m.Value;

                switch (best.type)
                {
                    case TT.LineComment:
                    case TT.BlockComment:
                        sb.Append(Col(Esc(raw), C_Comment));
                        break;

                    case TT.StringLit:
                    case TT.VerbatimString:
                    case TT.InterpolatedString:
                    case TT.CharLit:
                        sb.Append(Col(Esc(raw), C_String));
                        break;

                    case TT.Attribute:
                        // Split attribute into: [ name (args) ]
                        // Brackets in light grey, name in teal, args in default
                        sb.Append(ColorizeAttribute(raw));
                        break;

                    case TT.Number:
                        sb.Append(Col(Esc(raw), C_Number));
                        break;

                    case TT.Identifier:
                        sb.Append(ColorizeIdent(raw, code, best.m.Index + raw.Length));
                        break;

                    case TT.Punctuation:
                        sb.Append(Col(Esc(raw), C_Plain));
                        break;

                    case TT.Whitespace:
                        sb.Append(raw);
                        break;

                    default:
                        sb.Append(Esc(raw));
                        break;
                }

                pos = best.m.Index + best.m.Length;
            }

            return sb.ToString();
        }

        // ── Attribute coloring ───────────────────────────────────────────
        // VS Code Dark+: [SerializeField]  →  [ in grey, SerializeField in teal, ] in grey
        //                [Range(0f, 1f)]   →  [ in grey, Range in teal, (0f, 1f) in plain, ] in grey
        static readonly System.Text.RegularExpressions.Regex s_attrInner =
            new System.Text.RegularExpressions.Regex(
                @"^\[([A-Za-z_]\w*)(.*)?\]$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        static string ColorizeAttribute(string raw)
        {
            var m = s_attrInner.Match(raw);
            if (!m.Success) return Col(Esc(raw), C_Attribute);

            string name = m.Groups[1].Value;
            string rest = m.Groups[2].Value; // "(args)" or ""

            var sb = new System.Text.StringBuilder();
            sb.Append(Col("[", C_Punct));
            sb.Append(Col(Esc(name), C_Type));      // attribute name — teal like a type
            if (!string.IsNullOrEmpty(rest))
                sb.Append(Col(Esc(rest), C_Plain)); // args — plain
            sb.Append(Col("]", C_Punct));
            return sb.ToString();
        }

        // ── Identifier classification ─────────────────────────────────────
        static string ColorizeIdent(string word, string src, int after)
        {
            string e = Esc(word);

            if (s_control.Contains(word)) return Col(e, C_Control);
            if (s_keywords.Contains(word)) return Col(e, C_Keyword);
            if (s_types.Contains(word)) return Col(e, C_Type);

            // Method call — look ahead past whitespace for '('
            int i = after;
            while (i < src.Length && src[i] == ' ') i++;
            if (i < src.Length && src[i] == '(') return Col(e, C_Method);

            // PascalCase identifier that isn't in type list — treat as user-defined type
            if (word.Length > 1 && char.IsUpper(word[0]))
                return Col(e, C_Type);

            // Everything else — field/variable (light blue like VS Code)
            return Col(e, C_Field);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static string Col(string text, string hex) => $"<color={hex}>{text}</color>";

        /// Escapes characters that Unity RichText parser would misinterpret.
        static string Esc(string s)
            => s.Replace("&", "&amp;")
                .Replace("<", "\u003C")
                .Replace(">", "\u003E");
    }
}
    #endregion
#endif