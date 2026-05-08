using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NeoWatch.Drawing
{
    public class DrawableCollection : Collection<IDrawable>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public IBox Box { get; set; }

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

        public void ResetAndNotify() {
            Clear();
            Box = null;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
