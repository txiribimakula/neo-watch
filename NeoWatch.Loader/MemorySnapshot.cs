using System.Collections.Generic;

namespace NeoWatch.Loading
{
    /// <summary>
    /// What a watch item looked like in memory at the end of its last load, so the next break can
    /// tell whether anything changed without walking the elements again.
    ///
    /// Only the bytes are compared, never interpreted: that is why this works for types whose
    /// NatVis synthesises values that do not exist in memory. What is drawn is a pure function of
    /// this block, so identical bytes mean an identical drawing.
    /// </summary>
    public class MemorySnapshot
    {
        public MemorySnapshot(ulong address, string containerValue, int stride, int count, bool supportsPartial, byte[] bytes)
        {
            Address = address;
            ContainerValue = containerValue;
            Stride = stride;
            Count = count;
            SupportsPartial = supportsPartial;
            Bytes = bytes;
        }

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
