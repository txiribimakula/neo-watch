using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NeoWatch.Drawing
{
    public class DrawableCollection : Collection<IDrawable>, INotifyCollectionChanged, INotifyPropertyChanged
    {
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
                    Box = element.Box;
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
            foreach (var element in this) {
                if (element.Box == null) continue;
                if (Box == null) {
                    Box = element.Box;
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
