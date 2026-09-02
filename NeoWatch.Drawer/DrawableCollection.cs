using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NeoWatch.Drawing.Scene;

namespace NeoWatch.Drawing
{
    public class DrawableCollection : Collection<IDrawable>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private SceneSnapshot scene;
        private readonly HashSet<int> dirtyBlocks = new HashSet<int>();
        private Dictionary<IDrawable, int> indices;
        public int DataVersion { get; private set; }

        public SceneSnapshot CaptureScene()
        {
            if (scene == null || dirtyBlocks.Count > 0 || scene.Count != Count)
            {
                scene = SceneSnapshot.Capture(this, scene, dirtyBlocks);
                dirtyBlocks.Clear();
            }
            return scene;
        }

        public void ShareScene(SceneSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Count != Count) return;
            scene = snapshot;
            dirtyBlocks.Clear();
        }

        public int IndexOfReference(IDrawable item)
        {
            if (item == null) return -1;
            if (indices == null)
            {
                indices = new Dictionary<IDrawable, int>(ReferenceComparer.Instance);
                for (int i = 0; i < Count; i++) if (!indices.ContainsKey(this[i])) indices.Add(this[i], i);
            }
            return indices.TryGetValue(item, out int index) ? index : -1;
        }

        private void MarkChanged(int index, bool suffix)
        {
            int last = suffix ? Count / SceneSnapshot.BlockSize : index / SceneSnapshot.BlockSize;
            for (int block = index / SceneSnapshot.BlockSize; block <= last; block++) dirtyBlocks.Add(block);
            indices = null;
            DataVersion++;
        }

        protected override void InsertItem(int index, IDrawable item)
        { base.InsertItem(index, item); MarkChanged(index, index < Count - 1); }
        protected override void SetItem(int index, IDrawable item)
        { base.SetItem(index, item); MarkChanged(index, false); }
        protected override void RemoveItem(int index)
        { base.RemoveItem(index); MarkChanged(index, true); }
        protected override void ClearItems()
        { base.ClearItems(); scene = null; dirtyBlocks.Clear(); indices = null; DataVersion++; }

        private sealed class ReferenceComparer : IEqualityComparer<IDrawable>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(IDrawable x, IDrawable y) => ReferenceEquals(x, y);
            public int GetHashCode(IDrawable item) => RuntimeHelpers.GetHashCode(item);
        }

        public IBox Box { get; set; }

        /// <summary>
        /// The drawable currently highlighted, kept here rather than only on the WatchItem so
        /// that the geometry converters can read it without a binding of their own. That leaves
        /// GeometryVersion as the single source a redraw depends on: one bump, one pass per
        /// layer, instead of a pass per source that changes during a reload.
        /// Owned by WatchItem.SelectedItem — assign it through there, not directly.
        /// </summary>
        public IDrawable SelectedItem { get; set; }

        private string error;
        public string Error {
            get { return error; }
            set { error = value; NotifyPropertyChanged(nameof(Error)); }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private int geometryVersion;
        public int GeometryVersion {
            get { return geometryVersion; }
        }

        public void NotifyGeometriesChanged() {
            geometryVersion++;
            NotifyPropertyChanged(nameof(GeometryVersion));
        }

        public void AddAndNotify(List<IDrawable> elements) {
            foreach (var element in elements) {
                Add(element);
                if (Box == null) {
                    Box = (IBox)element.Box?.Clone();
                } else {
                    Box.Expand(element.Box);
                }
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Swaps a few drawables for new ones at the same indices, and grows or shrinks the tail,
        /// for the case where only part of the container moved.
        ///
        /// The Box is rebuilt from scratch rather than expanded: an element that moved inward, or
        /// one that was dropped, would otherwise leave the bounds too large forever.
        /// </summary>
        public void ApplyPartialAndNotify(IList<DrawableReplacement> replacements, IList<IDrawable> appended, int finalCount) {
            foreach (var replacement in replacements) {
                if (replacement.Index < 0 || replacement.Index >= Count) continue;
                this[replacement.Index] = replacement.Drawable;
            }

            while (Count > finalCount) {
                RemoveAt(Count - 1);
            }

            foreach (var element in appended) {
                Add(element);
            }

            Box = null;
            if (scene != null)
            {
                var bounds = CaptureScene().FitBounds;
                if (!bounds.IsEmpty) Box = new Box((float)bounds.MinX, (float)bounds.MaxX, (float)bounds.MinY, (float)bounds.MaxY);
            }
            else foreach (var element in this) {
                if (element.Box == null) continue;
                if (Box == null) {
                    Box = (IBox)element.Box.Clone();
                } else {
                    Box.Expand(element.Box);
                }
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void ResetAndNotify() {
            Clear();
            Box = null;
            SelectedItem = null;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
