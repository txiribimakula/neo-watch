using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NeoWatch.Drawing;
using NeoWatch.Common;

namespace NeoWatch.Loading
{
    public class Loader
    {
        private IDebugger debugger;
        private readonly LinkedListMemoryLoader linkedListMemoryLoader;
        private List<LinkedListMemoryBlueprint> linkedListMemoryBlueprints = new List<LinkedListMemoryBlueprint>();
        private bool isLinkedListMemoryLoadingEnabled;

        public IInterpreter Interpreter { get; set; }

        public Func<Task> YieldAction { get; set; } = async () => { await Task.Yield(); };

        public Loader(IDebugger debugger, IInterpreter interpreter, IMemoryReader memoryReader = null)
        {
            this.debugger = debugger;
            Interpreter = interpreter;
            MemoryReader = memoryReader;
            linkedListMemoryLoader = new LinkedListMemoryLoader(debugger, memoryReader);
        }

        /// <summary>
        /// Optional. When present, a container's bytes are snapshotted after each load so the next
        /// break can tell what moved without walking the elements. Null disables all of it.
        /// </summary>
        public IMemoryReader MemoryReader { get; set; }

        public void ConfigureLinkedListMemoryLoading(bool enabled, string blueprints)
        {
            isLinkedListMemoryLoadingEnabled = enabled;
            linkedListMemoryBlueprints = LinkedListMemoryBlueprintParser.Parse(blueprints);
            linkedListMemoryLoader.ClearLayoutCache();
        }

        public void ResetDebugSession()
        {
            linkedListMemoryLoader.ClearLayoutCache();
            if (MemoryReader != null) MemoryReader.Reset();
        }

        private static readonly string[] ListTypes =
        {
            "std::vector",
            "std::array",
            "System.Collections.Generic.List"
        };

        /// <summary>
        /// Fraction of a container that can change before reloading it whole beats reloading the
        /// changed elements one by one. Each targeted reload re-evaluates the container and the
        /// index, which a single sweep does once. Measure with the benchmark before moving it.
        /// </summary>
        public double PartialReloadFraction { get; set; } = 0.10;

        /// <summary>Small collections still get the targeted path even above the fraction.</summary>
        public int PartialReloadFloor { get; set; } = 8;

        /// <summary>
        /// Decides what this break actually costs, from memory rather than from walking elements.
        ///
        /// Two debugger calls and one block read, regardless of how many elements there are. Any
        /// doubt at all answers Everything and lets the normal path run: a needless reload only
        /// costs time, whereas a missed change would draw stale data.
        /// </summary>
        public ReloadPlan PlanReload(WatchItem item)
        {
            MemorySnapshot snapshot = item.Snapshot;
            if (snapshot == null) return ReloadPlan.Everything();
            if (MemoryReader == null || !MemoryReader.IsAvailable) return ReloadPlan.Everything();
            if (snapshot.ProcessId != 0 && snapshot.ProcessId != debugger.CurrentProcessId)
            {
                return ReloadPlan.Everything();
            }

            IExpression expression;
            try
            {
                // A linked-list snapshot only needs the root type here. Avoid asking NatVis to
                // expand the whole list just to decide whether its bytes changed.
                expression = debugger.GetExpression(item.Name,
                    !snapshot.IsSegmented && snapshot.ContiguousBlueprintType == null);
            }
            catch (COMException)
            {
                return ReloadPlan.Everything();
            }

            if (expression == null || !expression.IsValidValue
                || string.IsNullOrEmpty(expression.Type)) return ReloadPlan.Everything();

            if (snapshot.ContiguousBlueprintType != null)
            {
                return linkedListMemoryLoader.PlanContiguousReload(item.Name, expression.Type,
                    FindLinkedListMemoryBlueprint(expression.Type), snapshot, PartialReloadLimit(snapshot.Count));
            }

            if (snapshot.IsSegmented)
            {
                return PlanSegmentedReload(item, snapshot, expression.Type,
                    FindLinkedListMemoryBlueprint(expression.Type));
            }

            // The container's own display string is the cheapest signal there is: identical means
            // the same number of elements, different usually means it grew or shrank.
            int finalCount;
            bool sameSize = expression.Value == snapshot.ContainerValue;
            if (sameSize)
            {
                finalCount = snapshot.Count;
            }
            else if (!TryGetElementCount(expression.Value, out finalCount) || finalCount <= 0)
            {
                // Could not tell what it says now, so no assumptions.
                return ReloadPlan.Everything();
            }

            ulong address;
            if (!TryGetElementAddress(item.Name, out address)) return ReloadPlan.Everything();
            // The address moving while the size held still is not something to reason about.
            // Growth is different: push_back reallocates, and that is expected.
            if (sameSize && address != snapshot.Address) return ReloadPlan.Everything();

            long blockSize = (long)snapshot.Stride * finalCount;
            if (blockSize <= 0 || blockSize > MaxSnapshotBytes) return ReloadPlan.Everything();

            var buffer = new byte[blockSize];
            if (!MemoryReader.TryRead(address, buffer)) return ReloadPlan.Everything();

            if (sameSize && snapshot.Matches(buffer)) return ReloadPlan.Nothing();

            // Only the elements both blocks have in common; the tail is handled from the counts.
            List<int> changed = snapshot.FindChangedElements(buffer, finalCount, PartialReloadLimit(snapshot.Count));
            if (changed == null) return ReloadPlan.Everything();

            var added = new List<int>();
            for (int index = snapshot.Count; index < finalCount; index++)
            {
                added.Add(index);
            }

            if (changed.Count == 0 && added.Count == 0 && finalCount == snapshot.Count)
            {
                return ReloadPlan.Nothing();
            }

            // Loading a long tail one element at a time loses to a single sweep, same as replacing.
            if (changed.Count + added.Count > PartialReloadLimit(snapshot.Count)) return ReloadPlan.Everything();

            var newSnapshot = new MemorySnapshot(address, expression.Value, snapshot.Stride,
                finalCount, true, buffer, debugger.CurrentProcessId);
            return ReloadPlan.Partial(changed, added, finalCount, newSnapshot);
        }

        private ReloadPlan PlanSegmentedReload(WatchItem item, MemorySnapshot snapshot,
            string expressionType, LinkedListMemoryBlueprint blueprint)
        {
            string countExpression = blueprint == null
                ? item.Name + ".Count"
                : "(" + item.Name + ")." + blueprint.CountPath;
            string headExpression = blueprint == null
                ? item.Name + ".Head"
                : "(" + item.Name + ")." + blueprint.HeadPath;

            int finalCount;
            if (!TryEvaluateInt(countExpression, out finalCount)) return ReloadPlan.Everything();
            if (finalCount != snapshot.Count) return ReloadPlan.Everything();

            ulong head;
            if (!TryGetPointerAddress(headExpression, out head)) return ReloadPlan.Everything();
            if (head != snapshot.Address) return ReloadPlan.Everything();

            byte[] buffer;
            if (!TryReadSegmented(snapshot.ElementAddresses, snapshot.Stride, out buffer)) return ReloadPlan.Everything();

            if (snapshot.Matches(buffer)) return ReloadPlan.Nothing();

            List<int> changed = snapshot.FindChangedElements(buffer, finalCount,
                PartialReloadLimit(snapshot.Count));
            if (changed == null) return ReloadPlan.Everything();

            var newSnapshot = new MemorySnapshot(head, null, snapshot.Stride, finalCount,
                true, buffer, snapshot.ElementAddresses, debugger.CurrentProcessId);
            List<DrawableReplacement> prepared;
            if (!linkedListMemoryLoader.TryPrepareReplacements(expressionType, blueprint, snapshot,
                newSnapshot, changed, out prepared)) return ReloadPlan.Everything();

            return ReloadPlan.Partial(changed, new List<int>(), finalCount, newSnapshot, prepared);
        }

        /// <summary>
        /// Pulls the element count out of a container's display string: "{ size=3001 }" in C++,
        /// "Count = 3001" in C#. Interpreting it rather than just comparing it is what makes the
        /// grow case possible, and also the one thing here that could break on a different STL.
        /// Failing to parse only costs a full reload.
        /// </summary>
        private static bool TryGetElementCount(string containerValue, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(containerValue)) return false;

            Match match = Regex.Match(containerValue, @"(?:size|Count)\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
        }

        private int PartialReloadLimit(int count)
        {
            int fromFraction = (int)(count * PartialReloadFraction);
            return fromFraction > PartialReloadFloor ? fromFraction : PartialReloadFloor;
        }

        /// <summary>
        /// Re-reads just the elements the diff flagged. Returns null if anything is not exactly as
        /// expected, which sends the caller back to a full reload.
        /// </summary>
        public List<DrawableReplacement> ReloadElements(WatchItem item, List<int> indices,
            ReloadPlan plan = null)
        {
            if (plan != null && plan.PreparedReplacements != null)
            {
                var prepared = new List<DrawableReplacement>(indices.Count);
                foreach (int index in indices)
                {
                    DrawableReplacement replacement = plan.PreparedReplacements.Find(
                        candidate => candidate.Index == index);
                    if (replacement == null) return null;
                    prepared.Add(replacement);
                }
                return prepared;
            }

            var replacements = new List<DrawableReplacement>(indices.Count);

            foreach (int index in indices)
            {
                IExpression element;
                try
                {
                    element = debugger.GetExpression(item.Name + "[" + index + "]");
                }
                catch (COMException)
                {
                    return null;
                }

                if (element == null || string.IsNullOrEmpty(element.Type)) return null;

                Result<IDrawable> drawableResult = Interpreter.GetDrawable(element);
                if (drawableResult == null || drawableResult.Feedback.HasError || drawableResult.Data == null)
                {
                    return null;
                }

                // Same shape the full sweep gives them, so the Items dropdown stays consistent.
                drawableResult.Data.Description = "[" + index + "]: " + drawableResult.Data.Description;
                replacements.Add(new DrawableReplacement(index, drawableResult.Data));
            }

            return replacements;
        }

        /// <summary>
        /// Keeps the freshly read block as the new baseline. Only for a partial update that
        /// actually landed: otherwise the next break would compare against bytes never drawn.
        /// </summary>
        public void CommitSnapshot(WatchItem item, ReloadPlan plan)
        {
            if (plan == null || plan.NewSnapshot == null) return;
            item.Snapshot = plan.NewSnapshot;
        }

        /// <summary>Records where the elements live and what they held, for the next break.</summary>
        private void CaptureSnapshot(WatchItem item, IExpression expression, int elementCount, int drawableCount)
        {
            item.Snapshot = null;

            if (MemoryReader == null || !MemoryReader.IsAvailable) return;
            if (elementCount <= 0) return;

            // Indexable containers only. Not merely "something that expands": a linked list or a
            // NatVis-synthesised expansion has no operator[] and no contiguous block, so asking for
            // the address of its first element is a malformed expression.
            if (!new ExpressionLoader(expression, ListTypes).IsIndexableContainer)
            {
                TryCaptureLinkedListSnapshot(item, expression, elementCount, drawableCount);
                return;
            }

            ulong address;
            if (!TryGetElementAddress(item.Name, out address)) return;

            int stride;
            if (!TryGetElementSize(item.Name, out stride)) return;

            long blockSize = (long)stride * elementCount;
            if (blockSize <= 0 || blockSize > MaxSnapshotBytes) return;

            var buffer = new byte[blockSize];
            if (!MemoryReader.TryRead(address, buffer)) return;

            // One element to one drawable is what makes an index in the diff mean an index in the
            // collection. NatVis that expands an element into several breaks that correspondence,
            // so those keep whole-block detection but lose the targeted reload.
            bool supportsPartial = elementCount == drawableCount;

            item.Snapshot = new MemorySnapshot(address, expression.Value, stride, elementCount,
                supportsPartial, buffer, debugger.CurrentProcessId);
        }

        private bool TryCaptureLinkedListSnapshot(WatchItem item, IExpression expression, int elementCount, int drawableCount)
        {
            if (elementCount != drawableCount) return false;

            int count;
            if (!TryEvaluateInt(item.Name + ".Count", out count)) return false;
            if (count != elementCount || count <= 0) return false;

            ulong head;
            if (!TryGetPointerAddress(item.Name + ".Head", out head)) return false;
            if (head == 0) return false;

            int stride;
            if (!TryEvaluateInt("sizeof(*(" + item.Name + ".Head))", out stride)) return false;
            if (stride <= 0) return false;

            long blockSize = (long)stride * count;
            if (blockSize <= 0 || blockSize > MaxSnapshotBytes) return false;

            var addresses = new ulong[count];
            string nodeExpression = item.Name + ".Head";
            for (int index = 0; index < count; index++)
            {
                ulong nodeAddress;
                if (!TryGetPointerAddress(nodeExpression, out nodeAddress)) return false;
                if (nodeAddress == 0) return false;

                addresses[index] = nodeAddress;
                nodeExpression += "->Next";
            }

            byte[] buffer;
            if (!TryReadSegmented(addresses, stride, out buffer)) return false;

            item.Snapshot = new MemorySnapshot(head, expression.Value, stride, count, false,
                buffer, addresses, debugger.CurrentProcessId);
            return true;
        }

        private bool TryReadSegmented(ulong[] addresses, int stride, out byte[] buffer)
        {
            buffer = null;
            if (addresses == null || stride <= 0) return false;

            long blockSize = (long)stride * addresses.Length;
            if (blockSize <= 0 || blockSize > MaxSnapshotBytes) return false;

            buffer = new byte[blockSize];
            var nodeBytes = new byte[stride];
            for (int index = 0; index < addresses.Length; index++)
            {
                if (addresses[index] == 0) return false;
                if (!MemoryReader.TryRead(addresses[index], nodeBytes)) return false;

                Buffer.BlockCopy(nodeBytes, 0, buffer, index * stride, stride);
            }

            return true;
        }

        // 64 MB is far past any collection worth drawing, and keeps a runaway expression from
        // allocating the extension out of memory.
        private const long MaxSnapshotBytes = 64L * 1024 * 1024;

        private bool TryGetElementAddress(string name, out ulong address)
        {
            address = 0;
            // Empty containers have no [0] to take the address of; those simply get no snapshot.
            string value = EvaluateValue("(void*)&(" + name + ")[0]");
            if (value == null) return false;

            Match match = Regex.Match(value, @"0x([0-9a-fA-F]+)");
            if (!match.Success) return false;

            return ulong.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        private bool TryGetElementSize(string name, out int size)
        {
            size = 0;
            // sizeof is resolved at compile time by the evaluator: no call into the debuggee.
            string value = EvaluateValue("sizeof((" + name + ")[0])");
            if (value == null) return false;

            Match match = Regex.Match(value, @"\d+");
            return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size) && size > 0;
        }

        private bool TryEvaluateInt(string expressionText, out int value)
        {
            value = 0;
            string text = EvaluateValue(expressionText);
            if (text == null) return false;

            Match match = Regex.Match(text, @"\d+");
            return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private bool TryGetPointerAddress(string expressionText, out ulong address)
        {
            address = 0;
            string value = EvaluateValue("(void*)(" + expressionText + ")");
            if (value == null) return false;

            Match match = Regex.Match(value, @"0x([0-9a-fA-F]+)");
            if (!match.Success) return false;

            return ulong.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        private string EvaluateValue(string expressionText)
        {
            try
            {
                IExpression expression = debugger.GetExpression(expressionText);
                if (expression == null) return null;

                string value = expression.Value;
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private const int YieldEvery = 100;
        private static readonly TimeSpan MaxBetweenYields = TimeSpan.FromMilliseconds(100);

        public async Task<Result<Drawables>> Load(WatchItem item, CancellationToken cancellationToken = default(CancellationToken)) {

            IExpression expression = null;

            // Match the native Watch window's first step while the experimental loader is active:
            // resolve the root without running NatVis. An out-of-scope name is then rejected before
            // any collection expansion, while a recognised blueprint can stay on the fast path.
            if (isLinkedListMemoryLoadingEnabled)
            {
                try
                {
                    expression = debugger.GetExpression(item.Name, false);
                }
                catch (COMException)
                {
                    return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException, item.Name));
                }

                if (expression == null || !expression.IsValidValue
                    || string.IsNullOrEmpty(expression.Type))
                {
                    return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException, item.Name));
                }

                LinkedListMemoryBlueprint fastBlueprint = FindLinkedListMemoryBlueprint(expression.Type);
                if (fastBlueprint != null)
                {
                    LinkedListMemoryLoadResult fastResult = await linkedListMemoryLoader.TryLoadAsync(
                        item.Name, expression.Type, fastBlueprint, item, cancellationToken, YieldAction);
                    if (fastResult != null)
                    {
                        item.Snapshot = fastResult.Snapshot;
                        return new Result<Drawables>(fastResult.Drawables);
                    }
                }

                // The probe was valid but the experimental path could not prove the layout. Keep
                // the established NatVis path as the source of truth.
                expression = null;
            }

            try
            {
                expression = debugger.GetExpression(item.Name);
            }
            catch (COMException)
            {
                return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException, item.Name));
            }

            if (expression == null || !expression.IsValidValue || string.IsNullOrEmpty(expression.Type))
            {
                // Named, so the Status column says which expression the debugger would not resolve.
                return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException, item.Name));
            }

            var counter = new ElementCounter();
            var drawablesResult = await GetDrawablesAsync(expression, item, cancellationToken, counter);
            if(drawablesResult.Data != null)
            {
                drawablesResult.Data.Type = expression.Type;

                if (!drawablesResult.Feedback.HasError)
                {
                    CaptureSnapshot(item, expression, counter.Count, drawablesResult.Data.Count);
                }
                else
                {
                    // A partial or failed load must not be used as a baseline.
                    item.Snapshot = null;
                }
            }

            return drawablesResult;
        }

        private LinkedListMemoryBlueprint FindLinkedListMemoryBlueprint(string typeName)
        {
            if (!isLinkedListMemoryLoadingEnabled || string.IsNullOrEmpty(typeName)) return null;
            return linkedListMemoryBlueprints.Find(blueprint => blueprint.Matches(typeName));
        }

        /// <summary>
        /// Container elements seen, which is not the same as drawables produced: one element can
        /// expand into several through NatVis. Carried in a holder rather than returned so the
        /// caller reads it after awaiting, when the sweep has finished counting.
        /// </summary>
        private class ElementCounter
        {
            public int Count;
        }

        private async Task<Result<Drawables>> GetDrawablesAsync(IExpression itemExpression, WatchItem item, CancellationToken cancellationToken, ElementCounter counter)
        {
            var drawables = new Drawables();

            var listTypes = ListTypes;

            var expressions = new ExpressionLoader(itemExpression, listTypes);

            var sinceLastYield = Stopwatch.StartNew();
            var currentIndex = 0;
            foreach (IExpression expression in expressions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new Result<Drawables>(drawables, new Feedback(FeedbackType.Cancelled));
                }

                counter.Count++;

                var innerExpressions = new ExpressionLoader(expression, listTypes);
                int innerIndex = 0;

                foreach (IExpression innerExpression in innerExpressions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new Result<Drawables>(drawables, new Feedback(FeedbackType.Cancelled));
                    }

                    var newDrawableResult = Interpreter.GetDrawable(innerExpression);

                    // Reacquire the same NatVis path once. Evaluating C++ operator[] loses custom
                    // views and cannot address synthetic children or nested linked-list items.
                    if (newDrawableResult == null || newDrawableResult.Feedback.HasError)
                    {
                        var retry = ReinterpretElement(item.Name, itemExpression,
                            expressions.IsList, counter.Count - 1, expression,
                            innerExpressions.IsList, innerIndex, innerExpression);
                        if (retry != null && !retry.Feedback.HasError) newDrawableResult = retry;
                    }

                    if (newDrawableResult == null)
                    {
                        return new Result<Drawables>(drawables,
                            new Feedback(FeedbackType.ExpressionLoadException, item.Name));
                    }
                    if (newDrawableResult.Feedback.HasError)
                    {
                        return new Result<Drawables>(drawables, newDrawableResult.Feedback);
                    }

                    newDrawableResult.Data.Description = "[" + drawables.Count + "]: " + newDrawableResult.Data.Description;
                    drawables.Add(newDrawableResult.Data);

                    currentIndex++;
                    innerIndex++;
                    if (currentIndex % YieldEvery == 0 || sinceLastYield.Elapsed > MaxBetweenYields)
                    {
                        item.LoadingCount = currentIndex;
                        await YieldAction();
                        sinceLastYield.Restart();
                    }
                }
            }

            item.LoadingTotal = currentIndex;
            item.LoadingCount = currentIndex;

            return new Result<Drawables>(drawables);
        }

        private Result<IDrawable> ReinterpretElement(string name, IExpression root, bool isList,
            int outerIndex, IExpression outer, bool isNestedList, int innerIndex, IExpression leaf)
        {
            try
            {
                IExpression fresh = debugger.GetExpression(name);
                if (!HasSameValidType(fresh, root)) return null;
                if (isList) fresh = fresh.DataMembers?.GetAt(outerIndex);
                if (!HasSameValidType(fresh, outer)) return null;
                if (isNestedList) fresh = fresh.DataMembers?.GetAt(innerIndex);
                return HasSameValidType(fresh, leaf) ? Interpreter.GetDrawable(fresh) : null;
            }
            catch (COMException)
            {
                // Preserve the original error rather than skip an element or reuse stale data.
                return null;
            }
        }

        private static bool HasSameValidType(IExpression current, IExpression previous)
        {
            return current != null && current.IsValidValue && current.Type == previous.Type;
        }
    }
}
