# Changelog

All notable changes to GVLoadSystem will be documented here.

## [1.2.1] - 2026-06-08

### Fix
- Fixed nested serializable class fields inside a `[CustomGVData]` entry not saving edits. Changes were applied in memory but the dirty flag was never set, so JSON was never re-serialized. Fixed by wrapping `DrawReflectedField` calls for nested serializable types with `EditorGUI.BeginChangeCheck/EndChangeCheck` in `DrawCustomEntryExpanded`.

## [1.2.0] - 2026-06-08

### Improvements - Quality of Life Improvements
- Added color to nested classes for improved visual hierarchy in the inspector
- Added more clarity to objects displayed inside lists

## [1.1.1] - 2026-06-05

### Fix
- Fixed build compilation errors to allow successful player builds

## [1.1.0] - 2026-06-04

### Improvements
- Refactored internal architecture for cleaner separation of concerns
- General code cleanup across core systems

## [1.0.0] - 2026-05-15

Initial release.

### Features
- `GroupValues` ScriptableObject for storing typed game data (bool, int, float, double, short, long, byte, char, string, Vector2, Vector3, custom classes)
- `LoaderMono` MonoBehaviour for load/save/reset with configurable auto-load and auto-save hooks
- `SimpleGroupValues` singleton for lightweight project-wide data without a full GroupValues asset
- `GVEntryReference` serializable inspector-linked reference to a single entry
- JSON serialization via Unity's `JsonUtility` / `EditorJsonUtility`
- Automatic version migration: new and renamed fields are merged on load
- Optional backup file with automatic restore on main file corruption
- Three encryption modes: None, AES-256 + HMAC-SHA256, AES-256 Only, XOR obfuscation
- Per-device encryption key generated via CSPRNG and stored in `persistentDataPath`
- Async save/load API (`SaveValuesAsync`, `LoadValuesAsync`)
- CSV import/export
- UI appliers for Slider, Toggle, InputField, and Dropdown
- Editor tools: GroupValues Editor window, Simple GroupValues window, Importer/Exporter, Size Inspector, Debug Overlay
- Themeable editor UI with several built-in themes
- Build processor that bakes encryption settings automatically
- Example implementations: sound settings, camera viewport settings
