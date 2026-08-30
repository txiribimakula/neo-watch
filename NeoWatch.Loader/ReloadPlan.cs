using System.Collections.Generic;
using NeoWatch.Drawing;

namespace NeoWatch.Loading
{
    /// <summary>
    /// What a break costs for one watch item, decided from its memory rather than from walking it.
    /// </summary>
    public class ReloadPlan
    {
        private static readonly List<int> None = new List<int>();

        private ReloadPlan(ReloadScope scope, List<int> changedIndices, List<int> addedIndices,
            int finalCount, MemorySnapshot newSnapshot, List<DrawableReplacement> preparedReplacements)
        {
            Scope = scope;
            ChangedIndices = changedIndices ?? None;
            AddedIndices = addedIndices ?? None;
            FinalCount = finalCount;
            NewSnapshot = newSnapshot;
            PreparedReplacements = preparedReplacements;
        }

        public ReloadScope Scope { get; private set; }

        /// <summary>Existing positions whose bytes moved, to be replaced in place. Ascending.</summary>
        public List<int> ChangedIndices { get; private set; }

        /// <summary>Positions that did not exist before, to be loaded and appended. Ascending.</summary>
        public List<int> AddedIndices { get; private set; }

        /// <summary>Element count after the change. Below the previous one means it shrank.</summary>
        public int FinalCount { get; private set; }

        /// <summary>
        /// The baseline to keep once the partial update lands. Committing it is the caller's job,
        /// and only after the update actually succeeded — otherwise the next break would compare
        /// against bytes that were never drawn.
        /// </summary>
        public MemorySnapshot NewSnapshot { get; private set; }

        /// <summary>
        /// Drawables already decoded from the bytes read while planning. Avoids reading changed
        /// linked-list nodes a second time merely to rebuild their geometry.
        /// </summary>
        public List<DrawableReplacement> PreparedReplacements { get; private set; }

        public bool IsUnchanged { get { return Scope == ReloadScope.Nothing; } }
        public bool IsPartial { get { return Scope == ReloadScope.Partial; } }

        public static ReloadPlan Everything()
        {
            return new ReloadPlan(ReloadScope.Everything, null, null, 0, null, null);
        }

        public static ReloadPlan Nothing()
        {
            return new ReloadPlan(ReloadScope.Nothing, null, null, 0, null, null);
        }

        public static ReloadPlan Partial(List<int> changedIndices, List<int> addedIndices, int finalCount, MemorySnapshot newSnapshot)
        {
            return Partial(changedIndices, addedIndices, finalCount, newSnapshot, null);
        }

        public static ReloadPlan Partial(List<int> changedIndices, List<int> addedIndices,
            int finalCount, MemorySnapshot newSnapshot, List<DrawableReplacement> preparedReplacements)
        {
            return new ReloadPlan(ReloadScope.Partial, changedIndices, addedIndices, finalCount,
                newSnapshot, preparedReplacements);
        }
    }

    public enum ReloadScope
    {
        /// <summary>The default whenever there is any doubt: walk everything through DTE.</summary>
        Everything,

        /// <summary>Byte for byte identical. Nothing to load, nothing to redraw.</summary>
        Nothing,

        /// <summary>Some elements moved, or the tail grew or shrank. Touch only those.</summary>
        Partial
    }
}
