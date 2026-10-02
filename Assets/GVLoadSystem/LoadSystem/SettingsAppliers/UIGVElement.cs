using System;
using GVLoadSystem.Core;
using GVUtils.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GVLoadSystem.Settings
{
    public enum UIElement { TOGGLE, SLIDER, DRAWER, TMP_DRAWER, TMP_INPUT }

    /// <summary>
    /// Connects a UI control directly to a GroupValues entry via GVEntryReference.
    /// No IValuesContainer needed — reads/writes directly to the referenced GV.
    /// </summary>
    public class UIGVElement : MonoBehaviour
    {
        [SerializeField] GVEntryReference _entry = new GVEntryReference();
        [SerializeField] UIElement _uiElement;
        [SerializeField] VALUE_TYPE _dataType;

        [Header("Behaviour")]
        [SerializeField] bool _loadOnEnable = true;
        [SerializeField] bool _saveOnValueChange = true;

        [NonSerialized] public bool isDirty = false;

        // ── Unity ─────────────────────────────────────────────────────
        void OnEnable()
        {
            _entry?.ResetEntry(); 
            if (_loadOnEnable)
            {
                LoadData(); // LoadData handles callback registration internally
            }
            else if (_saveOnValueChange)
            {
                RegisterCallbacks(SetValue);
            }
        }

        void OnDisable() => UnregisterCallbacks();

        // ── Public API ────────────────────────────────────────────────
        [Button("LoadData")]
        public void LoadData()
        {
            if (_entry == null || !_entry.IsValid) return;

            // Unregister callbacks before setting values to prevent
            // onValueChanged firing during load and triggering a save
            UnregisterCallbacks();

            switch (_uiElement)
            {
                case UIElement.TOGGLE:
                    if (TryGetComponent<Toggle>(out var tog))
                        tog.isOn = _entry.Get<bool>();
                    break;
                case UIElement.SLIDER:
                    if (TryGetComponent<Slider>(out var sli))
                        sli.value = _dataType == VALUE_TYPE.INT
                            ? _entry.Get<int>()
                            : _entry.Get<float>();
                    break;
                case UIElement.DRAWER:
                    if (TryGetComponent<Dropdown>(out var dd))
                        dd.value = _entry.Get<int>();
                    break;
                case UIElement.TMP_DRAWER:
                    if (TryGetComponent<TMP_Dropdown>(out var tdd))
                        tdd.value = _entry.Get<int>();
                    break;
                case UIElement.TMP_INPUT:
                    if (TryGetComponent<TMP_InputField>(out var inp))
                        inp.text = _entry.Get<string>()?.ToString() ?? "";
                    break;
            }

            isDirty = false;

            // Re-register callbacks after load
            if (_saveOnValueChange) RegisterCallbacks(SetValue);
        }

        public void SetValue()
        {
            if (_entry == null || !_entry.IsValid) return;

            switch (_uiElement)
            {
                case UIElement.TOGGLE:
                    if (TryGetComponent<Toggle>(out var tog))
                        _entry.Set(tog.isOn);
                    break;
                case UIElement.SLIDER:
                    if (TryGetComponent<Slider>(out var sli))
                    {

                        _entry.Set<float>(sli.value);
                    }
                    break;
                case UIElement.DRAWER:
                    if (TryGetComponent<Dropdown>(out var dd))
                        _entry.Set(dd.value);
                    break;
                case UIElement.TMP_DRAWER:
                    if (TryGetComponent<TMP_Dropdown>(out var tdd))
                        _entry.Set(tdd.value);
                    break;
                case UIElement.TMP_INPUT:
                    if (TryGetComponent<TMP_InputField>(out var inp))
                        _entry.Set(inp.text);
                    break;
            }
            isDirty = false;
        }

        public void SaveIfDirty() { if (isDirty) SetValue(); }
        public void MarkDirty() => isDirty = true;

        // ── Inspector helper ──────────────────────────────────────────
        public void Reset() => DetectUIElement();

        public void DetectUIElement()
        {
            if (TryGetComponent<Slider>(out _)) { _dataType = VALUE_TYPE.FLOAT; _uiElement = UIElement.SLIDER; }
            else if (TryGetComponent<Toggle>(out _)) { _dataType = VALUE_TYPE.BOOL; _uiElement = UIElement.TOGGLE; }
            else if (TryGetComponent<TMP_InputField>(out _)) { _dataType = VALUE_TYPE.STRING; _uiElement = UIElement.TMP_INPUT; }
            else if (TryGetComponent<TMP_Dropdown>(out _)) { _dataType = VALUE_TYPE.INT; _uiElement = UIElement.TMP_DRAWER; }
            else if (TryGetComponent<Dropdown>(out _)) { _dataType = VALUE_TYPE.INT; _uiElement = UIElement.DRAWER; }
            else Debug.LogWarning("[UIGVElement] No supported UI component found on " + name);
        }

        // ── Accessors for the editor window ───────────────────────────
        public GVEntryReference Entry
        {
            get { if (_entry == null) _entry = new GVEntryReference(); return _entry; }
            set => _entry = value;
        }

        public void InitEntry()
        {
            if (_entry == null) _entry = new GVEntryReference();
        }
        public UIElement UIElem { get => _uiElement; set => _uiElement = value; }
        public VALUE_TYPE DataType { get => _dataType; set => _dataType = value; }

        // ── Callbacks ─────────────────────────────────────────────────
        void RegisterCallbacks(Action cb)
        {
            switch (_uiElement)
            {
                case UIElement.TOGGLE:
                    if (TryGetComponent<Toggle>(out var tog))
                        tog.onValueChanged.AddListener(_ => OnChanged(cb));
                    break;
                case UIElement.SLIDER:
                    if (TryGetComponent<Slider>(out var sli))
                        sli.onValueChanged.AddListener(_ => OnChanged(cb));
                    break;
                case UIElement.DRAWER:
                    if (TryGetComponent<Dropdown>(out var dd))
                        dd.onValueChanged.AddListener(_ => OnChanged(cb));
                    break;
                case UIElement.TMP_DRAWER:
                    if (TryGetComponent<TMP_Dropdown>(out var tdd))
                        tdd.onValueChanged.AddListener(_ => OnChanged(cb));
                    break;
                case UIElement.TMP_INPUT:
                    if (TryGetComponent<TMP_InputField>(out var inp))
                        inp.onValueChanged.AddListener(_ => OnChanged(cb));
                    break;
            }
        }

        void UnregisterCallbacks()
        {
            switch (_uiElement)
            {
                case UIElement.TOGGLE:
                    if (TryGetComponent<Toggle>(out var tog)) tog.onValueChanged.RemoveAllListeners(); break;
                case UIElement.SLIDER:
                    if (TryGetComponent<Slider>(out var sli)) sli.onValueChanged.RemoveAllListeners(); break;
                case UIElement.DRAWER:
                    if (TryGetComponent<Dropdown>(out var dd)) dd.onValueChanged.RemoveAllListeners(); break;
                case UIElement.TMP_DRAWER:
                    if (TryGetComponent<TMP_Dropdown>(out var tdd)) tdd.onValueChanged.RemoveAllListeners(); break;
                case UIElement.TMP_INPUT:
                    if (TryGetComponent<TMP_InputField>(out var inp)) inp.onValueChanged.RemoveAllListeners(); break;
            }
        }

        void OnChanged(Action cb) { isDirty = true; if (_saveOnValueChange) cb(); }
    }
}