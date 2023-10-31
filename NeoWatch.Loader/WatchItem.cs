using System.ComponentModel;
using NeoWatch.Drawing;

namespace NeoWatch.Loading
{
    public class WatchItem : INotifyPropertyChanged
    {
        public WatchItem() {
            Drawables = new DrawableCollection();
            isLoading = true;
            isVisible = true;
            color = null; 
        }

        private bool isVisible;
        public bool IsVisible {
            get { return isVisible; }
            set { isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        private bool isLoading;
        public bool IsLoading {
            get { return isLoading; }
            set { 
                isLoading = value;
                if(isLoading) {
                    IsLoadingActivated?.Invoke(this);
                } else {
                    IsLoadingCancelled?.Invoke(this);
                }
            }
        }
        public event WatchItemEventHandler IsLoadingActivated;
        public event WatchItemEventHandler IsLoadingCancelled;

        private string name;
        public string Name {
            get { return name; }
            set { name = value; OnNameChanged(); }
        }

        private string description;
        public string Description {
            get { return description; }
            set { description = value; OnPropertyChanged(nameof(Description)); }
        }
        
        private string color;
        public string Color {
            get { return color; }
            set { color = value; OnPropertyChanged(nameof(Color)); }
        }


        private DrawableCollection drawables;
        public DrawableCollection Drawables {
            get { return drawables; }
            set { drawables = value; OnPropertyChanged(nameof(Drawables)); }
        }

        private IDrawable selectedItem;
        public IDrawable SelectedItem
        {
            get { return selectedItem; }
            set
            {
                if(selectedItem != null)
                {
                    selectedItem.Dash = "1 0";
                }
                selectedItem = value;
                if(value != null && drawables.Count > 1)
                {
                    selectedItem.Dash = "3 2";
                }
                OnPropertyChanged(nameof(SelectedItem));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public event WatchItemEventHandler NameChanged;
        private void OnNameChanged() {
            NameChanged?.Invoke(this);
        }
        public delegate void WatchItemEventHandler(WatchItem sender);
    }
}
