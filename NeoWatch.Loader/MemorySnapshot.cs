using System.Collections.Generic;

namespace NeoWatch.Loading
{
    /// <summary>
    /// What a watch item looked like in memory at the end of its last load, so the next break can
    /// tell whether anything changed without walking the elements again.
    ///
    /// The normal NatVis path only compares these bytes. The experimental memory-blueprint path
    /// also decodes them into drawables, then keeps the same buffer as its comparison baseline.
    /// In either case, identical bytes mean an identical drawing.
    /// </summary>
    public class MemorySnapshot
    {
        public MemorySnapshot(ulong address, string containerValue, int stride, int count,
            bool supportsPartial, byte[] bytes, int processId = 0)
            : this(address, containerValue, stride, count, supportsPartial, bytes, null, processId)
        {
        }

        public MemorySnapshot(ulong address, string containerValue, int stride, int count,
            bool supportsPartial, byte[] bytes, ulong[] elementAddresses, int processId = 0)
        {
            Address = address;
            ContainerValue = containerValue;
            Stride = stride;
            Count = count;
            SupportsPartial = supportsPartial;
            Bytes = bytes;
            ElementAddresses = elementAddresses;
            ProcessId = processId;
        }

        /// <summary>Debuggee process that produced this snapshot. Zero means unspecified.</summary>
        public int ProcessId { get; private set; }

        // Kept separate from NatVis snapshots: their container strings and reload paths differ.
        public string ContiguousBlueprintType { get; set; }

        /// <summary>Address of the first element. Changes when the container reallocates.</summary>
        public ulong Address { get; private set; }

        /// <summary>
        /// The container's own display string, typically "{ size=N }". Catches a resize before any
        /// memory is read, and costs nothing: the expression has been fetched already.
        /// </summary>
        public string ContainerValue { get; private set; }

        /// <summary>Bytes per element, padding included. The unit the diff works in.</summary>
        public int Stride { get; private set; }

        /// <summary>Elements in the container, which is not the same as drawables produced.</summary>
        public int Count { get; private set; }

        /// <summary>
        /// False when one element does not map to exactly one drawable — a NatVis that expands each
        /// element into several, as DemoRectangle does. Whole-block comparison still works; telling
        /// which drawable to replace does not, because the indices no longer line up.
        /// </summary>
        public bool SupportsPartial { get; private set; }

        public byte[] Bytes { get; private set; }

        /// <summary>
        /// Non-null when the snapshot was captured from separate allocations, such as a linked
        /// list. The byte buffer still stores the nodes in visible order.
        /// </summary>
        public ulong[] ElementAddresses { get; private set; }

        public bool IsSegmented
        {
            get { return ElementAddresses != null; }
        }

        public bool Matches(byte[] other)
        {
            if (other == null || other.Length != Bytes.Length) return false;

            for (int i = 0; i < Bytes.Length; i++)
            {
                if (Bytes[i] != other[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// Indices whose bytes differ, comparing only the elements the two blocks have in common.
        /// A shorter or longer buffer is fine: growth and truncation at the tail are handled by
        /// the caller from the counts.
        ///
        /// Returns null when the caller should just reload everything: no per-element mapping, or
        /// more than <paramref name="limit"/> elements changed, past which reloading them one by
        /// one costs more than a single sweep.
        /// </summary>
        public List<int> FindChangedElements(byte[] other, int otherCount, int limit)
        {
            if (!SupportsPartial) return null;
            if (other == null) return null;
            if (Stride <= 0 || Count <= 0 || otherCount < 0) return null;
            if (other.Length < (long)otherCount * Stride) return null;

            int shared = Count < otherCount ? Count : otherCount;
            var changed = new List<int>();

            for (int index = 0; index < shared; index++)
            {
                int start = index * Stride;
                for (int offset = 0; offset < Stride; offset++)
                {
                    if (Bytes[start + offset] == other[start + offset]) continue;

                    changed.Add(index);
                    if (changed.Count > limit) return null;
                    break;
                }
            }

            return changed;
        }
    }
}
