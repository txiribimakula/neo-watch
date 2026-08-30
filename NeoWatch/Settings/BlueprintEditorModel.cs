using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace NeoWatch.Settings
{
    public sealed class BlueprintEntry : INotifyPropertyChanged
    {
        internal static readonly Regex Headers = new Regex(@"^[ \t]*\[(?<title>[^\r\n]*)\][ \t]*\r?$",
            RegexOptions.Multiline);
        private string text;
        private bool isExpanded;

        public BlueprintEntry(string text)
        {
            this.text = text ?? string.Empty;
        }

        public string Text
        {
            get { return text; }
            set
            {
                if (text == value) return;
                text = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        public string Title
        {
            get
            {
                var header = Headers.Match(Text);
                string title = header.Success ? header.Groups["title"].Value.Trim() : string.Empty;
                return title.Length == 0 ? "New blueprint" : title;
            }
        }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (isExpanded == value) return;
                isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class BlueprintEditorModel
    {
        public ObservableCollection<BlueprintEntry> Entries { get; } = new ObservableCollection<BlueprintEntry>();

        public BlueprintEditorModel(string text)
        {
            text = text ?? string.Empty;
            var headers = BlueprintEntry.Headers.Matches(text);
            if (headers.Count == 0)
            {
                if (text.Length != 0) Entries.Add(new BlueprintEntry(text));
                return;
            }

            // Slice the original text, not the parsed loader objects: retain comments and even
            // incomplete sections so opening the editor can never silently discard user data.
            var starts = new List<int> { 0 };
            for (int i = 1; i < headers.Count; i++)
            {
                int start = headers[i].Index;
                while (start > headers[i - 1].Index)
                {
                    int lineStart = start < 2 ? 0 : text.LastIndexOf('\n', start - 2) + 1;
                    string line = text.Substring(lineStart, start - lineStart).Trim();
                    if (line.Length != 0 && !line.StartsWith("#", StringComparison.Ordinal)
                        && !line.StartsWith(";", StringComparison.Ordinal)) break;
                    start = lineStart;
                }
                starts.Add(start);
            }
            for (int i = 0; i < headers.Count; i++)
            {
                int start = starts[i];
                int end = i + 1 < headers.Count ? starts[i + 1] : text.Length;
                Entries.Add(new BlueprintEntry(text.Substring(start, end - start)));
            }
        }

        public BlueprintEntry Add()
        {
            var entry = new BlueprintEntry(string.Empty) { IsExpanded = true };
            Entries.Add(entry);
            return entry;
        }

        public bool TrySerialize(out string text, out string error)
        {
            text = null;
            error = null;
            var types = new List<LinkedListMemoryBlueprint>();
            var result = new StringBuilder();
            foreach (BlueprintEntry entry in Entries)
            {
                if (BlueprintEntry.Headers.Matches(entry.Text).Count != 1)
                {
                    error = entry.Title + ": each entry needs exactly one [Type] section.";
                    entry.IsExpanded = true;
                    return false;
                }

                var parsed = LinkedListMemoryBlueprintParser.Parse(entry.Text);
                if (parsed.Count != 1)
                {
                    error = entry.Title + ": incomplete or invalid blueprint.";
                    entry.IsExpanded = true;
                    return false;
                }
                if (types.Exists(type => type.Matches(parsed[0].TypeName)))
                {
                    error = entry.Title + ": this container type already has a blueprint.";
                    entry.IsExpanded = true;
                    return false;
                }
                types.Add(parsed[0]);

                if (result.Length > 0 && result[result.Length - 1] != '\n') result.Append("\r\n");
                result.Append(entry.Text);
            }
            text = result.ToString();
            return true;
        }
    }
}
