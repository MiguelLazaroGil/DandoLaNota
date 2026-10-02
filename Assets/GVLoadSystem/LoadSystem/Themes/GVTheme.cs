using GVLoadSystem.Core;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Defines the visual theme for all LoadSystem editor windows and drawers.
    /// Create custom themes via Assets > Create > LoadSystem > Theme.
    /// </summary>
    [CreateAssetMenu(menuName = "LoadSystem/Theme", fileName = "GVTheme_Custom")]
    internal class GVTheme : ScriptableObject
    {
        [Header("Backgrounds")]
        public Color backgroundDeep = new Color(0.13f, 0.15f, 0.20f);
        public Color backgroundPanel = new Color(0.11f, 0.13f, 0.17f);
        public Color backgroundCode = new Color(0.09f, 0.10f, 0.14f);
        public Color backgroundRow0 = new Color(0.13f, 0.15f, 0.19f);
        public Color backgroundRow1 = new Color(0.15f, 0.17f, 0.22f);

        [Header("Accents")]
        public Color accent = new Color(0.25f, 0.55f, 0.90f);
        public Color selected = new Color(0.18f, 0.35f, 0.62f);
        public Color separator = new Color(0.22f, 0.25f, 0.32f);

        [Header("Text")]
        public Color textPrimary = new Color(0.85f, 0.92f, 1.00f);
        public Color textSecondary = new Color(0.78f, 0.83f, 0.90f);
        public Color textDim = new Color(0.55f, 0.60f, 0.70f);
        public Color textCode = new Color(0.85f, 0.85f, 0.85f);

        [Header("Status")]
        public Color valid = new Color(0.20f, 0.75f, 0.35f);
        public Color invalid = new Color(0.80f, 0.25f, 0.15f);
        public Color warning = new Color(0.90f, 0.60f, 0.10f);
        public Color dirty = new Color(0.90f, 0.60f, 0.10f);

        [Header("Value Type Colors")]
        public Color typeBool = new Color(0.95f, 0.40f, 0.40f);
        public Color typeInt = new Color(0.40f, 0.75f, 0.95f);
        public Color typeFloat = new Color(0.40f, 0.95f, 0.65f);
        public Color typeDouble = new Color(0.35f, 0.85f, 0.55f);
        public Color typeString = new Color(0.95f, 0.75f, 0.30f);
        public Color typeVector = new Color(0.75f, 0.50f, 0.95f);
        public Color typeChar = new Color(0.95f, 0.60f, 0.80f);
        public Color typeByte = new Color(0.60f, 0.80f, 0.95f);
        public Color typeShort = new Color(0.50f, 0.70f, 0.90f);
        public Color typeLong = new Color(0.45f, 0.65f, 0.85f);
        public Color typeCustom = new Color(0.95f, 0.65f, 0.25f);

        [Header("Button Colors")]
        public Color buttonPrimary = new Color(0.20f, 0.55f, 0.85f);
        public Color buttonSuccess = new Color(0.20f, 0.60f, 0.30f);
        public Color buttonDanger = new Color(0.55f, 0.15f, 0.15f);
        public Color buttonWarning = new Color(0.65f, 0.45f, 0.12f);
        public Color buttonNeutral = new Color(0.22f, 0.28f, 0.42f);

        [Header("Typography")]
        public int fontSizeTitle = 13;
        public int fontSizeBody = 12;
        public int fontSizeSmall = 11;
        public int fontSizeCode = 11;
        public int fontSizeMini = 9;

        [Header("Spacing")]
        public float rowHeight = 22f;
        public float headerHeight = 28f;
        public float padding = 8f;
        public float indentStep = 16f;

        // ── Runtime access ────────────────────────────────────────────────
        // Set by GVThemeManager at startup so runtime code can access the active theme
        // without depending on UnityEditor namespace.
        public static GVTheme Current { get; set; }

        void OnEnable()
        {
            // Auto-register as current if none set yet (fallback for runtime)
            if (Current == null) Current = this;
        }

        // ── Type color lookup ─────────────────────────────────────────────
        public Color GetTypeColor(VALUE_TYPE type)
        {
            switch (type)
            {
                case VALUE_TYPE.BOOL: return typeBool;
                case VALUE_TYPE.INT: return typeInt;
                case VALUE_TYPE.FLOAT: return typeFloat;
                case VALUE_TYPE.DOUBLE: return typeDouble;
                case VALUE_TYPE.STRING: return typeString;
                case VALUE_TYPE.VECTOR2:
                case VALUE_TYPE.VECTOR3: return typeVector;
                case VALUE_TYPE.CHAR: return typeChar;
                case VALUE_TYPE.BYTE: return typeByte;
                case VALUE_TYPE.SHORT: return typeShort;
                case VALUE_TYPE.LONG: return typeLong;
                case VALUE_TYPE.CUSTOM: return typeCustom;
                default: return typeCustom;
            }
        }

        // ── Predefined themes ─────────────────────────────────────────────
        public static GVTheme CreateOceanBlue()
        {
            var t = CreateInstance<GVTheme>();
            t.name = "GVTheme_OceanBlue";
            // Default values already match Ocean Blue — no overrides needed
            return t;
        }

        public static GVTheme CreateDarkForest()
        {
            var t = CreateInstance<GVTheme>();
            t.name = "GVTheme_DarkForest";
            t.backgroundDeep = new Color(0.08f, 0.12f, 0.10f);
            t.backgroundPanel = new Color(0.07f, 0.10f, 0.09f);
            t.backgroundCode = new Color(0.05f, 0.08f, 0.07f);
            t.backgroundRow0 = new Color(0.09f, 0.12f, 0.10f);
            t.backgroundRow1 = new Color(0.11f, 0.14f, 0.12f);
            t.accent = new Color(0.25f, 0.75f, 0.40f);
            t.selected = new Color(0.15f, 0.45f, 0.25f);
            t.separator = new Color(0.18f, 0.28f, 0.22f);
            t.textPrimary = new Color(0.80f, 0.95f, 0.85f);
            t.textSecondary = new Color(0.65f, 0.82f, 0.72f);
            t.textDim = new Color(0.45f, 0.60f, 0.52f);
            t.buttonPrimary = new Color(0.20f, 0.65f, 0.35f);
            t.buttonSuccess = new Color(0.15f, 0.55f, 0.25f);
            return t;
        }

        public static GVTheme CreateCrimson()
        {
            var t = CreateInstance<GVTheme>();
            t.name = "GVTheme_Crimson";
            t.backgroundDeep = new Color(0.14f, 0.08f, 0.10f);
            t.backgroundPanel = new Color(0.12f, 0.07f, 0.09f);
            t.backgroundCode = new Color(0.09f, 0.05f, 0.07f);
            t.backgroundRow0 = new Color(0.13f, 0.08f, 0.10f);
            t.backgroundRow1 = new Color(0.16f, 0.10f, 0.12f);
            t.accent = new Color(0.85f, 0.25f, 0.35f);
            t.selected = new Color(0.55f, 0.15f, 0.22f);
            t.separator = new Color(0.30f, 0.18f, 0.22f);
            t.textPrimary = new Color(1.00f, 0.88f, 0.90f);
            t.textSecondary = new Color(0.88f, 0.75f, 0.78f);
            t.textDim = new Color(0.65f, 0.52f, 0.55f);
            t.buttonPrimary = new Color(0.75f, 0.20f, 0.28f);
            t.buttonSuccess = new Color(0.65f, 0.18f, 0.25f);
            return t;
        }

        public static GVTheme CreateMinimal()
        {
            var t = CreateInstance<GVTheme>();
            t.name = "GVTheme_Minimal";
            t.backgroundDeep = new Color(0.18f, 0.18f, 0.18f);
            t.backgroundPanel = new Color(0.16f, 0.16f, 0.16f);
            t.backgroundCode = new Color(0.13f, 0.13f, 0.13f);
            t.backgroundRow0 = new Color(0.17f, 0.17f, 0.17f);
            t.backgroundRow1 = new Color(0.19f, 0.19f, 0.19f);
            t.accent = new Color(0.65f, 0.65f, 0.65f);
            t.selected = new Color(0.30f, 0.30f, 0.30f);
            t.separator = new Color(0.25f, 0.25f, 0.25f);
            t.textPrimary = new Color(0.92f, 0.92f, 0.92f);
            t.textSecondary = new Color(0.75f, 0.75f, 0.75f);
            t.textDim = new Color(0.55f, 0.55f, 0.55f);
            t.buttonPrimary = new Color(0.40f, 0.40f, 0.40f);
            t.buttonSuccess = new Color(0.35f, 0.55f, 0.35f);
            t.buttonNeutral = new Color(0.30f, 0.30f, 0.30f);
            return t;
        }
    }
}