using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class DrawableCollection : Collection<IDrawable>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public IBox Box { get; set; }

        private int progress = 0;
        public int Progress {
            get { return progress; }
            set { progress = value; NotifyPropertyChanged(nameof(Progress)); }
        }

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

        public void AddAndNotify(IDrawable element) {
            Add(element);
            if(Box == null) {
                Box = element.Box;
            } else {
                Box.Expand(element.Box);
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element));
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

        public void RemoveAndNotify(IDrawable element) {
            Remove(element);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));
        }
    }
}
