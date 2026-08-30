using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NeoWatch.Drawing;
using NeoWatch.Geometries;

namespace NeoWatch.Loading
{
    internal sealed class LinkedListMemoryLoadResult
    {
        public Drawables Drawables { get; set; }
        public MemorySnapshot Snapshot { get; set; }
    }

    /// <summary>
    /// Experimental fast path for configured native linked lists. The debugger is only used to
    /// locate the list and resolve a node layout; all elements are decoded from process memory.
    /// </summary>
    internal sealed partial class LinkedListMemoryLoader
    {
        private const long MaxSnapshotBytes = 64L * 1024 * 1024;
        private const int MaxNodeSize = 1024 * 1024;
        private const int YieldEvery = 100;
        private static readonly TimeSpan MaxBetweenYields = TimeSpan.FromMilliseconds(100);

        private readonly IDebugger debugger;
        private readonly IMemoryReader memoryReader;
        private readonly Dictionary<string, ResolvedLayout> layouts =
            new Dictionary<string, ResolvedLayout>(StringComparer.Ordinal);

        public LinkedListMemoryLoader(IDebugger debugger, IMemoryReader memoryReader)
        {
            this.debugger = debugger;
            this.memoryReader = memoryReader;
        }

        public void ClearLayoutCache()
        {
            layouts.Clear();
        }

        public async Task<LinkedListMemoryLoadResult> TryLoadAsync(string expressionName,
            string expressionType, LinkedListMemoryBlueprint blueprint, WatchItem item,
            CancellationToken cancellationToken, Func<Task> yieldAction)
        {
            if (memoryReader == null || !memoryReader.IsAvailable || blueprint == null) return null;

            try
            {
                if (blueprint.IsContiguous)
                {
                    return await TryLoadContiguousAsync(expressionName, expressionType, blueprint,
                        item, cancellationToken, yieldAction);
                }

                int count;
                if (!TryEvaluateInt(MemberExpression(expressionName, blueprint.CountPath), out count)
                    || count < 0) return null;

                var drawables = new Drawables { Type = expressionType };
                if (count == 0)
                {
                    item.LoadingTotal = 0;
                    item.LoadingCount = 0;
                    return new LinkedListMemoryLoadResult { Drawables = drawables };
                }

                string headExpression = MemberExpression(expressionName, blueprint.HeadPath);
                ulong head;
                if (!TryEvaluateAddress("(void*)(" + headExpression + ")", out head) || head == 0) return null;

                ResolvedLayout layout;
                if (!TryGetLayout(expressionType, headExpression, blueprint, out layout)) return null;

                long totalBytes = (long)layout.NodeSize * count;
                if (totalBytes <= 0 || totalBytes > MaxSnapshotBytes) return null;

                var bytes = new byte[(int)totalBytes];
                var addresses = new ulong[count];
                var visited = new HashSet<ulong>();
                var nodeBytes = new byte[layout.NodeSize];
                ulong address = head;
                var sinceLastYield = Stopwatch.StartNew();

                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (address == 0 || !visited.Add(address)) return null;
                    if (!memoryReader.TryRead(address, nodeBytes)) return null;

                    IDrawable drawable;
                    if (!TryDecodeDrawable(nodeBytes, blueprint, layout, out drawable)) return null;
                    drawable.Description = "[" + index + "]: " + GeometryName(drawable);
                    drawables.Add(drawable);

                    addresses[index] = address;
                    Buffer.BlockCopy(nodeBytes, 0, bytes, index * layout.NodeSize, layout.NodeSize);

                    if (index + 1 < count)
                    {
                        if (!TryReadPointer(nodeBytes, layout.NextOffset, layout.PointerSize, out address)) return null;
                    }

                    int completed = index + 1;
                    if (completed % YieldEvery == 0 || sinceLastYield.Elapsed > MaxBetweenYields)
                    {
                        item.LoadingCount = completed;
                        if (yieldAction != null) await yieldAction();
                        sinceLastYield.Restart();
                    }
                }

                item.LoadingTotal = count;
                item.LoadingCount = count;
                return new LinkedListMemoryLoadResult
                {
                    Drawables = drawables,
                    Snapshot = new MemorySnapshot(head, null, layout.NodeSize, count, true, bytes,
                        addresses, debugger.CurrentProcessId)
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // This is an optional fast path. Any unsupported expression, layout or debugger
                // state must fall back to the established NatVis loader.
                return null;
            }
        }

        public bool TryPrepareReplacements(string expressionType, LinkedListMemoryBlueprint blueprint,
            MemorySnapshot previousSnapshot, MemorySnapshot newSnapshot, List<int> indices,
            out List<DrawableReplacement> replacements)
        {
            replacements = null;
            if (blueprint == null || previousSnapshot == null || newSnapshot == null || indices == null)
            {
                return false;
            }

            ResolvedLayout layout;
            string key = LayoutKey(expressionType, blueprint);
            if (!layouts.TryGetValue(key, out layout)) return false;
            if (previousSnapshot.Stride != layout.NodeSize || newSnapshot.Stride != layout.NodeSize)
            {
                return false;
            }

            // A changed Next means that visible order or membership may have changed. The old
            // address table is then no longer authoritative, so let the full traversal rebuild it.
            if (!LinksMatch(previousSnapshot.Bytes, newSnapshot.Bytes, previousSnapshot.Count,
                layout.NextOffset, layout.PointerSize, layout.NodeSize)) return false;

            var prepared = new List<DrawableReplacement>(indices.Count);
            var nodeBytes = new byte[layout.NodeSize];
            foreach (int index in indices)
            {
                if (index < 0 || index >= newSnapshot.Count) return false;

                Buffer.BlockCopy(newSnapshot.Bytes, index * layout.NodeSize,
                    nodeBytes, 0, layout.NodeSize);

                IDrawable drawable;
                if (!TryDecodeDrawable(nodeBytes, blueprint, layout, out drawable)) return false;
                drawable.Description = "[" + index + "]: " + GeometryName(drawable);
                prepared.Add(new DrawableReplacement(index, drawable));
            }

            replacements = prepared;
            return true;
        }

        private static bool LinksMatch(byte[] previous, byte[] current, int count,
            int nextOffset, int pointerSize, int stride)
        {
            if (previous == null || current == null || previous.Length != current.Length) return false;

            for (int index = 0; index < count; index++)
            {
                int start = index * stride + nextOffset;
                for (int offset = 0; offset < pointerSize; offset++)
                {
                    if (previous[start + offset] != current[start + offset]) return false;
                }
            }

            return true;
        }

        private bool TryGetLayout(string expressionType, string headExpression,
            LinkedListMemoryBlueprint blueprint, out ResolvedLayout layout)
        {
            string key = LayoutKey(expressionType, blueprint);
            if (layouts.TryGetValue(key, out layout)) return true;

            int nodeSize;
            int pointerSize;
            if (!TryEvaluateInt("sizeof(*(" + headExpression + "))", out nodeSize)) return false;
            if (!TryEvaluateInt("sizeof(void*)", out pointerSize)) return false;
            if (nodeSize <= 0 || nodeSize > MaxNodeSize || (pointerSize != 4 && pointerSize != 8)) return false;

            int nextOffset = 0;
            if (!blueprint.IsContiguous && (!TryResolveOffset(headExpression, blueprint.NextPath, out nextOffset)
                || !Fits(nextOffset, pointerSize, nodeSize))) return false;

            var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
            if (blueprint.Tag != null && !TryAddOffset(headExpression, blueprint.Tag, nodeSize, offsets)) return false;

            foreach (MemoryGeometryBlueprint geometry in blueprint.Geometries)
            {
                foreach (MemoryValueBlueprint value in geometry.Values.Values)
                {
                    if (!TryAddOffset(headExpression, value, nodeSize, offsets)) return false;
                }
            }

            if (blueprint.IsContiguous)
            {
                if (blueprint.Tag != null && !HasScalarType(headExpression, blueprint.Tag)) return false;
                foreach (MemoryGeometryBlueprint geometry in blueprint.Geometries)
                {
                    foreach (MemoryValueBlueprint value in geometry.Values.Values)
                    {
                        if (!HasScalarType(headExpression, value)) return false;
                    }
                }
            }

            layout = new ResolvedLayout
            {
                NodeSize = nodeSize,
                PointerSize = pointerSize,
                NextOffset = nextOffset,
                ValueOffsets = offsets
            };
            layouts[key] = layout;
            return true;
        }

        private string LayoutKey(string expressionType, LinkedListMemoryBlueprint blueprint)
        {
            return debugger.CurrentProcessId + "|" + expressionType + "|" + blueprint.TypeName;
        }

        private bool TryAddOffset(string headExpression, MemoryValueBlueprint value, int nodeSize,
            Dictionary<string, int> offsets)
        {
            if (offsets.ContainsKey(value.Path)) return true;

            int offset;
            if (!TryResolveOffset(headExpression, value.Path, out offset)
                || !Fits(offset, value.Size, nodeSize)) return false;
            offsets[value.Path] = offset;
            return true;
        }

        private bool TryResolveOffset(string headExpression, string path, out int offset)
        {
            string valueExpression = "((" + headExpression + ")->" + path + ")";
            string offsetExpression = "(long long)((char*)&(" + valueExpression
                + ") - (char*)(" + headExpression + "))";
            return TryEvaluateInt(offsetExpression, out offset) && offset >= 0;
        }

        private static bool Fits(int offset, int size, int nodeSize)
        {
            return offset >= 0 && size > 0 && offset <= nodeSize - size;
        }

        private bool TryDecodeDrawable(byte[] bytes, LinkedListMemoryBlueprint blueprint,
            ResolvedLayout layout, out IDrawable drawable)
        {
            drawable = null;
            MemoryGeometryBlueprint geometry = null;

            if (blueprint.Geometries.Count == 1 && blueprint.Tag == null)
            {
                geometry = blueprint.Geometries[0];
            }
            else
            {
                long tag;
                if (blueprint.Tag == null || !TryReadInteger(bytes, blueprint.Tag, layout, out tag)) return false;
                geometry = blueprint.Geometries.Find(candidate => candidate.Tag.HasValue && candidate.Tag.Value == tag);
            }

            if (geometry == null) return false;

            double first;
            double second;
            double third;
            double fourth;
            double fifth;
            switch (geometry.Kind)
            {
                case MemoryGeometryKind.Point:
                    if (!TryReadNumber(bytes, geometry.Values["X"], layout, out first)
                        || !TryReadNumber(bytes, geometry.Values["Y"], layout, out second)) return false;
                    drawable = new DrawablePoint((float)first, (float)second);
                    return true;

                case MemoryGeometryKind.Line:
                    if (!TryReadNumber(bytes, geometry.Values["InitialX"], layout, out first)
                        || !TryReadNumber(bytes, geometry.Values["InitialY"], layout, out second)
                        || !TryReadNumber(bytes, geometry.Values["FinalX"], layout, out third)
                        || !TryReadNumber(bytes, geometry.Values["FinalY"], layout, out fourth)) return false;
                    drawable = new DrawableLineSegment(new Point((float)first, (float)second),
                        new Point((float)third, (float)fourth));
                    return true;

                case MemoryGeometryKind.Arc:
                    if (!TryReadNumber(bytes, geometry.Values["CenterX"], layout, out first)
                        || !TryReadNumber(bytes, geometry.Values["CenterY"], layout, out second)
                        || !TryReadNumber(bytes, geometry.Values["Radius"], layout, out third)
                        || !TryReadNumber(bytes, geometry.Values["InitialAngle"], layout, out fourth)
                        || !TryReadNumber(bytes, geometry.Values["SweepAngle"], layout, out fifth)) return false;
                    drawable = new DrawableArcSegment(new Point((float)first, (float)second),
                        (float)fourth, (float)fifth, (float)third);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryReadNumber(byte[] bytes, MemoryValueBlueprint value,
            ResolvedLayout layout, out double result)
        {
            result = 0;
            int offset;
            if (!layout.ValueOffsets.TryGetValue(value.Path, out offset)) return false;

            switch (value.ScalarType)
            {
                case MemoryScalarType.Float32:
                    result = BitConverter.ToSingle(bytes, offset);
                    break;
                case MemoryScalarType.Float64:
                    result = BitConverter.ToDouble(bytes, offset);
                    break;
                case MemoryScalarType.Int32:
                    result = BitConverter.ToInt32(bytes, offset);
                    break;
                case MemoryScalarType.UInt32:
                    result = BitConverter.ToUInt32(bytes, offset);
                    break;
                case MemoryScalarType.Int64:
                    result = BitConverter.ToInt64(bytes, offset);
                    break;
                case MemoryScalarType.UInt64:
                    result = BitConverter.ToUInt64(bytes, offset);
                    break;
                default:
                    return false;
            }

            return !double.IsNaN(result) && !double.IsInfinity(result)
                && result <= float.MaxValue && result >= -float.MaxValue;
        }

        private static bool TryReadInteger(byte[] bytes, MemoryValueBlueprint value,
            ResolvedLayout layout, out long result)
        {
            result = 0;
            int offset;
            if (!layout.ValueOffsets.TryGetValue(value.Path, out offset)) return false;

            switch (value.ScalarType)
            {
                case MemoryScalarType.Int32:
                    result = BitConverter.ToInt32(bytes, offset);
                    return true;
                case MemoryScalarType.UInt32:
                    result = BitConverter.ToUInt32(bytes, offset);
                    return true;
                case MemoryScalarType.Int64:
                    result = BitConverter.ToInt64(bytes, offset);
                    return true;
                case MemoryScalarType.UInt64:
                    ulong unsigned = BitConverter.ToUInt64(bytes, offset);
                    if (unsigned > long.MaxValue) return false;
                    result = (long)unsigned;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadPointer(byte[] bytes, int offset, int pointerSize, out ulong address)
        {
            address = 0;
            if (!Fits(offset, pointerSize, bytes.Length)) return false;

            address = pointerSize == 8 ? BitConverter.ToUInt64(bytes, offset) : BitConverter.ToUInt32(bytes, offset);
            return true;
        }

        private bool TryEvaluateInt(string expressionText, out int value)
        {
            value = 0;
            long parsed;
            return TryEvaluateInteger(expressionText, out parsed)
                && parsed >= int.MinValue && parsed <= int.MaxValue
                && (value = (int)parsed) == parsed;
        }

        private bool TryEvaluateAddress(string expressionText, out ulong address)
        {
            address = 0;
            string text = EvaluateValue(expressionText);
            if (text == null) return false;

            Match hex = Regex.Match(text, @"0x([0-9a-fA-F`]+)");
            if (!hex.Success) return false;

            string digits = hex.Groups[1].Value.Replace("`", string.Empty);
            return ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        private bool TryEvaluateInteger(string expressionText, out long value)
        {
            value = 0;
            string text = EvaluateValue(expressionText);
            if (text == null) return false;

            Match hex = Regex.Match(text, @"0x([0-9a-fA-F`]+)");
            if (hex.Success)
            {
                ulong unsigned;
                string digits = hex.Groups[1].Value.Replace("`", string.Empty);
                if (!ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out unsigned)
                    || unsigned > long.MaxValue) return false;
                value = (long)unsigned;
                return true;
            }

            Match decimalValue = Regex.Match(text, @"-?\d+");
            return decimalValue.Success && long.TryParse(decimalValue.Value,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private string EvaluateValue(string expressionText)
        {
            try
            {
                IExpression expression = debugger.GetExpression(expressionText, false);
                return expression == null || !expression.IsValidValue
                    || string.IsNullOrEmpty(expression.Value) ? null : expression.Value;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static string MemberExpression(string expressionName, string path)
        {
            return "(" + expressionName + ")." + path;
        }

        private static string GeometryName(IDrawable drawable)
        {
            if (drawable is DrawablePoint) return "Point";
            if (drawable is DrawableLineSegment) return "Line";
            if (drawable is DrawableArcSegment) return "Arc";
            return "Geometry";
        }

        private sealed class ResolvedLayout
        {
            public int NodeSize;
            public int PointerSize;
            public int NextOffset;
            public Dictionary<string, int> ValueOffsets;
        }
    }
}
