using System.Collections.Generic;

namespace NeoWatch.Loading
{
    /// <summary>
    /// Pulls watch expressions out of pasted text.
    ///
    /// The shape it expects is what Visual Studio puts on the clipboard when you copy rows from
    /// the Watch, Locals or Autos windows: one row per line, columns separated by tabs, the first
    /// column being the expression. Plain text with one expression per line works too, so pasting
    /// from a note or a file behaves the same.
    /// </summary>
    public static class WatchExpressionParser
    {
        /// <summary>
        /// A deliberate ceiling. Every expression becomes a watch item that loads on every break,
        /// so pasting a whole file by accident should not bring the window down.
        /// </summary>
        public const int MaxExpressions = 100;

        public static List<string> Parse(string text)
        {
            var expressions = new List<string>();
            if (string.IsNullOrEmpty(text)) return expressions;

            var seen = new HashSet<string>();

            foreach (string line in text.Split('\n'))
            {
                string expression = FirstColumn(line);
                if (expression.Length == 0) continue;
                if (!LooksLikeExpression(expression)) continue;

                // Repeats add nothing and each one costs a full load on every break.
                if (!seen.Add(expression)) continue;

                expressions.Add(expression);
                if (expressions.Count == MaxExpressions) break;
            }

            return expressions;
        }

        /// <summary>
        /// Rejects whole statements. Copying with nothing selected in an editor yields the entire
        /// source line, which reads like an expression until you try to evaluate it. None of these
        /// can appear in something you would put in a watch.
        /// </summary>
        private static bool LooksLikeExpression(string candidate)
        {
            return candidate.IndexOf(';') < 0
                && candidate.IndexOf("//", System.StringComparison.Ordinal) < 0
                && candidate.IndexOf("/*", System.StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// The tree expander travels with the row when Visual Studio copies it, so a line can
        /// arrive as "+\tcheckPoints\t{ size=40 }" or even as a lone "+" for a row that was not
        /// really selected. Those glyphs are stripped, and a line left with nothing else is dropped.
        /// </summary>
        private static readonly char[] ExpanderGlyphs = { '+', '-', '▶', '▼', '▸', '▾' };

        private static string FirstColumn(string line)
        {
            string trimmed = line.Trim('\r', ' ', '\t');

            // Drop leading expander glyphs before splitting: the expander can be its own column.
            while (trimmed.Length > 0 && System.Array.IndexOf(ExpanderGlyphs, trimmed[0]) >= 0)
            {
                trimmed = trimmed.Substring(1).TrimStart(' ', '\t');
            }

            int tab = trimmed.IndexOf('\t');
            if (tab >= 0)
            {
                trimmed = trimmed.Substring(0, tab);
            }

            return trimmed.Trim();
        }
    }
}
