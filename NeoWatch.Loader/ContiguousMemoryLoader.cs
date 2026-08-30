using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NeoWatch.Drawing;

namespace NeoWatch.Loading
{
    internal sealed partial class LinkedListMemoryLoader
    {
        private static readonly TimeSpan DecodeTimeSlice = TimeSpan.FromMilliseconds(16);

        private async Task<LinkedListMemoryLoadResult> TryLoadContiguousAsync(string name,
            string type, LinkedListMemoryBlueprint blueprint, WatchItem item,
            CancellationToken token, Func<Task> yieldAction)
        {
            token.ThrowIfCancellationRequested();
            MemorySnapshot snapshot;
            ResolvedLayout layout;
            if (!TryReadContiguous(name, type, blueprint, out snapshot, out layout)) return null;

            var drawables = new Drawables { Type = type };
            var element = new byte[snapshot.Stride];
            var sinceYield = Stopwatch.StartNew();
            for (int index = 0; index < snapshot.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                IDrawable drawable;
                Buffer.BlockCopy(snapshot.Bytes, index * snapshot.Stride, element, 0, element.Length);
                if (!TryDecodeDrawable(element, blueprint, layout, out drawable)) return null;
                drawable.Description = "[" + index + "]: " + GeometryName(drawable);
                drawables.Add(drawable);

                // Decoding memory is CPU-only. Yield by elapsed work, not by point count:
                // 3000 cheap points must not wait through 30 unrelated VS dispatcher queues.
                if (sinceYield.Elapsed >= DecodeTimeSlice)
                {
                    item.LoadingCount = index + 1;
                    if (yieldAction != null) await yieldAction();
                    token.ThrowIfCancellationRequested();
                    sinceYield.Restart();
                }
            }

            item.LoadingCount = snapshot.Count;
            item.LoadingTotal = snapshot.Count;
            return new LinkedListMemoryLoadResult { Drawables = drawables, Snapshot = snapshot };
        }

        public ReloadPlan PlanContiguousReload(string name, string type,
            LinkedListMemoryBlueprint blueprint, MemorySnapshot previous, int limit)
        {
            try
            {
                if (blueprint == null || !blueprint.IsContiguous || previous == null
                    || previous.ContiguousBlueprintType != type
                    || previous.ProcessId != debugger.CurrentProcessId) return ReloadPlan.Everything();

                MemorySnapshot current;
                ResolvedLayout layout;
                if (!TryReadContiguous(name, type, blueprint, out current, out layout)) return ReloadPlan.Everything();
                // Reallocation and structural changes get a full, validated decode.
                if (current.Address != previous.Address || current.Count != previous.Count
                    || current.Stride != previous.Stride) return ReloadPlan.Everything();
                if (previous.Matches(current.Bytes)) return ReloadPlan.Nothing();

                var indices = previous.FindChangedElements(current.Bytes, current.Count, limit);
                if (indices == null) return ReloadPlan.Everything();
                var replacements = new List<DrawableReplacement>(indices.Count);
                var element = new byte[current.Stride];
                foreach (int index in indices)
                {
                    IDrawable drawable;
                    Buffer.BlockCopy(current.Bytes, index * current.Stride, element, 0, element.Length);
                    if (!TryDecodeDrawable(element, blueprint, layout, out drawable)) return ReloadPlan.Everything();
                    drawable.Description = "[" + index + "]: " + GeometryName(drawable);
                    replacements.Add(new DrawableReplacement(index, drawable));
                }
                return ReloadPlan.Partial(indices, new List<int>(), current.Count, current, replacements);
            }
            catch (Exception)
            {
                return ReloadPlan.Everything();
            }
        }

        private bool TryReadContiguous(string name, string type, LinkedListMemoryBlueprint blueprint,
            out MemorySnapshot snapshot, out ResolvedLayout layout)
        {
            snapshot = null;
            layout = null;
            if (memoryReader == null || !memoryReader.IsAvailable || debugger.CurrentProcessId == 0) return false;

            ulong head, end, capacity;
            string headType, endType, capacityType;
            string headExpression = MemberExpression(name, blueprint.HeadPath);
            if (!TryReadPointerMember(headExpression, out head, out headType)
                || !TryReadPointerMember(MemberExpression(name, blueprint.EndPath), out end, out endType)
                || !TryReadPointerMember(MemberExpression(name, blueprint.CapacityPath), out capacity, out capacityType)
                || headType != endType || headType != capacityType) return false;
            if (head > end || end > capacity || (head == 0 && capacity != 0)) return false;

            // Empty vectors have no object whose fields we can inspect. Never dereference them.
            if (head == end)
            {
                snapshot = new MemorySnapshot(head, null, 0, 0, true, new byte[0], debugger.CurrentProcessId)
                {
                    ContiguousBlueprintType = type
                };
                return true;
            }

            if (!TryGetLayout(type, headExpression, blueprint, out layout)) return false;
            if (layout.PointerSize == 4 && capacity > uint.MaxValue) return false;
            ulong length = end - head;
            if (length > MaxSnapshotBytes || length % (ulong)layout.NodeSize != 0
                || (capacity - head) % (ulong)layout.NodeSize != 0) return false;

            var bytes = new byte[(int)length];
            if (!memoryReader.TryRead(head, bytes)) return false;
            snapshot = new MemorySnapshot(head, null, layout.NodeSize,
                bytes.Length / layout.NodeSize, true, bytes, debugger.CurrentProcessId)
            {
                ContiguousBlueprintType = type
            };
            return true;
        }

        private bool TryReadPointerMember(string expressionText, out ulong address, out string type)
        {
            address = 0;
            type = null;
            IExpression expression = debugger.GetExpression(expressionText, false);
            if (expression == null || !expression.IsValidValue || string.IsNullOrWhiteSpace(expression.Type)) return false;
            type = Regex.Replace(expression.Type.Trim(), @"\s+", " ");
            type = Regex.Replace(type, @"\s*([<>,*])\s*", "$1");
            if (!type.EndsWith("*", StringComparison.Ordinal)) return false;
            Match match = Regex.Match(expression.Value ?? string.Empty, @"\A0x([0-9a-fA-F`]+)(?:\s.*)?\z");
            return match.Success && ulong.TryParse(match.Groups[1].Value.Replace("`", string.Empty),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        private bool HasScalarType(string headExpression, MemoryValueBlueprint value)
        {
            IExpression expression = debugger.GetExpression("((" + headExpression + ")->" + value.Path + ")", false);
            if (expression == null || !expression.IsValidValue) return false;
            string type = (expression.Type ?? string.Empty).Trim();
            switch (value.ScalarType)
            {
                case MemoryScalarType.Float32: return type == "float";
                case MemoryScalarType.Float64: return type == "double";
                case MemoryScalarType.Int32: return type == "int" || type == "long";
                case MemoryScalarType.UInt32: return type == "unsigned int" || type == "unsigned long";
                case MemoryScalarType.Int64: return type == "__int64" || type == "long long";
                case MemoryScalarType.UInt64: return type == "unsigned __int64" || type == "unsigned long long";
                default: return false;
            }
        }
    }
}
