using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GVLoadSystem.Core;
using UnityEngine;

namespace GVLoadSystem
{

    /// <summary>
    /// Handles CSV export and import for GroupValues.
    /// Format: FieldName;EntryKey1(type);EntryKey2(type);...
    ///         FieldA;value1;value2;...
    /// Separator: semicolon (;) to avoid conflicts with Vector and CUSTOM values.
    /// </summary>
    internal static class GroupValuesCSV
    {
        const char SEP = ';';

        // ── Export ────────────────────────────────────────────────────────

        public static string Export(GroupValues gv)
        {
            var sb = new StringBuilder();

            foreach (var field in gv.fields)
            {
                if (field.entries.Count == 0) continue;

                // Header row: FieldName;Key1(type);Key2(type);...
                sb.Append(EscapeCell(field.fieldName));
                foreach (var entry in field.entries)
                {
                    sb.Append(SEP);
                    sb.Append(EscapeCell($"{entry.name}({entry.type.ToString().ToLower()})"));
                }
                sb.AppendLine();

                // Value row: (empty);value1;value2;...
                sb.Append(""); // first column empty on value row
                foreach (var entry in field.entries)
                {
                    sb.Append(SEP);
                    sb.Append(EscapeCell(EntryToString(entry)));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Import ────────────────────────────────────────────────────────

        internal static void Import(string csv, GroupValues target)
        {
            target.fields.Clear();

            var lines = csv.Split(new[] { "\r\n", "\r", "\n" },
                                  StringSplitOptions.RemoveEmptyEntries);

            int i = 0;
            while (i < lines.Length)
            {
                string headerLine = lines[i];
                if (string.IsNullOrWhiteSpace(headerLine)) { i++; continue; }

                var headerCells = SplitLine(headerLine);
                if (headerCells.Count == 0) { i++; continue; }

                string fieldName = UnescapeCell(headerCells[0]);

                // Check if next line is the value row (first cell empty or whitespace)
                string valueLine = (i + 1 < lines.Length) ? lines[i + 1] : "";
                var valueCells = SplitLine(valueLine);

                // If first cell of value line is empty → it's a value row
                bool hasValueRow = valueCells.Count > 0 &&
                                   string.IsNullOrWhiteSpace(valueCells[0]);

                var entries = new List<GVEntry>();

                for (int col = 1; col < headerCells.Count; col++)
                {
                    string header = UnescapeCell(headerCells[col]);
                    string rawValue = hasValueRow && col < valueCells.Count
                        ? UnescapeCell(valueCells[col]) : "";

                    // Parse key and optional type hint from header
                    string key;
                    VALUE_TYPE? hintType = ParseHeaderHint(header, out key);

                    // Determine type
                    VALUE_TYPE type = hintType ?? InferType(rawValue);

                    var entry = new GVEntry
                    {
                        name = key,
                        type = type,
                        value = GVValueFactory.Create(type),
                    };

                    SetEntryValue(entry, rawValue, type);
                    entries.Add(entry);
                }

                target.fields.Add(new GVField
                {
                    fieldName = fieldName,
                    entries = entries,
                });

                i += hasValueRow ? 2 : 1;
            }

            target.RebuildCache();
        }

        // ── Type inference ────────────────────────────────────────────────

        static VALUE_TYPE? ParseHeaderHint(string header, out string key)
        {
            int open = header.LastIndexOf('(');
            int close = header.LastIndexOf(')');

            if (open >= 0 && close > open)
            {
                key = header.Substring(0, open).Trim();
                string hint = header.Substring(open + 1, close - open - 1).Trim().ToUpper();
                VALUE_TYPE t;
                if (Enum.TryParse(hint, out t)) return t;
                return null; // unrecognized hint → infer from value
            }

            key = header.Trim();
            return null;
        }

        static VALUE_TYPE InferType(string value)
        {
            if (string.IsNullOrEmpty(value)) return VALUE_TYPE.STRING;

            // Strict bool
            if (value == "true" || value == "false") return VALUE_TYPE.BOOL;

            // Float: digits with exactly one dot, dot can be trailing (e.g. "5.")
            // but not double dot ("5..") → already excluded by not having two dots
            bool hasPoint = value.Contains(".");
            if (hasPoint)
            {
                // Reject if more than one dot
                if (value.Replace(".", "").Length != value.Length - 1)
                    return VALUE_TYPE.STRING;
                // Must be all digits + one dot
                bool allDigits = true;
                foreach (char c in value)
                    if (c != '.' && !char.IsDigit(c)) { allDigits = false; break; }
                if (allDigits) return VALUE_TYPE.FLOAT;
                return VALUE_TYPE.STRING;
            }

            // Int: all digits (INT has priority over CHAR for single digits)
            bool isAllDigits = true;
            foreach (char c in value)
                if (!char.IsDigit(c)) { isAllDigits = false; break; }
            if (isAllDigits) return VALUE_TYPE.INT;

            // Single non-numeric char → CHAR
            if (value.Length == 1) return VALUE_TYPE.CHAR;

            return VALUE_TYPE.STRING;
        }

        // ── Value conversion ──────────────────────────────────────────────

        static string EntryToString(GVEntry entry)
        {
            if (entry.value == null) return "";
            object raw = entry.value.GetValue();
            if (raw == null) return "";

            switch (entry.type)
            {
                case VALUE_TYPE.VECTOR2:
                    var v2 = raw is Vector2 vec2 ? vec2 : Vector2.zero;
                    return $"{v2.x},{v2.y}";
                case VALUE_TYPE.VECTOR3:
                    var v3 = raw is Vector3 vec3 ? vec3 : Vector3.zero;
                    return $"{v3.x},{v3.y},{v3.z}";
                case VALUE_TYPE.CUSTOM:
                    return raw.ToString(); // JSON — semicolon separator keeps this safe
                default:
                    return raw.ToString();
            }
        }

        static void SetEntryValue(GVEntry entry, string raw, VALUE_TYPE type)
        {
            if (entry.value == null) return;

            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                switch (type)
                {
                    case VALUE_TYPE.BOOL:
                        // Accept "true"/"True"/"TRUE" when type is explicitly declared as bool
                        // Strict inference (only "true"/"false") still applies when type is inferred
                        entry.value.SetValue(string.Equals(raw, "true",
                            StringComparison.OrdinalIgnoreCase));
                        break;
                    case VALUE_TYPE.INT:
                        entry.value.SetValue(int.TryParse(raw, out int iv) ? iv : 0);
                        break;
                    case VALUE_TYPE.FLOAT:
                        entry.value.SetValue(float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            inv, out float fv) ? fv : 0f);
                        break;
                    case VALUE_TYPE.DOUBLE:
                        entry.value.SetValue(double.TryParse(raw, System.Globalization.NumberStyles.Float,
                            inv, out double dv) ? dv : 0.0);
                        break;
                    case VALUE_TYPE.LONG:
                        entry.value.SetValue(long.TryParse(raw, out long lv) ? lv : 0L);
                        break;
                    case VALUE_TYPE.SHORT:
                        entry.value.SetValue(short.TryParse(raw, out short sv) ? sv : (short)0);
                        break;
                    case VALUE_TYPE.BYTE:
                        entry.value.SetValue(byte.TryParse(raw, out byte bv) ? bv : (byte)0);
                        break;
                    case VALUE_TYPE.CHAR:
                        entry.value.SetValue(raw.Length > 0 ? raw[0] : '\0');
                        break;
                    case VALUE_TYPE.STRING:
                        entry.value.SetValue(raw);
                        break;
                    case VALUE_TYPE.VECTOR2:
                        {
                            var parts = raw.Split(',');
                            float x = parts.Length > 0 && float.TryParse(parts[0].Trim(),
                                System.Globalization.NumberStyles.Float, inv, out float px) ? px : 0f;
                            float y = parts.Length > 1 && float.TryParse(parts[1].Trim(),
                                System.Globalization.NumberStyles.Float, inv, out float py) ? py : 0f;
                            entry.value.SetValue(new Vector2(x, y));
                            break;
                        }
                    case VALUE_TYPE.VECTOR3:
                        {
                            var parts = raw.Split(',');
                            float x = parts.Length > 0 && float.TryParse(parts[0].Trim(),
                                System.Globalization.NumberStyles.Float, inv, out float px) ? px : 0f;
                            float y = parts.Length > 1 && float.TryParse(parts[1].Trim(),
                                System.Globalization.NumberStyles.Float, inv, out float py) ? py : 0f;
                            float z = parts.Length > 2 && float.TryParse(parts[2].Trim(),
                                System.Globalization.NumberStyles.Float, inv, out float pz) ? pz : 0f;
                            entry.value.SetValue(new Vector3(x, y, z));
                            break;
                        }
                    case VALUE_TYPE.CUSTOM:
                        entry.value.SetValue(raw);
                        break;
                    default:
                        entry.value.SetValue(raw);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GroupValuesCSV] Failed to parse '{raw}' as {type}: {ex.Message}");
            }
        }

        // ── CSV helpers ───────────────────────────────────────────────────

        static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    // Handle escaped quote ""
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuote = !inQuote;
                }
                else if (c == SEP && !inQuote)
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        static string EscapeCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Quote if contains separator, quote, newline
            if (value.Contains(SEP) || value.Contains('"') ||
                value.Contains('\n') || value.Contains('\r'))
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }
            return value;
        }

        static string UnescapeCell(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
            return value;
        }
    }
}