using System.ComponentModel;
using System.Threading;
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

        private bool isBusy;
        public bool IsBusy {
            get { return isBusy; }
            set { isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }

        private bool isCancelling;
        public bool IsCancelling {
            get { return isCancelling; }
            set { isCancelling = value; OnPropertyChanged(nameof(IsCancelling)); }
        }

        private int loadingCount;
        public int LoadingCount {
            get { return loadingCount; }
            set { loadingCount = value; OnPropertyChanged(nameof(LoadingCount)); }
        }

        private int loadingTotal;
        public int LoadingTotal {
            get { return loadingTotal; }
            set { loadingTotal = value; OnPropertyChanged(nameof(LoadingTotal)); }
        }

        public CancellationTokenSource CurrentLoadCts { get; set; }

        public void CancelLoad()
        {
            if (CurrentLoadCts == null || CurrentLoadCts.IsCancellationRequested) return;
            IsCancelling = true;
            CurrentLoadCts.Cancel();
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
                }
            }
        }
        public event WatchItemEventHandler IsLoadingActivated;

        private string name;
        public string Name {
            get { return name; }
            set { name = value; OnNameChanged(); }
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
        /// <summary>
        /// Set by the user picking in the Items dropdown, so it redraws immediately.
        /// The reload path must use <see cref="SetSelectedItemQuietly"/> instead and bump the
        /// geometry once itself, otherwise every reload pays two redraws for one change.
        /// </summary>
        public IDrawable SelectedItem
        {
            get { return selectedItem; }
            set
            {
                if (!SetSelectedItemQuietly(value)) return;
                drawables?.NotifyGeometriesChanged();
            }
        }

        /// <summary>
        /// Applies the selection without asking for a redraw. Returns false when nothing
        /// changed, so callers can skip the work entirely.
        /// </summary>
        public bool SetSelectedItemQuietly(IDrawable value)
        {
            if (ReferenceEquals(selectedItem, value)) return false;

            selectedItem = value;
            // The converters read the selection from the collection, not from a binding.
            if (drawables != null) drawables.SelectedItem = value;
            OnPropertyChanged(nameof(SelectedItem));
            return true;
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
