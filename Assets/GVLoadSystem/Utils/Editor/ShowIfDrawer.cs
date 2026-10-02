#if UNITY_EDITOR
using GVUtils.Attributes;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace GVUtils.GVEditor
{
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    internal class HideIfDrawer : ShowIfDrawer { }

    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    internal class ShowIfDrawer : PropertyDrawer
    {
        // ── Entry point ───────────────────────────────────────────────────
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (ShowIfAttribute)attribute;

            // Single field with expectedValue — legacy behaviour
            if (IsLegacyMode(attr.expression))
            {
                if (IsVisibleLegacy(property, label, attr) == attr.comparison)
                    EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // Expression mode
            if (Evaluate(attr.expression, property) == attr.comparison)
                EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = (ShowIfAttribute)attribute;

            bool visible;
            if (IsLegacyMode(attr.expression))
                visible = IsVisibleLegacy(property, label, attr) == attr.comparison;
            else
                visible = Evaluate(attr.expression, property) == attr.comparison;

            return visible
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : 0f;
        }

        // ── Legacy mode — single field + expectedValue ────────────────────
        // Legacy mode: expression is a plain field name (no operators)
        bool IsLegacyMode(string expression)
        {
            return !expression.Contains("||") &&
                   !expression.Contains("&&") &&
                   !expression.Contains("!") &&
                   !expression.Contains("(");
        }

        bool IsVisibleLegacy(SerializedProperty property, GUIContent label, ShowIfAttribute attr)
        {
            SerializedProperty other = FindRelativeProperty(property, attr.expression);

            if (other == null)
            {
                Debug.LogWarning($"{label.text}: field '{attr.expression}' not found.");
                return true;
            }

            switch (other.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (attr.expectedValue is bool)
                        return other.boolValue.Equals(attr.expectedValue);
                    Debug.LogWarning($"Expected value for boolean must be bool. Found: {attr.expectedValue?.GetType()}");
                    break;

                case SerializedPropertyType.Enum:
                    if (attr.expectedValue is string str)
                        return other.enumNames[other.enumValueIndex] == str;
                    if (attr.expectedValue is int i)
                        return other.enumValueIndex == i;
                    if (attr.expectedValue?.GetType().IsEnum == true)
                        return other.enumNames[other.enumValueIndex] == attr.expectedValue.ToString();
                    Debug.LogWarning($"Unsupported expectedValue type for enum: {attr.expectedValue?.GetType()}");
                    break;

                case SerializedPropertyType.Integer:
                    if (attr.expectedValue is int)
                        return other.intValue.Equals(attr.expectedValue);
                    Debug.LogWarning($"Expected value for integer must be int. Found: {attr.expectedValue?.GetType()}");
                    break;

                case SerializedPropertyType.Float:
                    if (attr.expectedValue is float)
                        return Mathf.Approximately(other.floatValue, (float)attr.expectedValue);
                    Debug.LogWarning($"Expected value for float must be float. Found: {attr.expectedValue?.GetType()}");
                    break;

                case SerializedPropertyType.String:
                    if (attr.expectedValue is string)
                        return other.stringValue.Equals(attr.expectedValue);
                    Debug.LogWarning($"Expected value for string must be string. Found: {attr.expectedValue?.GetType()}");
                    break;

                case SerializedPropertyType.ObjectReference:
                    if (attr.expectedValue is bool expBool && expBool)
                        return other.objectReferenceValue == null;
                    if (attr.expectedValue is null)
                        return other.objectReferenceValue == null;
                    Debug.LogWarning($"ShowIf with ObjectReference only checks for null. Found: {attr.expectedValue}");
                    break;

                default:
                    Debug.LogWarning($"Unsupported type: {other.propertyType}");
                    return true;
            }

            return true;
        }

        // ── Expression evaluator ──────────────────────────────────────────
        bool Evaluate(string expression, SerializedProperty context)
        {
            var tokens = Tokenize(expression);
            int pos = 0;
            return ParseOr(tokens, ref pos, context);
        }

        bool ParseOr(List<string> tokens, ref int pos, SerializedProperty context)
        {
            bool result = ParseAnd(tokens, ref pos, context);
            while (pos < tokens.Count && tokens[pos] == "||")
            {
                pos++;
                bool right = ParseAnd(tokens, ref pos, context);
                result = result || right;
            }
            return result;
        }

        bool ParseAnd(List<string> tokens, ref int pos, SerializedProperty context)
        {
            bool result = ParseUnary(tokens, ref pos, context);
            while (pos < tokens.Count && tokens[pos] == "&&")
            {
                pos++;
                bool right = ParseUnary(tokens, ref pos, context);
                result = result && right;
            }
            return result;
        }

        bool ParseUnary(List<string> tokens, ref int pos, SerializedProperty context)
        {
            if (pos < tokens.Count && tokens[pos] == "!")
            {
                pos++;
                return !ParseUnary(tokens, ref pos, context);
            }
            return ParsePrimary(tokens, ref pos, context);
        }

        bool ParsePrimary(List<string> tokens, ref int pos, SerializedProperty context)
        {
            if (pos >= tokens.Count) return false;

            if (tokens[pos] == "(")
            {
                pos++;
                bool result = ParseOr(tokens, ref pos, context);
                if (pos < tokens.Count && tokens[pos] == ")")
                    pos++;
                return result;
            }

            string fieldName = tokens[pos++];
            return ResolveField(fieldName, context);
        }

        // ── Field resolver ────────────────────────────────────────────────
        bool ResolveField(string fieldName, SerializedProperty context)
        {
            var prop = FindRelativeProperty(context, fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ShowIf] Field '{fieldName}' not found.");
                return true;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Boolean:       return prop.boolValue;
                case SerializedPropertyType.Integer:       return prop.intValue != 0;
                case SerializedPropertyType.Float:         return !Mathf.Approximately(prop.floatValue, 0f);
                case SerializedPropertyType.String:        return !string.IsNullOrEmpty(prop.stringValue);
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue != null;
                case SerializedPropertyType.Enum:          return prop.enumValueIndex != 0;
                default:
                    Debug.LogWarning($"[ShowIf] Unsupported type '{prop.propertyType}' for '{fieldName}'.");
                    return true;
            }
        }

        // ── Tokenizer ─────────────────────────────────────────────────────
        List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < expression.Length)
            {
                char c = expression[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(' || c == ')') { tokens.Add(c.ToString()); i++; continue; }

                if (c == '!' && (i + 1 >= expression.Length || expression[i + 1] != '='))
                { tokens.Add("!"); i++; continue; }

                if (c == '|' && i + 1 < expression.Length && expression[i + 1] == '|')
                { tokens.Add("||"); i += 2; continue; }

                if (c == '&' && i + 1 < expression.Length && expression[i + 1] == '&')
                { tokens.Add("&&"); i += 2; continue; }

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    int start = i;
                    while (i < expression.Length &&
                           (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                        i++;
                    tokens.Add(expression.Substring(start, i - start));
                    continue;
                }

                i++;
            }
            return tokens;
        }

        // ── Path helper ───────────────────────────────────────────────────
        SerializedProperty FindRelativeProperty(SerializedProperty property, string fieldName)
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
                return property.serializedObject.FindProperty(fieldName);

            string parentPath = path.Substring(0, lastDot);
            return property.serializedObject.FindProperty(parentPath + "." + fieldName);
        }
    }
}
#endif