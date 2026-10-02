using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using GVLoadSystem.GVEditor;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GVLoadSystem.Core
{
    /// <summary>
    /// Reference to a specific GVEntry inside a GroupValues.
    /// The reference is stored by key string. If the key is renamed in the GV,
    /// a warning appears in the inspector and console until re-linked.
    ///
    /// Usage:
    ///   [SerializeField] GVEntryReference _speed;
    ///   float speed = _speed.Get<float>();
    ///   _speed.Set(10f);
    /// </summary>
    [Serializable]
    public partial class GVEntryReference
    {
        [SerializeField] GroupValues _groupValues;
        [SerializeField] string _fieldName = "";
        [SerializeField] string _entryKey = "";
        [SerializeField] string _path = "";
        [NonSerialized] int _lastGVInstanceID;

        // ── Cache ─────────────────────────────────────────────────────────
        [NonSerialized] GVEntry _cached;
        [NonSerialized] bool _validated;      // whether we've logged warning this session
        [NonSerialized] string _lastCheckedKey; // tracks last key we warned about

        // ── Properties ────────────────────────────────────────────────────
        public GroupValues GroupValues => _groupValues;
        public string FieldName => _fieldName;
        public string EntryKey => _entryKey;
        public string Path => _path;
        public bool HasPath => !string.IsNullOrEmpty(_path);

        public bool IsValid
        {
            get
            {
                Resolve(log: false, context: null);
                return _cached != null;
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Gets the value. Pass 'this' as context for better warning messages.</summary>
        public T Get<T>(UnityEngine.Object context = null)
        {
            Resolve(log: true, context: context);
            if (_cached?.value == null) return default;
            var raw = _cached.value.GetValue();

            if (HasPath && raw is string j && _cached.type == VALUE_TYPE.CUSTOM)
                return GetFromPath<T>(j);
            // Direct type match
            if (raw is T t) return t;

            // JSON deserialization for custom/complex types stored as string
            if (raw is string json &&
                !typeof(T).IsPrimitive &&
                typeof(T) != typeof(string))
            {
                try
                {
                    var instance = Activator.CreateInstance<T>();
                    JsonUtility.FromJsonOverwrite(json, instance);
                    return instance;
                }
                catch { return default; }
            }

            // Scalar conversion
            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return default; }
        }

        /// <summary>Sets the value. Pass 'this' as context for better warning messages.</summary>
        public void Set<T>(T value, UnityEngine.Object context = null)
        {
            Resolve(log: true, context: context);
            //Debug.Log($"[GVEntryReference] Set: cached={_cached?.name} value={value} type={typeof(T)}");

            if (_cached?.value == null) return;


            if (HasPath && _cached.type == VALUE_TYPE.CUSTOM)
            {
                SetFromPath(value);
                return;
            }

            _groupValues.SetValue(_entryKey, value);

#if UNITY_EDITOR
            if (_groupValues != null)
                UnityEditor.EditorUtility.SetDirty(_groupValues);
#endif
            //Debug.Log($"[GVEntryReference] After Set: cached hash={_cached.GetHashCode()} value hash={_cached.value.GetHashCode()} value in GV direct={_groupValues.GetValue<int>(_entryKey)}");
        }

        public VALUE_TYPE GetValueType()
        {
            Resolve(log: false);
            if (_cached == null) return VALUE_TYPE.STRING;
            if (HasPath && _cached.type == VALUE_TYPE.CUSTOM)
            {
                var leafType = ResolvePathType();
                return leafType != null ? CSharpTypeToValueType(leafType) : VALUE_TYPE.STRING;
            }
            return _cached.type;
        }

        /// <summary>
        /// Clears the cache so the next access re-resolves from the GV.
        /// </summary>
        public void Invalidate()
        {
            _cached = null;
            _validated = false;
            _lastCheckedKey = null;
        }

        /// <summary>
        /// Forces re-resolution. Useful to call in OnEnable to ensure
        /// the reference is valid before using it.
        /// </summary>
        public void ResetEntry(UnityEngine.Object context = null)
        {
            _cached = null;
            _validated = false;
            Resolve(log: true, context: context);
        }

        /// <summary>
        /// If _cached is valid but _entryKey is stale, syncs the key to the
        /// cached entry's current name. Returns true if the fix was applied.
        /// </summary>
        public bool TryAutoFix()
        {
            if (_cached == null) return false;
            if (_cached.name == _entryKey) return false;

            _entryKey = _cached.name;
            _validated = true;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_groupValues);
#endif
            return true;
        }
        // ── Path navigation ───────────────────────────────────────────────

        T GetFromPath<T>(string json)
        {
            if (string.IsNullOrEmpty(_cached.customTypeName)) return default;
            var rootType = ResolveCustomType(_cached.customTypeName);
            if (rootType == null) return default;

            object inst;
            try
            {
                inst = Activator.CreateInstance(rootType);
                JsonUtility.FromJsonOverwrite(json, inst);
            }
            catch { return default; }

            object leaf = NavigatePath(inst, _path.Split('/'));
            if (leaf == null) return default;

            if (leaf is T direct) return direct;
            try { return (T)Convert.ChangeType(leaf, typeof(T)); }
            catch { return default; }
        }

        void SetFromPath<T>(T value)
        {
            if (string.IsNullOrEmpty(_cached.customTypeName)) return;
            var rootType = ResolveCustomType(_cached.customTypeName);
            if (rootType == null) return;

            string json = _cached.value.GetValue() as string ?? "{}";
            object inst;
            try
            {
                inst = Activator.CreateInstance(rootType);
                JsonUtility.FromJsonOverwrite(json, inst);
            }
            catch { return; }

            if (!SetAtPath(inst, _path.Split('/'), value)) return;
            _cached.value.SetValue(JsonUtility.ToJson(inst));
        }

        /// <summary>Resolves the C# type at the end of the path.</summary>
        Type ResolvePathType()
        {
            if (_cached == null || string.IsNullOrEmpty(_cached.customTypeName)) return null;
            var rootType = ResolveCustomType(_cached.customTypeName);
            if (rootType == null) return null;
            return NavigatePathType(rootType, _path.Split('/'));
        }

        /// <summary>
        /// Resolves a custom type by name. Checks registry first, then falls back
        /// to scanning all loaded assemblies — covers GroupValuesWrapper types that
        /// may not be registered yet.
        /// </summary>
        internal static Type ResolveCustomType(string typeName)
        {
            // 1. Try registry first (fastest)
            if (CustomGVDataRegistry.Types.TryGetValue(typeName, out var t)) return t;

            // 2. Fallback — scan all loaded assemblies by type name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = asm.GetType(typeName);
                if (found != null) return found;
            }

            // 3. Try simple name match (class name without namespace)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                        if (type.Name == typeName) return type;
                }
                catch { /* skip assemblies that fail to load */ }
            }

            Debug.LogWarning($"[GVEntryReference] Could not resolve type '{typeName}'.");
            return null;
        }

        // ── Reflection helpers ────────────────────────────────────────────

        static object NavigatePath(object obj, string[] segments)
        {
            foreach (var seg in segments)
            {
                if (obj == null) return null;
                var fi = GetField(obj.GetType(), seg);
                if (fi == null) return null;
                obj = fi.GetValue(obj);
            }
            return obj;
        }

        static bool SetAtPath<T>(object root, string[] segments, T value)
        {
            if (segments.Length == 0) return false;

            // Navigate to parent of last segment
            object parent = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (parent == null) return false;
                var fi = GetField(parent.GetType(), segments[i]);
                if (fi == null) return false;
                parent = fi.GetValue(parent);
            }

            if (parent == null) return false;
            var lastField = GetField(parent.GetType(), segments[segments.Length - 1]);
            if (lastField == null) return false;

            try
            {
                object converted = Convert.ChangeType(value, lastField.FieldType);
                lastField.SetValue(parent, converted);

                // Structs need to be boxed back up the chain
                PropagateStructChanges(root, segments, parent);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GVEntryReference] SetAtPath failed on '{lastField.Name}' " +
                                 $"(type {lastField.FieldType.Name}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// After modifying a struct field, we need to propagate the change back up
        /// because structs are value types and modifications on a copy don't affect the original.
        /// </summary>
        static void PropagateStructChanges(object root, string[] segments, object modifiedParent)
        {
            if (segments.Length <= 1) return;

            // Walk from root back down, re-setting each struct along the path
            object current = root;
            var parents = new List<(object obj, FieldInfo fi)>();

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var fi = GetField(current.GetType(), segments[i]);
                if (fi == null) return;
                parents.Add((current, fi));
                current = fi.GetValue(current);
            }

            // Set from deepest parent upward
            object toSet = modifiedParent;
            for (int i = parents.Count - 1; i >= 0; i--)
            {
                var (obj, fi) = parents[i];
                fi.SetValue(obj, toSet);
                toSet = obj;
            }
        }

        static Type NavigatePathType(Type rootType, string[] segments)
        {
            Type current = rootType;
            foreach (var seg in segments)
            {
                if (current == null) return null;
                var fi = GetField(current, seg);
                if (fi == null) return null;
                current = fi.FieldType;
            }
            return current;
        }

        static FieldInfo GetField(Type type, string name)
            => type.GetField(name,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static VALUE_TYPE CSharpTypeToValueType(Type t)
        {
            if (t == typeof(bool)) return VALUE_TYPE.BOOL;
            if (t == typeof(int)) return VALUE_TYPE.INT;
            if (t == typeof(float)) return VALUE_TYPE.FLOAT;
            if (t == typeof(double)) return VALUE_TYPE.DOUBLE;
            if (t == typeof(long)) return VALUE_TYPE.LONG;
            if (t == typeof(short)) return VALUE_TYPE.SHORT;
            if (t == typeof(byte)) return VALUE_TYPE.BYTE;
            if (t == typeof(char)) return VALUE_TYPE.CHAR;
            if (t == typeof(string)) return VALUE_TYPE.STRING;
            if (t == typeof(Vector2)) return VALUE_TYPE.VECTOR2;
            if (t == typeof(Vector3)) return VALUE_TYPE.VECTOR3;
            return VALUE_TYPE.CUSTOM;
        }
        // ── Resolution ────────────────────────────────────────────────────
        void Resolve(bool log, UnityEngine.Object context = null)
        {

            if (_cached != null)
            {
                // Check if SO was reloaded (instanceID changes on reload)
                int currentID = _groupValues != null ? _groupValues.GetInstanceID() : 0;
                if (currentID != _lastGVInstanceID)
                {
                    // SO was reloaded — invalidate cache
                    _cached = null;
                    _lastGVInstanceID = currentID;

                    // fall through to re-resolve
                }
                else if (_cached.name == _entryKey)
                {
                    return; // still valid
                }
                else
                {
                    // Entry was renamed
                    _entryKey = _cached.name;
                    _validated = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(_groupValues);
#endif
                    return;
                }
            }

            if (_groupValues == null || string.IsNullOrEmpty(_entryKey)) return;

            bool alreadyWarned = _validated && _lastCheckedKey == _entryKey;

            foreach (var field in _groupValues.fields)
                foreach (var entry in field.entries)
                    if (entry.name == _entryKey)
                    {
                        _cached = entry;
                        _validated = true;
                        _lastCheckedKey = _entryKey;
                        _lastGVInstanceID = _groupValues.GetInstanceID();
                        return;
                    }

            // Not found — truly missing (deleted, not just renamed)
            if (log && !alreadyWarned)
            {
                _validated = true;
                _lastCheckedKey = _entryKey;

                // Log with both the scene object (context) and the GV asset so both
                // are pingable in the console
                string who = context != null ? $" (used by '{context.name}')" : "";
                Debug.LogWarning(
                    $"[GVEntryReference] Key '{_entryKey}' not found in " +
                    $"GroupValues '{_groupValues.name}'{who}. " +
                    $"The entry may have been deleted. Re-link it in the inspector.",
                    (UnityEngine.Object)(context != null ? context : _groupValues));
            }
        }

#if UNITY_EDITOR
        public void SetTarget(GroupValues gv, string fieldName, string key, string path = "")
        {
            _groupValues = gv;
            _fieldName = fieldName;
            _entryKey = key;
            _path = path;
            _cached = null;
            _validated = false;
            Resolve(log: false);
        }

        public string DisplayLabel
        {
            get
            {
                if (_groupValues == null) return "— not set —";
                if (string.IsNullOrEmpty(_entryKey)) return $"{_groupValues.name} / —";
                string base_ = IsValid
                    ? $"{_groupValues.name} / {_entryKey}"
                    : $"⚠  {_groupValues.name} / {_entryKey} (not found)";
                if (HasPath) base_ += $"  ›  {_path.Replace("/", " › ")}";
                return base_;
            }
        }
        public bool HasWarning =>
            _groupValues != null &&
            !string.IsNullOrEmpty(_entryKey) &&
            !IsValid;
        public bool HasPathWarning
        {
            get
            {
                if (!HasPath || !IsValid || _cached.type != VALUE_TYPE.CUSTOM) return false;
                return ResolvePathType() == null;
            }
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(GVEntryReference))]
    internal class GVEntryReferenceDrawer : PropertyDrawer
    {
        static readonly Color C_Valid = new Color(0.20f, 0.75f, 0.35f);
        static readonly Color C_Invalid = new Color(0.80f, 0.25f, 0.15f);
        static readonly Color C_Warn = new Color(0.90f, 0.60f, 0.10f);

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        {
            var r = GetRef(prop);
            float h = EditorGUIUtility.singleLineHeight;
            if (r == null) return h;
            if (r.HasWarning || r.HasPathWarning) h += EditorGUIUtility.singleLineHeight + 2;
            // Path row for CUSTOM entries
            if (r.IsValid && r.GetValueType() == VALUE_TYPE.CUSTOM ||
                (r.IsValid && r.HasPath))
                h += EditorGUIUtility.singleLineHeight + 2;
            return h;
        }

        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            var t = GVThemeManager.Current;
            EditorGUI.BeginProperty(pos, label, prop);

            float lw = EditorGUIUtility.labelWidth;
            float lh = EditorGUIUtility.singleLineHeight;
            Rect lR = new Rect(pos.x, pos.y, lw, lh);
            Rect fR = new Rect(pos.x + lw + 2, pos.y, pos.width - lw - 2, lh);

            EditorGUI.LabelField(lR, label);

            var refObj = GetRef(prop);
            bool warn = refObj != null && refObj.HasWarning;

            // Status dot
            Color dotColor = refObj == null || refObj.GroupValues == null ? t.textDim :
                                warn ? t.warning : t.valid;
            EditorGUI.DrawRect(new Rect(fR.x, fR.y + 4, 8, 8), dotColor);

            // Entry picker button
            string display = refObj?.DisplayLabel ?? "— not set —";
            GUIStyle style = warn
                ? new GUIStyle(EditorStyles.popup) { normal = { textColor = t.warning } } : EditorStyles.popup;


            Rect btnR = new Rect(fR.x + 12, fR.y, fR.width - 12, lh);
            if (GUI.Button(btnR, display, style))
                ShowEntryPicker(prop, refObj);

            float y = pos.y + lh + 2;

            // Path row — shown when entry is CUSTOM
            if (refObj != null && refObj.IsValid &&
                refObj.GetValueType() == VALUE_TYPE.CUSTOM)
            {
                Rect pathLabelR = new Rect(pos.x + lw + 2, y, 40, lh);
                Rect pathBtnR = new Rect(pos.x + lw + 44, y, pos.width - lw - 48, lh);

                EditorGUI.LabelField(pathLabelR, "Path",
                    GVEditorStyles.StyleSmallLabel(t.textSecondary));

                bool pathWarn = refObj.HasPathWarning;
                string pathLabel = string.IsNullOrEmpty(refObj.Path)
                    ? "— whole entry —"
                    : refObj.Path.Replace("/", " › ");
                if (pathWarn) pathLabel = "⚠  " + pathLabel + " (field not found)";

                GUIStyle pathStyle = pathWarn
                    ? new GUIStyle(EditorStyles.popup) { normal = { textColor = t.warning } }
                    : EditorStyles.popup;

                if (GUI.Button(pathBtnR, pathLabel, pathStyle))
                    ShowPathPicker(prop, refObj);

                y += lh + 2;
            }

            // Warning row
            if (warn || (refObj != null && refObj.HasPathWarning))
            {
                float btnW = 70f;
                Rect warnR = new Rect(pos.x + lw + 2, y,
                                         pos.width - lw - btnW - 6, lh);
                Rect fixR = new Rect(pos.xMax - btnW, y, btnW, lh);

                string msg = refObj.HasWarning
                    ? $"Key '{refObj.EntryKey}' not found — re-link"
                    : $"Path '{refObj.Path}' invalid — field not found";
                //

                var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = t.warning } };
                EditorGUI.LabelField(warnR, msg, warnStyle);

                GUI.backgroundColor = t.buttonWarning;
                if (GUI.Button(fixR, "Fix Now", EditorStyles.miniButton))
                {
                    // Fix Now button — syncs key from cached entry name if possible,
                    // otherwise opens picker to manually re-link
                    if (refObj.HasWarning)
                    {
                        if (refObj.TryAutoFix())
                        {
                            prop.serializedObject.Update();
                            prop.FindPropertyRelative("_entryKey").stringValue = refObj.EntryKey;
                            prop.serializedObject.ApplyModifiedProperties();
                        }
                        else ShowEntryPicker(prop, refObj);

                    }


                    else
                    {
                        // Path warning — clear path
                        prop.serializedObject.Update();
                        prop.FindPropertyRelative("_path").stringValue = "";
                        prop.serializedObject.ApplyModifiedProperties();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUI.EndProperty();
        }

        // ── Picker ────────────────────────────────────────────────────────
        void ShowEntryPicker(SerializedProperty prop, GVEntryReference refObj)
        {
            var menu = new GenericMenu();
            var allGVs = GroupValuesRegistry.GetAll();

            if (allGVs == null || allGVs.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No GroupValues in Registry"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("None"), refObj?.GroupValues == null,
                   () => ApplyTarget(prop, null, "", "", ""));

            menu.AddSeparator("");

            foreach (var gv in allGVs)
                foreach (var field in gv.fields)
                    foreach (var entry in field.entries)
                    {
                        var cGV = gv;
                        var cField = field.fieldName;
                        var cKey = entry.name;
                        bool active = refObj?.GroupValues == gv &&
                                      refObj?.EntryKey == cKey;

                        menu.AddItem(
                            new GUIContent(
                                $"{gv.name}/{field.fieldName}/{entry.name} [{entry.type}]"),
                            active,
                            () => ApplyTarget(prop, cGV, cField, cKey, ""));
                    }

            menu.ShowAsContext();
        }

        // ── Path picker ───────────────────────────────────────────────────
        void ShowPathPicker(SerializedProperty prop, GVEntryReference refObj)
        {
            var rootType = GVEntryReference.ResolveCustomType(
                refObj.GetRef_CachedCustomTypeName());
            if (rootType == null) return;

            var menu = new GenericMenu();

            // Option: whole entry (no path)
            menu.AddItem(new GUIContent("— whole entry —"),
                string.IsNullOrEmpty(refObj.Path),
                () => ApplyPath(prop, ""));
            menu.AddSeparator("");

            // Recursively add all leaf paths
            AddPathItems(menu, prop, refObj, rootType, "", 0);
            menu.ShowAsContext();
        }

        static void AddPathItems(GenericMenu menu, SerializedProperty prop,
                                  GVEntryReference refObj, Type type,
                                  string currentPath, int depth)
        {
            if (depth > 8) return; // safety limit

            foreach (var fi in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!fi.IsPublic && fi.GetCustomAttribute<SerializeField>() == null) continue;
                if (fi.GetCustomAttribute<NonSerializedAttribute>() != null) continue;

                string path = string.IsNullOrEmpty(currentPath)
                    ? fi.Name : $"{currentPath}/{fi.Name}";
                bool isLeaf = IsLeafType(fi.FieldType);
                bool active = refObj.Path == path;

                if (isLeaf)
                {
                    menu.AddItem(
                        new GUIContent(path.Replace("/", " › ") +
                                       $" [{fi.FieldType.Name}]"),
                        active,
                        () => ApplyPath(prop, path));
                }
                else if (IsNavigableStruct(fi.FieldType))
                {
                    // Navigable struct/class — show as disabled header and recurse
                    menu.AddDisabledItem(new GUIContent(
                        path.Replace("/", " › ") + " ▸"));
                    AddPathItems(menu, prop, refObj, fi.FieldType, path, depth + 1);
                }
            }
        }

        static bool IsLeafType(Type t) =>
            t.IsPrimitive ||
            t == typeof(string);

        // Unity math types that can be navigated into for their components
        static readonly HashSet<Type> s_unityNavigable = new HashSet<Type>
    {
        typeof(Vector2), typeof(Vector3), typeof(Vector4),
        typeof(Color),   typeof(Color32), typeof(Quaternion),
        typeof(Rect),    typeof(Bounds),
    };

        // Types that can be navigated into for their components
        static bool IsNavigableStruct(Type t) =>
            s_unityNavigable.Contains(t) ||
            (t.IsSerializable && !t.IsGenericType &&
             !t.IsPrimitive && t != typeof(string));

        static void ApplyPath(SerializedProperty prop, string path)
        {
            prop.serializedObject.Update();
            prop.FindPropertyRelative("_path").stringValue = path;
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
        }

        void ApplyTarget(SerializedProperty prop,
                         GroupValues gv, string fieldName, string key, string path)
        {
            prop.serializedObject.Update();

            prop.FindPropertyRelative("_groupValues").objectReferenceValue = gv;
            prop.FindPropertyRelative("_fieldName").stringValue = fieldName;
            prop.FindPropertyRelative("_entryKey").stringValue = key;
            prop.FindPropertyRelative("_path").stringValue = path;

            prop.serializedObject.ApplyModifiedProperties();

            // Force cache invalidation on the actual object
            var refObj = GetRef(prop);
            refObj?.Invalidate();

            EditorUtility.SetDirty(prop.serializedObject.targetObject);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static GVEntryReference GetRef(SerializedProperty prop)
        {
            object obj = prop.serializedObject.targetObject;
            foreach (var part in prop.propertyPath.Split('.'))
            {
                var fi = obj?.GetType().GetField(part,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                obj = fi?.GetValue(obj);
            }
            return obj as GVEntryReference;
        }
    }
    public partial class GVEntryReference
    {
#if UNITY_EDITOR
        internal string GetRef_CachedCustomTypeName()
        {
            Resolve(log: false);
            return _cached?.customTypeName ?? "";
        }
#endif
    }

#endif
}