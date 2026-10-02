#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GVLoadSystem.Core;
using UnityEditor;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    internal class GroupValuesWindow : EditorWindow
    {
        private enum EditorMode { GroupValues, Template }
        private EditorMode currentMode = EditorMode.GroupValues;

        ALoader loader = new ALoader();

        private List<GroupValues> allValues;
        private List<GroupValuesTemplate> allValuesT;

        private int selectedIndex = -1;
        private int selectedTemplateIndex = -1;

        private GroupValuesTemplate selectedTemplate;
        private GroupValuesTemplate workingCopyTemplate;

        private GroupValues selected;
        private GroupValues workingCopy;

        private Vector2 scroll;
        private bool showTemplateValues = false;

        private readonly Dictionary<int, bool> fieldFoldouts = new();

        [MenuItem("Tools/LoadSystem/Group Values Editor", priority = 0)]
        public static void Open() => GetWindow<GroupValuesWindow>("GroupValues");

        void OnEnable()
        {
            loader.SetEncryptionSettings(
                GroupValuesProjectSettings.instance.encryptionMethod,
                GroupValuesProjectSettings.instance.passwordSalt);
            RefreshRegistry();
        }

        void RefreshRegistry()
        {
            allValues = GroupValuesRegistry.GetAll();
            allValuesT = GroupValuesRegistry.GetAllTemplates();
            Repaint();
        }

        // ── Main loop ─────────────────────────────────────────────────────
        void OnGUI()
        {
            // Paint the window background before anything else so Unity's default
            // grey panel doesn't show through transparent or undrawn areas.
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                               GVThemeManager.Current.backgroundDeep);

            DrawModeSelector();
            DrawProjectSettingsPanel();

            switch (currentMode)
            {
                case EditorMode.GroupValues:
                    DrawRegistryPanel();
                    if (selected != null)
                    {
                        if (!showTemplateValues) DrawToolbar();
                        DrawEditor();
                    }
                    break;
                case EditorMode.Template:
                    DrawTemplatePanel();
                    if (selectedTemplate != null)
                    {
                        DrawTemplateToolbar();
                        DrawEditorTemplate();
                    }
                    break;
            }
            if (currentMode == EditorMode.GroupValues && workingCopy != null)
                DrawVersionBar();
        }

        // ── Mode selector ─────────────────────────────────────────────────
        void DrawSectionHeaderWithHelp(string title, string docTarget)
        {
            var settings = GroupValuesProjectSettings.instance;
            if (settings != null && !settings.showHelpButton)
            {
                GVEditorStyles.DrawWindowSectionHeader(title);
                return;
            }

            var t = GVThemeManager.Current;
            Rect r = EditorGUILayout.GetControlRect(false, t.headerHeight - 6f);
            GVEditorStyles.DrawBox(r, t.backgroundPanel, t.accent);
            GVEditorStyles.DrawAccentBar(r, t.accent);

            // Help button on the right
            const float btnW = 22f;
            Rect btnR = new Rect(r.xMax - btnW - 4f, r.y + 3f, btnW, r.height - 6f);
            Rect lblR = new Rect(r.x + 8, r.y + 3, r.width - btnW - 12f, r.height);

            GUI.Label(lblR, title, GVEditorStyles.StyleSectionHeader());

            var helpStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            helpStyle.normal.textColor = t.textPrimary;

            GUI.backgroundColor = t.buttonNeutral;
            if (GUI.Button(btnR, "?", helpStyle))
                GroupValuesDocumentationWindow.OpenAtTarget(docTarget);
            GUI.backgroundColor = Color.white;
        }

        void DrawModeSelector()
        {
            var t = GVThemeManager.Current;

            Rect barRect = EditorGUILayout.GetControlRect(false, 32);

            // Draw full bar background
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(barRect, t.backgroundPanel);

            float tabW = barRect.width / 2f;

            // Transparent label style — background drawn manually so Unity can't override it
            var tabStyle = new GUIStyle(GUIStyle.none)
            {
                fontStyle = FontStyle.Bold,
                fontSize = t.fontSizeSmall,
                alignment = TextAnchor.MiddleCenter,
            };

            string[] labels = { "GROUP VALUES", "TEMPLATES" };
            for (int ti = 0; ti < 2; ti++)
            {
                Rect tabR = new Rect(barRect.x + ti * tabW, barRect.y, tabW, barRect.height);
                bool active = (int)currentMode == ti;

                // Draw background first — before the button so it's behind the label
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(tabR, active ? t.selected : t.backgroundPanel);
                    // Active tab: accent line on top
                    if (active)
                        EditorGUI.DrawRect(new Rect(tabR.x, tabR.y, tabR.width, 3), t.accent);
                    // Separator between tabs
                    if (ti == 0)
                        EditorGUI.DrawRect(new Rect(tabR.xMax - 1, tabR.y, 1, tabR.height), t.separator);
                    // Bottom border
                    EditorGUI.DrawRect(new Rect(tabR.x, tabR.yMax - 1, tabR.width, 1), t.separator);
                }

                tabStyle.normal.textColor = active ? t.textPrimary : t.textSecondary;

                if (GUI.Button(tabR, labels[ti], tabStyle))
                {
                    currentMode = (EditorMode)ti;
                    switch (currentMode)
                    {
                        case EditorMode.GroupValues: SelectTemplate(selectedTemplate); break;
                        case EditorMode.Template: Select(selected); break;
                    }
                    GUI.changed = true;
                }
            }

            EditorGUILayout.Space(2);
        }

        // ── Project settings ──────────────────────────────────────────────
        void DrawProjectSettingsPanel()
        {
            EditorGUILayout.Space(2);
            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUILayout.Button("Open Project Settings", GVEditorStyles.StyleUtilButton()))
                SettingsService.OpenProjectSettings("Project/Load System");
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(2);
        }

        // ── Registry panel ────────────────────────────────────────────────
        void DrawRegistryPanel()
        {
            DrawSectionHeaderWithHelp("GROUP VALUES REGISTRY", "Group Values Editor");
            EditorGUILayout.Space(4);

            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUILayout.Button("Refresh List", GVEditorStyles.StyleUtilButton()))
                RefreshRegistry();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(2);

            string[] names = allValues.ConvertAll(v => v.name).ToArray();
            int newIndex = GVEditorStyles.StyledPopup("Selected Asset",
                                    selectedIndex, names, position.width);
            if (newIndex != selectedIndex && newIndex >= 0)
            {
                selectedIndex = newIndex;
                Select(allValues[selectedIndex]);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            if (GVEditorStyles.ColorButton("Create GroupValues", GVThemeManager.Current.buttonSuccess))
                CreateNewGroupValues();
            if (selected != null &&
                GVEditorStyles.ColorButton("Delete Selected", GVThemeManager.Current.buttonDanger))
                DeleteSelected();
            if (GVEditorStyles.ColorButton("Reset All", GVThemeManager.Current.buttonWarning))
                ResetAllGroupValuesAndApply();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            bool isReferenced = allValuesT.Exists(t => t.groupValuesReference == selected);
            if (isReferenced)
                showTemplateValues = GVEditorStyles.StyledToggle(
                    "View Template Values", showTemplateValues, position.width);

            EditorGUILayout.Space(6);
            GVEditorStyles.DrawSeparator(0, GUILayoutUtility.GetLastRect().yMax + 2, position.width);
            EditorGUILayout.Space(6);
        }

        // ── Template panel ────────────────────────────────────────────────
        void DrawTemplatePanel()
        {
            DrawSectionHeaderWithHelp("TEMPLATES REGISTRY", "Group Values Editor");
            EditorGUILayout.Space(4);

            GUI.backgroundColor = GVThemeManager.Current.buttonNeutral;
            if (GUILayout.Button("Refresh List", GVEditorStyles.StyleUtilButton()))
                RefreshRegistry();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(2);

            string[] names = allValuesT.ConvertAll(t => t.name).ToArray();
            int newIndex = GVEditorStyles.StyledPopup("Selected Template",
                                    selectedTemplateIndex, names, position.width);
            if (newIndex != selectedTemplateIndex && newIndex >= 0)
            {
                selectedTemplateIndex = newIndex;
                SelectTemplate(allValuesT[selectedTemplateIndex]);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            if (GVEditorStyles.ColorButton("Create Template", GVThemeManager.Current.buttonSuccess))
                CreateNewGroupValuesTemplate();
            if (selected != null &&
                GVEditorStyles.ColorButton("Delete Selected", GVThemeManager.Current.buttonDanger))
                DeleteSelectedTemplate();
            if (GVEditorStyles.ColorButton("Reset All", GVThemeManager.Current.buttonWarning))
                ResetAllGroupValuesTemplatesAndApply();
            EditorGUILayout.EndHorizontal();

            if (allValuesT.Count == 0) return;

            if (selectedTemplate != null && workingCopyTemplate != null)
            {
                EditorGUILayout.Space(5);
                GVEditorStyles.DrawSeparator(0, GUILayoutUtility.GetLastRect().yMax + 2, position.width);
                EditorGUILayout.Space(4);

                // Reference row — always visible so user can assign a GV
                Rect refRow = EditorGUILayout.GetControlRect(false, 20f);
                GVEditorStyles.DrawRowBackground(refRow);
                EditorGUI.LabelField(new Rect(refRow.x + 6, refRow.y + 2, 110, refRow.height),
                    "Reference", GVEditorStyles.StylePopupLabel());
                EditorGUI.BeginChangeCheck();
                var newRef = (GroupValues)EditorGUI.ObjectField(
                    new Rect(refRow.x + 120, refRow.y + 2, refRow.width - 126, refRow.height - 4),
                    workingCopyTemplate.groupValuesReference, typeof(GroupValues), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newRef != null && IsReferenceAlreadyUsed(newRef, workingCopyTemplate))
                        EditorUtility.DisplayDialog("Reference already used",
                            "Another template is already using this GroupValues reference.", "Ok");
                    else
                    {
                        selectedTemplate.groupValuesReference = newRef;
                        workingCopyTemplate.groupValuesReference = newRef;
                        if (newRef != null)
                        {
                            selectedTemplate.SetDefaultValuesInTemplate();
                            workingCopyTemplate.SetDefaultValuesInTemplate();
                        }
                        EditorUtility.SetDirty(selectedTemplate);
                    }
                }

                // Only show Template/GV sync buttons if reference is assigned
                if (workingCopyTemplate.groupValuesReference != null)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.BeginHorizontal();
                    if (GVEditorStyles.ColorButton("Template from GV", GVThemeManager.Current.buttonPrimary))
                    {
                        Undo.RecordObject(selectedTemplate, "Set default values in template");
                        Undo.RecordObject(workingCopyTemplate, "Set default values in template");
                        selectedTemplate.SetDefaultValuesInTemplate();
                        workingCopyTemplate.SetDefaultValuesInTemplate();
                        EditorUtility.SetDirty(selectedTemplate);
                        EditorUtility.SetDirty(workingCopyTemplate);
                        AssetDatabase.SaveAssets();
                    }
                    if (GVEditorStyles.ColorButton("Template to GV", GVThemeManager.Current.buttonSuccess))
                    {
                        Undo.RecordObject(selectedTemplate.groupValuesReference, "Set default values in GV");
                        Undo.RecordObject(workingCopyTemplate.groupValuesReference, "Set default values in GV");
                        selectedTemplate.SetDefaultValuesInSO();
                        workingCopyTemplate.SetDefaultValuesInSO();
                        EditorUtility.SetDirty(selectedTemplate.groupValuesReference);
                        EditorUtility.SetDirty(workingCopyTemplate.groupValuesReference);
                        AssetDatabase.SaveAssets();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(5);
            GVEditorStyles.DrawSeparator(0, GUILayoutUtility.GetLastRect().yMax + 2, position.width);
            EditorGUILayout.Space(6);
        }

        bool IsReferenceAlreadyUsed(GroupValues reference, GroupValuesTemplate currentTemplate)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GroupValuesTemplate"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<GroupValuesTemplate>(path);
                if (template == currentTemplate) continue;
                if (template.groupValuesReference == reference) return true;
            }
            return false;
        }

        // ── Toolbars ──────────────────────────────────────────────────────
        void DrawToolbar()
        {
            EditorGUILayout.Space(2);
            Rect bar = EditorGUILayout.GetControlRect(false, 30);
            GVEditorStyles.DrawBox(bar, GVThemeManager.Current.backgroundPanel, GVEditorStyles.C_Border);
            GUILayout.BeginHorizontal();
            if (GVEditorStyles.ColorButton("Copy From Current", GVThemeManager.Current.buttonPrimary))
                DuplicateCurrentGroupValues();
            if (GVEditorStyles.ColorButton("Apply", GVThemeManager.Current.buttonSuccess))
                Apply();
            if (GVEditorStyles.ColorButton("Reset To Defaults", GVThemeManager.Current.buttonWarning))
            {
                Undo.RecordObject(workingCopy, "Reset to Defaults");
                workingCopy.ResetToDefaults();
                EditorUtility.SetDirty(workingCopy);
                AssetDatabase.SaveAssets();
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        void DrawTemplateToolbar()
        {
            if (selectedTemplate == null || selectedTemplate.groupValuesReference == null) return;
            EditorGUILayout.Space(2);
            GUILayout.BeginHorizontal();
            if (GVEditorStyles.ColorButton("Apply", GVThemeManager.Current.buttonSuccess))
                ApplyTemplate();
            if (GVEditorStyles.ColorButton("Reset To Defaults", GVThemeManager.Current.buttonWarning))
            {
                Undo.RecordObject(selectedTemplate, "Reset to Defaults");
                Undo.RecordObject(workingCopyTemplate, "Reset to Defaults");
                selectedTemplate.ResetFields();
                workingCopyTemplate = selectedTemplate.Clone();
                EditorUtility.SetDirty(selectedTemplate);
                EditorUtility.SetDirty(workingCopyTemplate);
                AssetDatabase.SaveAssets();
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ── Editor — GroupValues ──────────────────────────────────────────
        void DrawEditor()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            List<GVField> fieldsDisplay = showTemplateValues
                ? GetTemplateForSelected(selected)?.Clone().fields
                : workingCopy.fields;

            if (fieldsDisplay == null) { EditorGUILayout.EndScrollView(); return; }

            for (int i = 0; i < fieldsDisplay.Count; i++)
            {
                var field = fieldsDisplay[i];
                if (!fieldFoldouts.ContainsKey(i)) fieldFoldouts[i] = true;
                bool expanded = fieldFoldouts[i];

                // Field header
                Rect hr = EditorGUILayout.GetControlRect(false, 24);
                GVEditorStyles.DrawBox(hr, GVEditorStyles.C_Header, GVEditorStyles.C_HeaderBdr);
                GVEditorStyles.DrawAccentBar(hr, GVEditorStyles.C_HeaderBdr);

                // Toggle
                Rect toggleR = new Rect(hr.x + 4, hr.y + 5, 16, 14);
                if (GUI.Button(toggleR, expanded ? "▾" : "▸",
                        GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr)))
                {
                    fieldFoldouts[i] = !fieldFoldouts[i];
                    expanded = fieldFoldouts[i];
                }

                // FIELD label
                Rect fieldLblR = new Rect(toggleR.xMax + 2, hr.y + 4, 36, 16);
                EditorGUI.LabelField(fieldLblR, "FIELD",
                    GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr));

                // Name
                float nameX = fieldLblR.xMax + 4;
                float nameW = hr.xMax - nameX - (showTemplateValues ? 4 : 26);
                Rect nameR = new Rect(nameX, hr.y + 4, nameW, 16);

                if (showTemplateValues)
                    EditorGUI.LabelField(nameR, field.fieldName,
                        GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr));
                else
                {
                    field.fieldName = EditorGUI.TextField(nameR, field.fieldName);
                    Rect xR = new Rect(hr.xMax - 22, hr.y + 4, 18, 16);
                    GUI.backgroundColor = GVThemeManager.Current.buttonDanger;
                    if (GUI.Button(xR, "x", GVEditorStyles.StyleDeleteButton()))
                    {
                        GUI.backgroundColor = Color.white;
                        Undo.RecordObject(workingCopy, "Delete Field");
                        workingCopy.fields.RemoveAt(i);
                        workingCopy.RebuildCache();
                        fieldFoldouts.Remove(i);
                        EditorUtility.SetDirty(workingCopy);
                        AssetDatabase.SaveAssets();
                        break;
                    }
                    GUI.backgroundColor = Color.white;
                }

                if (!expanded) { EditorGUILayout.Space(2); continue; }

                // Entries
                for (int j = 0; j < field.entries.Count; j++)
                    DrawEntry(field, j, showTemplateValues);

                if (!showTemplateValues)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    Rect addR = GUILayoutUtility.GetRect(
                        new GUIContent("+ Add Entry"), GVEditorStyles.StyleAddButton(),
                        GUILayout.ExpandWidth(false));
                    GUI.backgroundColor = GVThemeManager.Current.buttonPrimary;
                    if (GUI.Button(addR, "+ Add Entry", GVEditorStyles.StyleAddButton()))
                    {
                        int ci = i;
                        TypePickerWindow.Show(addR, VALUE_TYPE.INT, picked =>
                        {
                            Undo.RecordObject(workingCopy, "Add Entry");
                            workingCopy.fields[ci].entries.Add(new GVEntry
                            {
                                name = "NewEntry_" + workingCopy.fields[ci].entries.Count,
                                type = picked,
                                value = GVValueFactory.Create(picked)
                            });
                            workingCopy.RebuildCache();
                            EditorUtility.SetDirty(workingCopy);
                            AssetDatabase.SaveAssets();
                        });
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
            }

            if (!showTemplateValues)
            {
                EditorGUILayout.Space(2);
                GUI.backgroundColor = GVThemeManager.Current.buttonPrimary;
                if (GUILayout.Button("+ Add Field", GVEditorStyles.StyleAddButton()))
                {
                    Undo.RecordObject(workingCopy, "Add Field");
                    workingCopy.fields.Add(new GVField { fieldName = "NewField" });
                    workingCopy.RebuildCache();
                    EditorUtility.SetDirty(workingCopy);
                    AssetDatabase.SaveAssets();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }
        void DrawVersionBar()
        {
            var t = GVThemeManager.Current;
            float h = EditorGUIUtility.singleLineHeight + 6f;

            // Pin to bottom of window
            Rect bar = new Rect(0, position.height - h, position.width, h);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(bar, t.backgroundPanel);
                EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width, 1), t.separator);
            }

            float x = bar.x + 8f;
            float y = bar.y + 3f;
            float lh = EditorGUIUtility.singleLineHeight;
            float fw = 36f;
            float lw = 24f;
            float pad = 2f;

            var labelStyle = GVEditorStyles.StyleSmallLabel(t.textDim);
            GUI.Label(new Rect(x, y, 52f, lh), "Version:", labelStyle);
            x += 52f + pad;

            var numStyle = new GUIStyle(EditorStyles.numberField)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
            };

            EditorGUI.BeginChangeCheck();

            GUI.Label(new Rect(x, y, lw, lh), "Maj", GVEditorStyles.StyleSmallLabel(t.textSecondary));
            x += lw;
            int major = EditorGUI.IntField(new Rect(x, y, fw, lh), workingCopy.version?.major ?? 1, numStyle);
            x += fw + pad;

            GUI.Label(new Rect(x, y, lw, lh), "Min", GVEditorStyles.StyleSmallLabel(t.textSecondary));
            x += lw;
            int minor = EditorGUI.IntField(new Rect(x, y, fw, lh), workingCopy.version?.minor ?? 0, numStyle);
            x += fw + pad;

            GUI.Label(new Rect(x, y, lw, lh), "Pat", GVEditorStyles.StyleSmallLabel(t.textSecondary));
            x += lw;
            int patch = EditorGUI.IntField(new Rect(x, y, fw, lh), workingCopy.version?.patch ?? 0, numStyle);
            x += fw + pad;

            GUI.Label(new Rect(x, y, lw, lh), "Lbl", GVEditorStyles.StyleSmallLabel(t.textSecondary));
            x += lw;
            float labelFieldW = bar.xMax - x - 8f;
            string label = EditorGUI.TextField(new Rect(x, y, labelFieldW, lh),
                workingCopy.version?.label ?? "");

            if (EditorGUI.EndChangeCheck())
            {
                if (workingCopy.version == null)
                    workingCopy.version = new GVVersion(1, 0, 0);
                workingCopy.version.major = Mathf.Max(0, major);
                workingCopy.version.minor = Mathf.Max(0, minor);
                workingCopy.version.patch = Mathf.Max(0, patch);
                workingCopy.version.label = label;
            }
        }
        // ── Editor — Template ─────────────────────────────────────────────
        void DrawEditorTemplate()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < workingCopyTemplate.fields.Count; i++)
            {
                var field = workingCopyTemplate.fields[i];
                if (!fieldFoldouts.ContainsKey(i + 10000)) fieldFoldouts[i + 10000] = true;
                bool expanded = fieldFoldouts[i + 10000];

                Rect hr = EditorGUILayout.GetControlRect(false, 24);
                GVEditorStyles.DrawBox(hr, GVEditorStyles.C_Header, GVEditorStyles.C_HeaderBdr);
                GVEditorStyles.DrawAccentBar(hr, GVEditorStyles.C_HeaderBdr);

                Rect toggleR = new Rect(hr.x + 4, hr.y + 5, 16, 14);
                if (GUI.Button(toggleR, expanded ? "▾" : "▸",
                        GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr)))
                {
                    fieldFoldouts[i + 10000] = !fieldFoldouts[i + 10000];
                    expanded = fieldFoldouts[i + 10000];
                }

                Rect fieldLblR = new Rect(toggleR.xMax + 2, hr.y + 4, 36, 16);
                EditorGUI.LabelField(fieldLblR, "FIELD",
                    GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr));

                EditorGUI.LabelField(
                    new Rect(fieldLblR.xMax + 4, hr.y + 4, hr.xMax - fieldLblR.xMax - 8, 16),
                    field.fieldName,
                    GVEditorStyles.StyleSmallLabel(GVEditorStyles.C_HeaderBdr));

                if (!expanded) { EditorGUILayout.Space(2); continue; }

                for (int j = 0; j < field.entries.Count; j++)
                    DrawEntryTemplate(field, j);

                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── DrawEntry ─────────────────────────────────────────────────────
        void DrawEntry(GVField field, int index, bool readOnly)
        {
            var entry = field.entries[index];
            Color tCol = GVEditorStyles.GetTypeColor(entry.type);
            bool isCustom = entry.type == VALUE_TYPE.CUSTOM;

            Rect rowRect = EditorGUILayout.GetControlRect(false, 22);
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = index % 2 == 0 ? GVEditorStyles.C_RowEven : GVEditorStyles.C_RowOdd;
                EditorGUI.DrawRect(rowRect, bg);
                GVEditorStyles.DrawAccentBar(rowRect, tCol);
            }

            const float indent = 16f;
            const float badgeW = 52f;
            const float badgeH = 14f;
            float x = rowRect.x + indent;
            float y = rowRect.y + 3;

            Rect badgeR = new Rect(x, y + (16 - badgeH) * 0.5f, badgeW, badgeH);
            GVEditorStyles.DrawBadge(badgeR, entry.type.ToString(), tCol);

            if (!readOnly && GUI.Button(badgeR, GUIContent.none, GUIStyle.none))
            {
                GVEntry cap = entry;
                TypePickerWindow.Show(badgeR, cap.type, picked =>
                {
                    cap.type = picked;
                    cap.value = GVValueFactory.Create(picked);
                    Repaint();
                });
            }

            Rect nameR = new Rect(badgeR.xMax + 4, y, 120, 16);
            if (readOnly)
            {
                EditorGUI.LabelField(nameR, entry.name,
                    GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textSecondary));
            }
            else
            {
                string prevName = entry.name;
                entry.name = EditorGUI.TextField(nameR, entry.name);

                // Duplicate key warning — check inside workingCopy, not the SO
                if (!string.IsNullOrEmpty(entry.name) &&
                    IsDuplicateKeyInCopy(entry.name, entry, workingCopy))
                {
                    Rect warnR = new Rect(rowRect.x, rowRect.yMax, rowRect.width, 16);
                    EditorGUILayout.GetControlRect(false, 16); // reserve space
                    DrawDuplicateKeyWarning(warnR, entry.name);
                }
            }

            if (!isCustom)
            {
                float valX = nameR.xMax + 4;
                float valW = readOnly ? rowRect.xMax - valX : rowRect.xMax - valX - 24;
                EditorGUI.BeginDisabledGroup(readOnly);
                DrawEntryValue(entry, new Rect(valX, y, valW, 16));
                EditorGUI.EndDisabledGroup();
            }

            if (!readOnly)
            {
                Rect xR = new Rect(rowRect.xMax - 20, y, 18, 16);
                GUI.backgroundColor = GVThemeManager.Current.buttonDanger;
                if (GUI.Button(xR, "x", GVEditorStyles.StyleDeleteButton()))
                {
                    GUI.backgroundColor = Color.white;
                    Undo.RecordObject(workingCopy, "Delete Entry");
                    field.entries.RemoveAt(index);
                    workingCopy.RebuildCache();
                    EditorUtility.SetDirty(workingCopy);
                    AssetDatabase.SaveAssets();
                    return;
                }
                GUI.backgroundColor = Color.white;
            }

            if (isCustom)
                DrawCustomEntryExpanded(entry, rowRect.x, rowRect.xMax,
                    classReadOnly: readOnly, valuesReadOnly: readOnly);
        }

        // ── DrawEntryTemplate ─────────────────────────────────────────────
        void DrawEntryTemplate(GVField field, int index)
        {
            var entry = field.entries[index];
            Color tCol = GVEditorStyles.GetTypeColor(entry.type);
            bool isCustom = entry.type == VALUE_TYPE.CUSTOM;

            float rowH = isCustom ? 22f : 22f;
            Rect rowRect = EditorGUILayout.GetControlRect(false, rowH);
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = index % 2 == 0 ? GVEditorStyles.C_RowEven : GVEditorStyles.C_RowOdd;
                EditorGUI.DrawRect(rowRect, bg);
                GVEditorStyles.DrawAccentBar(rowRect, tCol);
            }

            const float badgeW = 52f;
            const float badgeH = 14f;
            float x = rowRect.x + 16f;
            float y = rowRect.y + 3;

            // Badge only — no button, type is read-only in templates
            Rect badgeR = new Rect(x, y + (16 - badgeH) * 0.5f, badgeW, badgeH);
            GVEditorStyles.DrawBadge(badgeR, entry.type.ToString(), tCol);

            Rect nameR = new Rect(badgeR.xMax + 4, y, 120, 16);
            EditorGUI.LabelField(nameR, entry.name,
                GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textSecondary));

            if (!isCustom)
            {
                Rect valR = new Rect(nameR.xMax + 4, y, rowRect.xMax - nameR.xMax - 8, 16);
                DrawEntryValue(entry, valR);
            }
            else
            {
                // CUSTOM: show expanded fields below the header row
                DrawCustomEntryExpanded(entry, rowRect.x, rowRect.xMax,
                    classReadOnly: true, valuesReadOnly: false);
            }
        }

        // ── DrawCustomEntryExpanded ───────────────────────────────────────
        void DrawCustomEntryExpanded(GVEntry entry, float rowX, float rowXMax, bool classReadOnly, bool valuesReadOnly = false)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float x = rowX + 6;
            float w = rowXMax - rowX - 12;

            // Class picker row
            Rect pickerRect = EditorGUILayout.GetControlRect(false, line);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(pickerRect, GVEditorStyles.C_Body);

            string current = entry.customTypeName ?? "";
            Color badgeCol = CustomGVDataRegistry.Entries.TryGetValue(current, out var ec)
                              ? ec.Color : GVThemeManager.Current.textDim;
            string badgeLabel = string.IsNullOrEmpty(current) ? "— none —" : current;

            Rect labelR = new Rect(x, pickerRect.y + 1, 60f, line);
            Rect badgeR = new Rect(x + 64f, pickerRect.y + 1, w - 64f, line);

            EditorGUI.LabelField(labelR, "CLASS",
                GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textDim));
            GVEditorStyles.DrawBadge(badgeR, badgeLabel, badgeCol);

            if (!classReadOnly && GUI.Button(badgeR, GUIContent.none, GUIStyle.none))
            {
                GVEntry cap = entry;
                CustomDataPickerWindow.Show(badgeR, current, picked =>
                {
                    cap.customTypeName = picked;
                    if (picked.Length > 0 &&
                        CustomGVDataRegistry.Types.TryGetValue(picked, out var t))
                    {
                        string json = UnityEngine.JsonUtility.ToJson(
                                          System.Activator.CreateInstance(t));
                        cap.value = GVValueFactory.Create(VALUE_TYPE.CUSTOM);
                        cap.value.SetValue(json);
                    }
                });
            }

            string typeName = entry.customTypeName ?? "";
            if (string.IsNullOrEmpty(typeName)) return;
            if (!CustomGVDataRegistry.Types.TryGetValue(typeName, out var type)) return;

            string rawJson = entry.value?.GetValue() as string ?? "{}";
            var fromJson = typeof(UnityEngine.JsonUtility)
                .GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static,
                           null, new[] { typeof(string) }, null)
                ?.MakeGenericMethod(type);

            object inst;
            try
            {
                inst = fromJson?.Invoke(null, new object[] { rawJson })
                            ?? System.Activator.CreateInstance(type);
            }
            catch { inst = System.Activator.CreateInstance(type); }

            // Apply load-time attributes: reset DontSave fields, clamp values
            if (inst != null)
            {
                foreach (var fi in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (GVFieldAttributeHelper.IsDontSave(fi))
                    {
                        var def = fi.FieldType.IsValueType
                            ? System.Activator.CreateInstance(fi.FieldType) : null;
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

            bool dirty = false;
            foreach (var field in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                bool isPub = field.IsPublic;
                bool hasSF = field.GetCustomAttribute<SerializeField>() != null;
                bool hasNS = field.GetCustomAttribute<System.NonSerializedAttribute>() != null;
                if ((!isPub && !hasSF) || hasNS) continue;

                // [DontSave] — skip entirely
                if (GVFieldAttributeHelper.IsDontSave(field)) continue;

                // Skip WriteN counter fields
                if (field.Name.StartsWith("__") && field.Name.EndsWith("WriteCount")) continue;

                bool isReadOnly = GVFieldAttributeHelper.IsReadOnly(field);
                bool isWriteOnce = GVFieldAttributeHelper.IsWriteOnce(field);
                int maxW = GVFieldAttributeHelper.GetMaxWrites(field);
                int remaining = maxW >= 0
                    ? GVFieldAttributeHelper.GetRemainingWrites(field, inst) : -1;

                // Display name — use SaveAs key
                string displayName = GVFieldAttributeHelper.GetJsonKey(field);

                float fieldH = GVEditorStyles.ReflectedFieldHeightWithInstance(
                                   field.FieldType, field.GetValue(inst));
                Rect fieldRow = EditorGUILayout.GetControlRect(false, fieldH);
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(fieldRow, GVEditorStyles.C_Body);

                var t = GVThemeManager.Current;

                // Layout: [🔄] [label] [value field] [badge]
                const float lw = 110f;
                const float resetW = 18f;
                const float badgeW = 28f;
                const float pad = 4f;

                float curX = x + 8f;

                // 🔄 Reset button for WriteN
                if (maxW >= 0 || isWriteOnce)
                {
                    Rect resetR = new Rect(curX, fieldRow.y + 1f, resetW, line);
                    bool exhausted = (maxW >= 0 && remaining == 0) ||
                                     (isWriteOnce && !GVFieldAttributeHelper.CanWrite(field, inst));
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
                        GVFieldAttributeHelper.ResetWriteCount(field, inst);
                        if (isWriteOnce)
                        {
                            var def = field.FieldType.IsValueType
                                ? System.Activator.CreateInstance(field.FieldType) : null;
                            field.SetValue(inst, def);
                        }
                        dirty = true;
                    }
                    GUI.backgroundColor = Color.white;
                    curX += resetW + pad;
                }
                else
                {
                    curX += pad; // keep alignment
                }

                Rect lR = new Rect(curX, fieldRow.y + 1f, lw, line);
                curX += lw + pad;

                // Badge area — 🔒 or ✎n (remaining)
                float valueW = rowXMax - curX - badgeW - pad - 8f;
                Rect vR = new Rect(curX, fieldRow.y + 1f, valueW, line);
                Rect fieldBadgeR = new Rect(curX + valueW + pad, fieldRow.y + 2f, badgeW, line - 4f);

                // Range label — shown as small prefix before the field name
                string rangeLabel = GVFieldAttributeHelper.GetRangeLabel(field);
                if (!string.IsNullOrEmpty(rangeLabel))
                {
                    // Draw range label in smaller font to the left
                    const float rlW = 54f;
                    Rect rlR = new Rect(lR.x, lR.y, rlW, lR.height);
                    Rect nmR = new Rect(lR.x + rlW, lR.y, lR.width - rlW, lR.height);
                    var rlStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize = 8,
                        alignment = TextAnchor.MiddleLeft,
                    };
                    rlStyle.normal.textColor = t.accent;
                    EditorGUI.LabelField(rlR, rangeLabel, rlStyle);
                    EditorGUI.LabelField(nmR, displayName,
                        GVEditorStyles.StyleSmallLabel(t.textDim));
                }
                else
                {
                    EditorGUI.LabelField(lR, displayName,
                        GVEditorStyles.StyleSmallLabel(t.textDim));
                }

                // Draw badge
                if (isReadOnly)
                {
                    GVEditorStyles.DrawBadge(fieldBadgeR, "🔒", t.textDim);
                }
                else if (isWriteOnce)
                {
                    Color bc = GVFieldAttributeHelper.CanWrite(field, inst) ? t.valid : t.invalid;
                    GVEditorStyles.DrawBadge(fieldBadgeR, "✎¹", bc);
                }
                else if (maxW >= 0)
                {
                    Color bc = remaining > 0 ? t.valid : t.invalid;
                    GVEditorStyles.DrawBadge(fieldBadgeR, $"✎{remaining}", bc);
                }

                bool isNestedSerializable = field.FieldType.IsSerializable
                    && !field.FieldType.IsPrimitive
                    && !field.FieldType.IsEnum
                    && field.FieldType != typeof(string)
                    && !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)
                    && !(field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>));

                EditorGUI.BeginDisabledGroup(valuesReadOnly || isReadOnly);
                object oldV = field.GetValue(inst);
                Rect vRH = new Rect(vR.x, vR.y, vR.width,
                                  GVEditorStyles.ReflectedFieldHeightWithInstance(field.FieldType, oldV));
                if (isNestedSerializable) EditorGUI.BeginChangeCheck();
                object newV = GVEditorStyles.DrawReflectedField(vRH, "", field.FieldType, oldV);
                bool nestedChanged = isNestedSerializable && EditorGUI.EndChangeCheck();
                EditorGUI.EndDisabledGroup();

                bool isList = field.FieldType.IsGenericType &&
                  field.FieldType.GetGenericTypeDefinition() == typeof(List<>);

                if (!isReadOnly && !valuesReadOnly && newV != null && (isList || nestedChanged || !newV.Equals(oldV)) &&
                    Event.current.type != EventType.Layout &&
                    Event.current.type != EventType.Repaint)
                {
                    newV = GVFieldAttributeHelper.ClampValue(field, newV);
                    if (GVFieldAttributeHelper.CanWrite(field, inst))
                    {
                        field.SetValue(inst, newV);
                        GVFieldAttributeHelper.IncrementWriteCount(field, inst);
                        dirty = true;
                    }
                }
            }

            if (dirty)
                entry.value.SetValue(UnityEngine.JsonUtility.ToJson(inst));
        }

        object DrawWindowField(Rect r, System.Type ft, object cur)
        {
            if (ft == typeof(bool)) return EditorGUI.Toggle(r, cur is bool b ? b : false);
            if (ft == typeof(int)) return EditorGUI.IntField(r, cur is int iv ? iv : 0);
            if (ft == typeof(float)) return EditorGUI.FloatField(r, cur is float f ? f : 0f);
            if (ft == typeof(double)) return EditorGUI.DoubleField(r, cur is double d ? d : 0.0);
            if (ft == typeof(long)) return EditorGUI.LongField(r, cur is long l ? l : 0L);
            if (ft == typeof(string)) return EditorGUI.TextField(r, cur as string ?? "");
            if (ft == typeof(Vector2)) return EditorGUI.Vector2Field(r, GUIContent.none,
                                           cur is Vector2 v2 ? v2 : Vector2.zero);
            if (ft == typeof(Vector3)) return EditorGUI.Vector3Field(r, GUIContent.none,
                                           cur is Vector3 v3 ? v3 : Vector3.zero);
            EditorGUI.LabelField(r, $"({ft.Name})",
                GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textDim));
            return cur;
        }

        // ── DrawEntryValue ────────────────────────────────────────────────
        void DrawEntryValue(GVEntry entry, Rect rect)
        {
            if (entry.value == null) return;
            object raw = entry.value.GetValue();

            switch (entry.type)
            {
                case VALUE_TYPE.BOOL:
                    {
                        bool v = raw is bool b ? b : false;
                        bool n = EditorGUI.Toggle(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.INT:
                    {
                        int v = raw is int i ? i : 0;
                        int n = EditorGUI.IntField(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.FLOAT:
                    {
                        float v = raw is float f ? f : 0f;
                        float n = EditorGUI.FloatField(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.DOUBLE:
                    {
                        double v = raw is double d ? d : 0.0;
                        double n = EditorGUI.DoubleField(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.LONG:
                    {
                        long v = raw is long l ? l : 0L;
                        long n = EditorGUI.LongField(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.SHORT:
                    {
                        short v = raw is short s ? s : (short)0;
                        short n = (short)EditorGUI.IntField(rect, v);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.BYTE:
                    {
                        byte v = raw is byte by ? by : (byte)0;
                        byte n = (byte)Mathf.Clamp(EditorGUI.IntField(rect, v), 0, 255);
                        if (n != v) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.CHAR:
                    {
                        string cur = raw is char c ? c.ToString() : "";
                        EditorGUI.BeginChangeCheck();
                        string next = EditorGUI.TextField(rect, cur);
                        if (EditorGUI.EndChangeCheck() && next.Length > 0)
                            entry.value.SetValue(next[0]);
                        break;
                    }
                case VALUE_TYPE.STRING:
                    {
                        string v = raw as string ?? "";
                        EditorGUI.BeginChangeCheck();
                        string n = EditorGUI.TextField(rect, v);
                        if (EditorGUI.EndChangeCheck()) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.VECTOR2:
                    {
                        Vector2 v = raw is Vector2 v2 ? v2 : Vector2.zero;
                        EditorGUI.BeginChangeCheck();
                        Vector2 n = EditorGUI.Vector2Field(rect, GUIContent.none, v);
                        if (EditorGUI.EndChangeCheck()) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.VECTOR3:
                    {
                        Vector3 v = raw is Vector3 v3 ? v3 : Vector3.zero;
                        EditorGUI.BeginChangeCheck();
                        Vector3 n = EditorGUI.Vector3Field(rect, GUIContent.none, v);
                        if (EditorGUI.EndChangeCheck()) entry.value.SetValue(n);
                        break;
                    }
                case VALUE_TYPE.CUSTOM:
                    EditorGUI.LabelField(rect, raw as string ?? "",
                        GVEditorStyles.StyleSmallLabel(GVThemeManager.Current.textDim));
                    break;
            }
        }

        // ── Selection ─────────────────────────────────────────────────────
        void Select(GroupValues gv)
        {
            if (gv == null) return;
            selected = gv;
            workingCopy = gv.Clone();
            fieldFoldouts.Clear();
        }

        void SelectTemplate(GroupValuesTemplate t)
        {
            if (t == null) return;
            selectedTemplate = t;
            workingCopyTemplate = t.Clone();
            fieldFoldouts.Clear();
        }

        GroupValuesTemplate GetTemplateForSelected(GroupValues gv)
            => allValuesT.Find(t => t.groupValuesReference == gv);

        // ── Apply / Reset ─────────────────────────────────────────────────
        void Apply()
        {
            // Suggest version bump if structure changed
            var suggested = GVVersion.SuggestBump(selected, workingCopy, selected.version);
            if (suggested != null)
            {
                bool accept = EditorUtility.DisplayDialog(
                    "Version Bump Suggested\n",
                    $"Structural changes detected in '{selected.name}'.\n" +
                    $"Current version: {selected.version}\n" +
                    $"Suggested version: {suggested}\n" +
                    $"Accept the suggested bump?",
                    "Accept", "Keep Current");

                if (accept)
                    workingCopy.version = suggested;
            }

            selected.CopyFrom(workingCopy);
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
            SaveJsonForAsset(selected);
        }

        void ApplyTemplate()
        {
            selectedTemplate.CopyFrom(workingCopyTemplate);
            EditorUtility.SetDirty(selectedTemplate);
            AssetDatabase.SaveAssets();
        }

        void SaveJsonForAsset(GroupValues asset)
        {
            loader.ChangeAssetName(asset.name);
            loader.AutoResolveFromResources();
            loader.SaveValues(asset);
        }

        void ResetAllGroupValuesAndApply()
        {
            if (!EditorUtility.DisplayDialog("Reset ALL GroupValues",
                "This will reset ALL GroupValues to defaults and overwrite JSON.\nThis cannot be undone.",
                "Yes", "Cancel")) return;
            foreach (var gv in GroupValuesRegistry.GetAll())
            {
                gv.ResetToDefaults();
                EditorUtility.SetDirty(gv);
                ApplyJsonForGroupValues(gv);
            }
            AssetDatabase.SaveAssets();
        }

        void ResetAllGroupValuesTemplatesAndApply()
        {
            if (!EditorUtility.DisplayDialog("Reset ALL GroupValuesTemplates",
                "This will reset ALL GroupValues to defaults as written in all templates.",
                "Yes", "Cancel")) return;
            foreach (var gv in GroupValuesRegistry.GetAllTemplates())
            {
                gv.ResetFields();
                EditorUtility.SetDirty(gv);
            }

            workingCopyTemplate = selectedTemplate.Clone();
            AssetDatabase.SaveAssets();
        }

        void ApplyJsonForGroupValues(GroupValues gv)
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(gv));
            loader.ChangeAssetName(name);
            typeof(ALoader)
                .GetField("values", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(loader, gv);
            loader.SaveValues();
        }

        // ── Create / Delete / Duplicate ───────────────────────────────────
        void DuplicateCurrentGroupValues()
        {
            if (selected == null) return;
            string path = AssetDatabase.GetAssetPath(selected);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path.Replace(".asset", "_Copy.asset"));
            var copy = ScriptableObject.CreateInstance<GroupValues>();
            copy.CopyFrom(selected);
            AssetDatabase.CreateAsset(copy, newPath);
            AssetDatabase.SaveAssets();
            selected = copy;
        }

        void CreateNewGroupValues()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create GroupValues", "NewGroupValues", "asset", "Save GroupValues");
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GroupValues>(), path);
            AssetDatabase.SaveAssets();
            RefreshRegistry();
        }

        void CreateNewGroupValuesTemplate()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create GroupValuesTemplate", "NewGroupValuesTemplate", "asset", "Save GroupValuesTemplate");
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GroupValuesTemplate>(), path);
            AssetDatabase.SaveAssets();
            RefreshRegistry();
        }

        void DeleteSelected()
        {
            if (!EditorUtility.DisplayDialog("Delete GroupValues?",
                $"Delete {selected.name}?\nThis cannot be undone.", "Yes", "Cancel")) return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selected));
            AssetDatabase.SaveAssets();
            selected = null; workingCopy = null; selectedIndex = -1;
            RefreshRegistry();
        }

        // ── Duplicate key helpers ─────────────────────────────────────────
        static bool IsDuplicateKeyInCopy(string key, GVEntry self, GroupValues copy)
        {
            int count = 0;
            foreach (var field in copy.fields)
                foreach (var e in field.entries)
                    if (e.name == key) { count++; if (count > 1 || e != self) return true; }
            return false;
        }

        static void DrawDuplicateKeyWarning(Rect rect, string key)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(GVThemeManager.Current.buttonDanger.r, GVThemeManager.Current.buttonDanger.g, GVThemeManager.Current.buttonDanger.b, 0.35f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height),
                                   GVThemeManager.Current.invalid);
            }
            var style = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            style.normal.textColor = GVThemeManager.Current.warning;
            GUI.Label(new Rect(rect.x + 6, rect.y, rect.width - 6, rect.height),
                      $"⚠  Key '{key}' already exists.", style);
        }

        void DeleteSelectedTemplate()
        {
            if (!EditorUtility.DisplayDialog("Delete GroupValuesTemplate?",
                $"Delete {selectedTemplate.name}?\nThis cannot be undone.", "Yes", "Cancel")) return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selectedTemplate));
            AssetDatabase.SaveAssets();
            selectedTemplate = null; workingCopyTemplate = null; selectedTemplateIndex = -1;
            RefreshRegistry();
        }
    }
}
#endif