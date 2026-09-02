using System;
using System.Collections.Generic;
using System.Threading;

namespace NeoWatch.Drawing.Scene
{
    public sealed class SceneBlock
    {
        private static long nextId;
        private readonly ScenePrimitive[] primitives;
        public long Id { get; } = Interlocked.Increment(ref nextId);
        public int Count => primitives.Length;
        public ScenePrimitive this[int index] => primitives[index];
        public SceneBounds Bounds { get; }
        public SceneBounds FitBounds { get; }
        public bool IsFinite { get; }

        internal SceneBlock(IList<IDrawable> source, int offset, int count)
        {
            primitives = new ScenePrimitive[count];
            var bounds = SceneBounds.Empty;
            var fitBounds = SceneBounds.Empty;
            bool finite = true;
            for (int i = 0; i < count; i++)
            {
                var primitive = ScenePrimitive.Copy(source[offset + i]);
                primitives[i] = primitive;
                bounds = bounds.Union(primitive.Bounds);
                var box = source[offset + i].Box;
                if (box != null) fitBounds = fitBounds.Union(new SceneBounds(box.MinX, box.MinY, box.MaxX, box.MaxY));
                finite &= primitive.IsFinite;
            }
            Bounds = bounds;
            FitBounds = fitBounds;
            IsFinite = finite;
        }

        public ScenePrimitive[] CopyPrimitives() => (ScenePrimitive[])primitives.Clone();
    }

    public sealed class SceneSnapshot
    {
        public const int BlockSize = 2048;
        private readonly SceneBlock[] blocks;
        private readonly BoundsNode root;
        public int Count { get; }
        public int BlockCount => blocks.Length;
        public SceneBlock this[int block] => blocks[block];
        public SceneBounds Bounds => root?.Bounds ?? SceneBounds.Empty;
        public SceneBounds FitBounds => root?.FitBounds ?? SceneBounds.Empty;
        public static SceneSnapshot Empty { get; } = new SceneSnapshot(new SceneBlock[0], 0, null);

        private SceneSnapshot(SceneBlock[] blocks, int count, BoundsNode root)
        { this.blocks = blocks; Count = count; this.root = root; }

        public static SceneSnapshot Capture(IList<IDrawable> source, SceneSnapshot previous = null,
            ISet<int> dirtyBlocks = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            int count = (source.Count + BlockSize - 1) / BlockSize;
            var blocks = new SceneBlock[count];
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int length = Math.Min(BlockSize, source.Count - i * BlockSize);
                blocks[i] = previous != null && i < previous.BlockCount && dirtyBlocks != null
                    && !dirtyBlocks.Contains(i) && previous[i].Count == length
                    ? previous[i] : new SceneBlock(source, i * BlockSize, length);
            }
            return new SceneSnapshot(blocks, source.Count, BoundsNode.Build(blocks, 0, count));
        }

        public void VisitVisible(SceneBounds viewport, Action<int, SceneBlock> visitor)
        { root?.Visit(viewport, visitor); }

        private sealed class BoundsNode
        {
            public SceneBounds Bounds;
            public SceneBounds FitBounds;
            private BoundsNode left, right;
            private SceneBlock block;
            private int index;

            public static BoundsNode Build(SceneBlock[] blocks, int start, int count)
            {
                if (count == 0) return null;
                if (count == 1) return new BoundsNode { block = blocks[start], index = start,
                    Bounds = blocks[start].Bounds, FitBounds = blocks[start].FitBounds };
                int half = count / 2;
                var left = Build(blocks, start, half);
                var right = Build(blocks, start + half, count - half);
                return new BoundsNode { left = left, right = right, Bounds = left.Bounds.Union(right.Bounds),
                    FitBounds = left.FitBounds.Union(right.FitBounds) };
            }

            public void Visit(SceneBounds viewport, Action<int, SceneBlock> visitor)
            {
                if (!Bounds.Intersects(viewport)) return;
                if (block != null) visitor(index, block);
                else { left.Visit(viewport, visitor); right.Visit(viewport, visitor); }
            }
        }
    }
}
