using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NeoWatch.Loading
{
    public enum MemoryScalarType
    {
        Float32,
        Float64,
        Int32,
        UInt32,
        Int64,
        UInt64
    }

    public enum MemoryGeometryKind
    {
        Point,
        Line,
        Arc
    }

    public sealed class MemoryValueBlueprint
    {
        public MemoryValueBlueprint(string path, MemoryScalarType scalarType)
        {
            Path = path;
            ScalarType = scalarType;
        }

        public string Path { get; private set; }
        public MemoryScalarType ScalarType { get; private set; }

        public int Size
        {
            get
            {
                switch (ScalarType)
                {
                    case MemoryScalarType.Float32:
                    case MemoryScalarType.Int32:
                    case MemoryScalarType.UInt32:
                        return 4;
                    default:
                        return 8;
                }
            }
        }
    }

    public sealed class MemoryGeometryBlueprint
    {
        public MemoryGeometryBlueprint(MemoryGeometryKind kind, int? tag,
            IDictionary<string, MemoryValueBlueprint> values)
        {
            Kind = kind;
            Tag = tag;
            Values = new Dictionary<string, MemoryValueBlueprint>(values, StringComparer.OrdinalIgnoreCase);
        }

        public MemoryGeometryKind Kind { get; private set; }
        public int? Tag { get; private set; }
        public Dictionary<string, MemoryValueBlueprint> Values { get; private set; }
    }

    public sealed class LinkedListMemoryBlueprint
    {
        public string TypeName { get; set; }
        public bool IsContiguous { get; set; }
        public string EndPath { get; set; }
        public string CapacityPath { get; set; }
        public string CountPath { get; set; }
        public string HeadPath { get; set; }
        public string NextPath { get; set; }
        public MemoryValueBlueprint Tag { get; set; }
        public List<MemoryGeometryBlueprint> Geometries { get; private set; } = new List<MemoryGeometryBlueprint>();

        public bool Matches(string typeName)
        {
            return string.Equals(NormalizeTypeName(TypeName), NormalizeTypeName(typeName),
                StringComparison.Ordinal);
        }

        private static string NormalizeTypeName(string value)
        {
            if (value == null) return string.Empty;

            string normalized = value.Trim();
            if (normalized.StartsWith("class ", StringComparison.Ordinal)) normalized = normalized.Substring(6);
            if (normalized.StartsWith("struct ", StringComparison.Ordinal)) normalized = normalized.Substring(7);
            normalized = Regex.Replace(normalized.Trim(), @"\s+", " ");
            return Regex.Replace(normalized, @"\s*([<>,])\s*", "$1");
        }
    }

    /// <summary>
    /// Parses the deliberately small INI format exposed in Tools &gt; Options. Invalid sections are
    /// ignored so an experimental blueprint can never prevent the normal NatVis loader running.
    /// </summary>
    public static class LinkedListMemoryBlueprintParser
    {
        private static readonly string[] PointFields = { "X", "Y" };
        private static readonly string[] LineFields = { "InitialX", "InitialY", "FinalX", "FinalY" };
        private static readonly string[] ArcFields = { "CenterX", "CenterY", "Radius", "InitialAngle", "SweepAngle" };

        public static List<LinkedListMemoryBlueprint> Parse(string text)
        {
            var result = new List<LinkedListMemoryBlueprint>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            string section = null;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)
                        || line.StartsWith(";", StringComparison.Ordinal)) continue;

                    if (line.StartsWith("[", StringComparison.Ordinal)
                        && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        AddSection(result, section, values);
                        section = line.Substring(1, line.Length - 2).Trim();
                        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        continue;
                    }

                    int equals = line.IndexOf('=');
                    if (section == null || equals <= 0) continue;

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.Length > 0 && value.Length > 0) values[key] = value;
                }
            }

            AddSection(result, section, values);
            return result;
        }

        private static void AddSection(List<LinkedListMemoryBlueprint> result, string typeName,
            Dictionary<string, string> values)
        {
            string storage;
            bool contiguous = values.TryGetValue("Storage", out storage)
                && string.Equals(storage, "Contiguous", StringComparison.OrdinalIgnoreCase);
            if (storage != null && !contiguous
                && !string.Equals(storage, "LinkedList", StringComparison.OrdinalIgnoreCase)) return;

            string count = null;
            string head;
            string next = null;
            string end = null;
            string capacity = null;
            if (string.IsNullOrWhiteSpace(typeName)
                || !values.TryGetValue("Head", out head)) return;
            if (contiguous)
            {
                if (!values.TryGetValue("End", out end)
                    || !values.TryGetValue("Capacity", out capacity)) return;
            }
            else if (!values.TryGetValue("Count", out count)
                || !values.TryGetValue("Next", out next)) return;

            var blueprint = new LinkedListMemoryBlueprint
            {
                TypeName = typeName,
                IsContiguous = contiguous,
                EndPath = end,
                CapacityPath = capacity,
                CountPath = count,
                HeadPath = head,
                NextPath = next
            };

            string tag;
            if (values.TryGetValue("Tag", out tag))
            {
                MemoryValueBlueprint tagValue;
                if (!TryParseValue(tag, out tagValue)) return;
                blueprint.Tag = tagValue;
            }

            AddGeometry(blueprint, values, MemoryGeometryKind.Point, PointFields);
            AddGeometry(blueprint, values, MemoryGeometryKind.Line, LineFields);
            AddGeometry(blueprint, values, MemoryGeometryKind.Arc, ArcFields);

            if (blueprint.Geometries.Count == 0) return;
            if (blueprint.Geometries.Count > 1
                && (blueprint.Tag == null || blueprint.Geometries.Exists(geometry => !geometry.Tag.HasValue))) return;

            result.Add(blueprint);
        }

        private static void AddGeometry(LinkedListMemoryBlueprint blueprint,
            Dictionary<string, string> source, MemoryGeometryKind kind, string[] requiredFields)
        {
            string prefix = kind + ".";
            var fields = new Dictionary<string, MemoryValueBlueprint>(StringComparer.OrdinalIgnoreCase);

            foreach (string field in requiredFields)
            {
                string value;
                MemoryValueBlueprint parsed;
                if (!source.TryGetValue(prefix + field, out value) || !TryParseValue(value, out parsed)) return;
                fields[field] = parsed;
            }

            int? tag = null;
            string tagText;
            int tagValue;
            if (source.TryGetValue(prefix + "Tag", out tagText))
            {
                if (!int.TryParse(tagText, out tagValue)) return;
                tag = tagValue;
            }

            blueprint.Geometries.Add(new MemoryGeometryBlueprint(kind, tag, fields));
        }

        private static bool TryParseValue(string text, out MemoryValueBlueprint value)
        {
            value = null;
            int separator = text.LastIndexOf('|');
            if (separator <= 0 || separator == text.Length - 1) return false;

            string path = text.Substring(0, separator).Trim();
            string scalarText = text.Substring(separator + 1).Trim();
            MemoryScalarType scalarType;
            if (path.Length == 0 || !Enum.TryParse(scalarText, true, out scalarType)
                || !Enum.IsDefined(typeof(MemoryScalarType), scalarType)) return false;

            value = new MemoryValueBlueprint(path, scalarType);
            return true;
        }
    }
}
