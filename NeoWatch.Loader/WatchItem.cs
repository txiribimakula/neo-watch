using System.ComponentModel;
using System.Diagnostics;
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
            set {
                loadingCount = value;
                OnPropertyChanged(nameof(LoadingCount));
                // The loader refreshes the count every 100 elements or 100 ms, so hanging the
                // clock off it keeps the elapsed time ticking without any extra plumbing.
                OnPropertyChanged(nameof(LoadingElapsedMs));
            }
        }

        private readonly Stopwatch loadClock = new Stopwatch();

        /// <summary>Milliseconds since this row's load started, or its total once finished.</summary>
        public long LoadingElapsedMs {
            get { return loadClock.ElapsedMilliseconds; }
        }

        public void StartLoadClock() {
            loadClock.Restart();
            OnPropertyChanged(nameof(LoadingElapsedMs));
        }

        public void StopLoadClock() {
            loadClock.Stop();
            OnPropertyChanged(nameof(LoadingElapsedMs));
        }

        private int loadingTotal;
        public int LoadingTotal {
            get { return loadingTotal; }
            set { loadingTotal = value; OnPropertyChanged(nameof(LoadingTotal)); }
        }

        public CancellationTokenSource CurrentLoadCts { get; set; }

        /// <summary>
        /// The bytes this item's elements held after the last successful load, used to skip the
        /// next reload when nothing moved. Null means no baseline, so the next break loads
        /// normally. See <see cref="Loader.IsUnchanged"/>.
        /// </summary>
        public MemorySnapshot Snapshot { get; set; }

        public void CancelLoad()
        {
            if (CurrentLoadCts == null || CurrentLoadCts.IsCancellationRequested) return;
            IsCancelling = true;
            CurrentLoadCts.Cancel();
        }

        private bool isVisible;
        /// <summary>
        /// A hidden item is not loaded at all, so showing one again has to ask for a reload —
        /// its drawables are from whenever it was last visible. Same shape as
        /// <see cref="IsLoadingActivated"/>, but only on the transition, so that the checkbox
        /// re-writing the same value costs nothing.
        /// </summary>
        public bool IsVisible {
            get { return isVisible; }
            set {
                bool wasVisible = isVisible;
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                if (isVisible && !wasVisible) {
                    IsVisibleActivated?.Invoke(this);
                }
            }
        }
        public event WatchItemEventHandler IsVisibleActivated;

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
