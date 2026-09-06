using System.Collections.Generic;
using System.Text;

namespace NeoWatch.Settings
{
    /// <summary>
    /// Bridges the per-blueprint list shown in Tools &gt; Options and the single INI text the
    /// loader parses. Splitting reuses <see cref="BlueprintEditorModel"/> so the list and the
    /// editor window always agree on where one blueprint ends and the next begins.
    /// </summary>
    public static class BlueprintSettingsList
    {
        /// <summary>Name of the single column the settings page lists blueprints by.</summary>
        public const string TypeKey = "type";

        public static List<KeyValuePair<string, string>> FromIni(string text)
        {
            var items = new List<KeyValuePair<string, string>>();
            foreach (BlueprintEntry entry in new BlueprintEditorModel(text).Entries)
            {
                var header = BlueprintEntry.Headers.Match(entry.Text);
                if (!header.Success) continue;

                string type = header.Groups["title"].Value.Trim();
                if (type.Length == 0) continue;

                // Comments written above the header move below it. The parser ignores them either
                // way, and every list item has to start at its own container type.
                string before = Normalize(entry.Text.Substring(0, header.Index));
                string body = Normalize(entry.Text.Substring(header.Index + header.Length));
                string definition = before.Length == 0 || body.Length == 0
                    ? before + body
                    : before + "\n" + body;
                items.Add(new KeyValuePair<string, string>(type, definition));
            }
            return items;
        }

        public static string ToIni(IEnumerable<KeyValuePair<string, string>> items)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, string> item in items)
            {
                string type = (item.Key ?? string.Empty).Trim();
                if (type.Length == 0) continue;

                if (text.Length > 0) text.Append("\n\n");
                text.Append('[').Append(type).Append(']');
                string definition = Normalize(item.Value);
                if (definition.Length != 0) text.Append('\n').Append(definition);
            }
            return text.ToString();
        }

        private static string Normalize(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n").Trim();
        }
    }
}
