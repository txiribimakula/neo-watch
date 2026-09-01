using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NeoWatch.Drawing;

namespace NeoWatch.Loading
{
    public class WatchItem : INotifyPropertyChanged
    {
        public WatchItem() {
            Drawables = new DrawableCollection();
            PreviousDrawables = new DrawableCollection();
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

        public void ClearDebugSessionState()
        {
            CancelLoad();
            Snapshot = null;
            SetSelectedItemQuietly(null);
            Drawables.Error = null;
            Drawables.ResetAndNotify();
            ClearPreviousDrawables();
            discardPreviousOnNextLoad = true;
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
                OnPropertyChanged(nameof(IsVisibleControlChecked));
                if (isVisible && !wasVisible) {
                    IsVisibleActivated?.Invoke(this);
                }
            }
        }
        public bool IsVisibleControlChecked {
            get { return IsRowConfigured && IsVisible; }
            set { IsVisible = value; }
        }
        public event WatchItemEventHandler IsVisibleActivated;

        private bool isLoading;
        public bool IsLoading {
            get { return isLoading; }
            set {
                isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsLoadingControlChecked));
                if(isLoading) {
                    IsLoadingActivated?.Invoke(this);
                }
            }
        }
        public bool IsLoadingControlChecked {
            get { return IsRowConfigured && IsLoading; }
            set { IsLoading = value; }
        }
        public event WatchItemEventHandler IsLoadingActivated;

        private string name;
        public string Name {
            get { return name; }
            set {
                if (name != value) {
                    ClearPreviousDrawables();
                    Snapshot = null;
                    discardPreviousOnNextLoad = true;
                }
                name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(IsRowConfigured));
                OnPropertyChanged(nameof(IsVisibleControlChecked));
                OnPropertyChanged(nameof(IsLoadingControlChecked));
                OnNameChanged();
            }
        }
        public bool IsRowConfigured { get { return !string.IsNullOrWhiteSpace(name); } }

        private string color;
        public string Color {
            get { return color; }
            set { color = value; OnPropertyChanged(nameof(Color)); }
        }


        private DrawableCollection drawables;
        public DrawableCollection Drawables {
            get { return drawables; }
            set {
                drawables = value;
                OnPropertyChanged(nameof(Drawables));
                OnPropertyChanged(nameof(DisplayedDrawables));
            }
        }

        private DrawableCollection previousDrawables;
        private readonly List<IDrawable> changedCurrentDrawables = new List<IDrawable>();
        private readonly List<IDrawable> changedPreviousDrawables = new List<IDrawable>();

        public DrawableCollection PreviousDrawables {
            get { return previousDrawables; }
            private set {
                previousDrawables = value;
                OnPropertyChanged(nameof(PreviousDrawables));
                OnPropertyChanged(nameof(HasPreviousDrawables));
                OnPropertyChanged(nameof(DisplayedDrawables));
            }
        }

        public bool HasPreviousDrawables {
            get { return PreviousDrawables != null && PreviousDrawables.Count > 0; }
        }

        private bool isShowingPrevious;
        private bool isSwitchingDisplayedDrawables;
        public bool IsShowingPrevious {
            get { return isShowingPrevious; }
        }

        public DrawableCollection DisplayedDrawables {
            get { return IsShowingPrevious && HasPreviousDrawables ? PreviousDrawables : Drawables; }
        }

        private bool discardPreviousOnNextLoad;

        public bool ConsumeDiscardPreviousOnNextLoad()
        {
            bool discard = discardPreviousOnNextLoad;
            discardPreviousOnNextLoad = false;
            return discard;
        }

        public void RememberPreviousDrawables(IList<IDrawable> previous)
        {
            FindChangedDrawables(previous, Drawables);

            var snapshot = new DrawableCollection();
            snapshot.AddAndNotify(new List<IDrawable>(previous));

            isShowingPrevious = false;
            PreviousDrawables = snapshot;
            SynchronizeSelectionCollections();
            OnPropertyChanged(nameof(IsShowingPrevious));
        }

        public void ClearPreviousDrawables()
        {
            changedCurrentDrawables.Clear();
            changedPreviousDrawables.Clear();
            isShowingPrevious = false;
            PreviousDrawables = new DrawableCollection();
            SynchronizeSelectionCollections();
            OnPropertyChanged(nameof(IsShowingPrevious));
        }

        public bool SetShowingPrevious(bool value)
        {
            if (value && !HasPreviousDrawables) return false;
            if (isShowingPrevious == value) return false;

            DrawableCollection source = DisplayedDrawables;
            int selectedIndex = selectedItem == null ? -1 : source.IndexOf(selectedItem);

            isShowingPrevious = value;
            DrawableCollection target = DisplayedDrawables;
            IDrawable targetSelection = selectedIndex >= 0 && selectedIndex < target.Count
                ? target[selectedIndex]
                : (target.Count > 0 ? target[0] : null);

            isSwitchingDisplayedDrawables = true;
            try
            {
                selectedItem = targetSelection;
                SynchronizeSelectionCollections();
                OnPropertyChanged(nameof(IsShowingPrevious));
                OnPropertyChanged(nameof(DisplayedDrawables));
                OnPropertyChanged(nameof(SelectedItem));
            }
            finally
            {
                isSwitchingDisplayedDrawables = false;
            }
            return true;
        }

        public bool IsDrawableChanged(IDrawable drawable)
        {
            return ContainsReference(changedCurrentDrawables, drawable)
                || ContainsReference(changedPreviousDrawables, drawable);
        }

        private void FindChangedDrawables(IList<IDrawable> previous, IList<IDrawable> current)
        {
            changedCurrentDrawables.Clear();
            changedPreviousDrawables.Clear();

            int sharedCount = previous.Count < current.Count ? previous.Count : current.Count;
            for (int index = 0; index < sharedCount; index++)
            {
                if (previous[index].Equals(current[index])) continue;

                changedPreviousDrawables.Add(previous[index]);
                changedCurrentDrawables.Add(current[index]);
            }

            for (int index = sharedCount; index < previous.Count; index++)
            {
                changedPreviousDrawables.Add(previous[index]);
            }
            for (int index = sharedCount; index < current.Count; index++)
            {
                changedCurrentDrawables.Add(current[index]);
            }
        }

        private static bool ContainsReference(IList<IDrawable> drawables, IDrawable candidate)
        {
            for (int index = 0; index < drawables.Count; index++)
            {
                if (ReferenceEquals(drawables[index], candidate)) return true;
            }

            return false;
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
                if (isSwitchingDisplayedDrawables) return;
                if (!SetSelectedItemQuietly(value)) return;
                drawables?.NotifyGeometriesChanged();
                if (previousDrawables != null && !ReferenceEquals(previousDrawables, drawables))
                {
                    previousDrawables.NotifyGeometriesChanged();
                }
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
            SynchronizeSelectionCollections();
            OnPropertyChanged(nameof(SelectedItem));
            return true;
        }

        private void SynchronizeSelectionCollections()
        {
            // The converters read the selection from their collection, not from a binding.
            int selectedIndex = -1;
            if (selectedItem != null && drawables != null)
            {
                selectedIndex = drawables.IndexOf(selectedItem);
            }
            if (selectedIndex < 0 && selectedItem != null && previousDrawables != null)
            {
                selectedIndex = previousDrawables.IndexOf(selectedItem);
            }

            if (drawables != null)
            {
                drawables.SelectedItem = selectedIndex >= 0 && selectedIndex < drawables.Count
                    ? drawables[selectedIndex]
                    : null;
            }
            if (previousDrawables != null)
            {
                previousDrawables.SelectedItem = selectedIndex >= 0 && selectedIndex < previousDrawables.Count
                    ? previousDrawables[selectedIndex]
                    : null;
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
