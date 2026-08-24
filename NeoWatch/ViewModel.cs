using System.Collections.Generic;
using System.ComponentModel;
using EnvDTE;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using NeoWatch.Common;

namespace NeoWatch
{
    public class ViewModel : INotifyPropertyChanged
    {
        public bool IsSenseShown { get; set; } = false;

        private bool canUserAddRows = true;
        public bool CanUserAddRows
        {
            get { return canUserAddRows; }
            set { canUserAddRows = value; OnPropertyChanged(nameof(CanUserAddRows)); }
        }

        public ViewModel(IDebugger debugger, Dictionary<PatternKind, string[]> patterns, Dictionary<string, PatternKind> typeKindPairs, IMemoryReader memoryReader = null)
        {
            WatchItems = new ObservableCollection<WatchItem>();
            WatchItems.CollectionChanged += OnWatchItemsCollectionChanged;

            Loader = new Loader(debugger, new Interpreter(patterns, typeKindPairs), memoryReader);
            Loader.YieldAction = BackgroundYield;

            CancelLoadCommand = new RelayCommand(watchItem => ((WatchItem)watchItem).CancelLoad());
            PickColorCommand = new RelayCommand(watchItem => PickColor((WatchItem)watchItem));
            ToggleSenseCommand = new RelayCommand(_ => ToggleSense());
        }

        private static async Task BackgroundYield()
        {
            // Background priority so pending Input events (e.g. cancel button) drain before the loader resumes.
            await Dispatcher.Yield(DispatcherPriority.Background);
        }

        private static Task WaitForRenderFrame()
        {
            var tcs = new TaskCompletionSource<bool>();
            EventHandler handler = null;
            handler = (s, e) =>
            {
                CompositionTarget.Rendering -= handler;
                tcs.SetResult(true);
            };
            CompositionTarget.Rendering += handler;
            return tcs.Task;
        }

        public void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
        {
            foreach (var watchItem in WatchItems)
            {
                OnWatchItemReloadAsync(watchItem);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private IDrawable selectedItem;
        public IDrawable SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
            }
        }

        private Loader Loader;
        private readonly SemaphoreSlim loadSemaphore = new SemaphoreSlim(1, 1);
        private GeometryDrawer geoDrawer;
        public ObservableCollection<WatchItem> WatchItems { get; set; }

        public (Axis, Axis) Axes { get; set; }

        public DrawableLineSegment Ruler { get; set; }
        private bool isMeasuring;

        private Geometries.Point currentCursorPoint;
        public Geometries.Point CurrentCursorPoint
        {
            get { return currentCursorPoint; }
            set { currentCursorPoint = value; OnPropertyChanged("CurrentCursorPoint"); }
        }

        private bool isMiddleMouseDown;
        private Geometries.Point lastCanvasClickedPoint;

        public RelayCommand AutoFitCommand { get; set; }
        public RelayCommand ToggleSenseCommand { get; set; }
        public RelayCommand PickColorCommand { get; set; }
        public RelayCommand CancelLoadCommand { get; set; }

        private int loadingCount;
        public bool IsAnyLoading
        {
            get { return loadingCount > 0; }
        }

        private void IncrementLoading()
        {
            loadingCount++;
            if (loadingCount == 1) OnPropertyChanged(nameof(IsAnyLoading));
        }

        private void DecrementLoading()
        {
            loadingCount--;
            if (loadingCount == 0) OnPropertyChanged(nameof(IsAnyLoading));
        }


        public void OnLoaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement frameworkElement = (FrameworkElement)sender;

            ICoordinateSystem coordinateSystem = new CoordinateSystem((float)frameworkElement.ActualWidth, (float)frameworkElement.ActualHeight, new Box(-10, 10, -10, 10));

            Axes = (new Axis(new Box(0, (float)frameworkElement.ActualWidth, 0, 0)), new Axis(new Box(0, 0, 0, (float)frameworkElement.ActualHeight)));

            Ruler = new DrawableLineSegment(new Geometries.Point(0, 0), new Geometries.Point(0, 0));

            DrawableVisitor visitor = new DrawableVisitor(coordinateSystem);
            geoDrawer = new GeometryDrawer(visitor);

            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));

            AutoFitCommand = new RelayCommand(parameter => AutoFit((float)frameworkElement.ActualWidth / (float)frameworkElement.ActualHeight));
        }

        private void ToggleSense()
        {
            IsSenseShown = !IsSenseShown;
            OnPropertyChanged(nameof(IsSenseShown));
        }

        private void PickColor(WatchItem watchItem)
        {
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                watchItem.Color = "#" + (colorDialog.Color.ToArgb() & 0x00FFFFFF).ToString("X6");
            }
        }

        private void AutoFit(float windowRatio)
        {
            IBox box = GetBox(WatchItems);

            if (box != null)
            {
                SetNewCoordinateSystem(windowRatio, box);
                TransformCanvasGeometries();
            }
        }

        private void TransformCanvasGeometries()
        {
            foreach (var watchItem in WatchItems)
            {
                if (!watchItem.IsVisible) continue;
                geoDrawer.TransformGeometries(watchItem.Drawables);
                watchItem.Drawables.NotifyGeometriesChanged();
            }
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
        }

        private void SetNewCoordinateSystem(float windowRatio, IBox box)
        {
            LockMaximumZoomIn(box);

            AdaptToWindowRatio(windowRatio, box);

            ICoordinateSystem coordinateSystem = new CoordinateSystem(geoDrawer.DrawableVisitor.CoordinateSystem.WorldWidth, geoDrawer.DrawableVisitor.CoordinateSystem.WorldHeight, box);
            geoDrawer.DrawableVisitor.CoordinateSystem = coordinateSystem;
        }

        private IBox GetBox(ObservableCollection<WatchItem> watchItems)
        {
            IBox box = null;

            foreach (var watchItem in watchItems)
            {
                if (watchItem.Drawables.Box != null)
                {
                    if (box == null && watchItem.IsVisible)
                    {
                        box = (IBox)watchItem.Drawables.Box.Clone();
                    }
                    else if (watchItem.IsVisible)
                    {
                        box.Expand(watchItem.Drawables.Box);
                    }
                }
            }

            return box;
        }

        private static void AdaptToWindowRatio(float windowRatio, IBox box)
        {
            float drawablesRatio = box.HorizontalLength / box.VerticalLength;
            if (drawablesRatio > windowRatio)
            {
                float verticalIncrement = (box.VerticalLength * (drawablesRatio / windowRatio)) - box.VerticalLength;
                box.MaxY += verticalIncrement / 2;
                box.MinY -= verticalIncrement / 2;
            }
        }

        private static void LockMaximumZoomIn(IBox box)
        {
            if (box.VerticalLength < 1)
            {
                float verticalIncrement = (float)((0.9 - box.VerticalLength) / 2);
                box.MaxY += verticalIncrement;
                box.MinY -= verticalIncrement;
            }
        }

        public void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            geoDrawer.DrawableVisitor.CoordinateSystem.ReCalculate((float)args.NewSize.Width, (float)args.NewSize.Height);
            foreach (var watchItem in WatchItems)
            {
                if (!watchItem.IsVisible) continue;
                geoDrawer.TransformGeometries(watchItem.Drawables);
                watchItem.Drawables.NotifyGeometriesChanged();
            }
            Axes.Item1.Box = new Box(0, (float)args.NewSize.Width, 0, 0);
            Axes.Item2.Box = new Box(0, 0, 0, (float)args.NewSize.Height);
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
        }

        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsAnyLoading) return;
            IInputElement senderElement = (IInputElement)sender;
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                isMiddleMouseDown = true;
                System.Windows.Point point = e.GetPosition(senderElement);
                lastCanvasClickedPoint = new Geometries.Point((float)point.X, (float)point.Y);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (isMeasuring)
                {
                    Ruler = null;
                    OnPropertyChanged(nameof(Ruler));
                    isMeasuring = false;
                }
                else
                {
                    Ruler = new DrawableLineSegment(currentCursorPoint, currentCursorPoint);
                    geoDrawer.TransformGeometry(Ruler);
                    OnPropertyChanged(nameof(Ruler));
                    isMeasuring = true;
                }
            }
            senderElement.CaptureMouse();
        }

        public void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            IInputElement senderElement = (IInputElement)sender;
            isMiddleMouseDown = false;
            senderElement.ReleaseMouseCapture();
        }

        public void OnMouseLeave(object sender, MouseEventArgs e)
        {
            CurrentCursorPoint = null;
        }

        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            Geometries.Point currentCanvasCursorPoint = GetCurrentCanvasCursorPoint(sender, e);
            CurrentCursorPoint = geoDrawer.DrawableVisitor.CoordinateSystem.ConvertPointToLocal(currentCanvasCursorPoint);

            if (isMiddleMouseDown)
            {
                PanCanvas(currentCanvasCursorPoint);
            }
            if (isMeasuring)
            {
                SetMeasurement();
            }
        }

        private static Geometries.Point GetCurrentCanvasCursorPoint(object sender, MouseEventArgs e)
        {
            IInputElement senderElement = (IInputElement)sender;
            System.Windows.Point canvasClickPoint = e.GetPosition(senderElement);
            var currentCanvasCursorPoint = new Geometries.Point((float)canvasClickPoint.X, (float)canvasClickPoint.Y);
            return currentCanvasCursorPoint;
        }

        private void PanCanvas(Geometries.Point currentCanvasCursorPoint)
        {
            float incrementalX = currentCanvasCursorPoint.X - lastCanvasClickedPoint.X;
            float incrementalY = currentCanvasCursorPoint.Y - lastCanvasClickedPoint.Y;
            geoDrawer.DrawableVisitor.CoordinateSystem.Offset = new Geometries.Point(incrementalX, incrementalY);
            foreach (var watchItem in WatchItems)
            {
                if (!watchItem.IsVisible) continue;
                geoDrawer.TransformGeometries(watchItem.Drawables);
                watchItem.Drawables.NotifyGeometriesChanged();
            }
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
            lastCanvasClickedPoint = currentCanvasCursorPoint;
        }

        private void SetMeasurement()
        {
            Ruler.FinalPoint = CurrentCursorPoint;
            geoDrawer.TransformGeometry(Ruler);
            OnPropertyChanged(nameof(Ruler));
        }

        private void OnWatchItemsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (WatchItem item in e.NewItems)
                {
                    if (item != null)
                    {
                        item.NameChanged += OnWatchItemReloadAsync;
                        item.IsLoadingActivated += OnWatchItemReloadAsync;
                        item.IsVisibleActivated += OnWatchItemReloadAsync;
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (WatchItem item in e.OldItems)
                {
                    if (item != null)
                    {
                        item.NameChanged -= OnWatchItemReloadAsync;
                        item.IsLoadingActivated -= OnWatchItemReloadAsync;
                        item.IsVisibleActivated -= OnWatchItemReloadAsync;
                        item.CancelLoad();
                    }
                }
            }
        }

        public void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            IInputElement senderElement = (IInputElement)sender;
            System.Windows.Point cursorWorldPoint = e.GetPosition(senderElement);
            var localPoint = new Geometries.Point(geoDrawer.DrawableVisitor.CoordinateSystem.ConvertXToLocal((float)cursorWorldPoint.X), geoDrawer.DrawableVisitor.CoordinateSystem.ConvertYToLocal((float)cursorWorldPoint.Y));
            if (e.Delta < 0)
            {
                geoDrawer.DrawableVisitor.CoordinateSystem.Scale *= 1.1f;
            }
            else
            {
                if (geoDrawer.DrawableVisitor.CoordinateSystem.LocalMaxY - geoDrawer.DrawableVisitor.CoordinateSystem.LocalMinY < 1)
                {
                    return;
                }

                geoDrawer.DrawableVisitor.CoordinateSystem.Scale /= 1.1f;
            }
            float newWorldPointX = geoDrawer.DrawableVisitor.CoordinateSystem.ConvertXToWorld(localPoint.X);
            float newWorldPointY = geoDrawer.DrawableVisitor.CoordinateSystem.ConvertYToWorld(localPoint.Y);

            geoDrawer.DrawableVisitor.CoordinateSystem.Offset = new Geometries.Point((float)cursorWorldPoint.X - newWorldPointX, (float)cursorWorldPoint.Y - newWorldPointY);

            foreach (var watchItem in WatchItems)
            {
                if (!watchItem.IsVisible) continue;
                geoDrawer.TransformGeometries(watchItem.Drawables);
                watchItem.Drawables.NotifyGeometriesChanged();
            }
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
            if (isMeasuring)
            {
                geoDrawer.TransformGeometry(Ruler);
                OnPropertyChanged(nameof(Ruler));
            }
        }

        /// <summary>
        /// Reloads only the elements whose bytes changed. No progress bar and no cancellation: it
        /// is a handful of expressions, not a sweep. Returns false on anything unexpected, and
        /// then the caller falls through to the full reload.
        /// </summary>
        private bool TryReloadChangedElements(WatchItem watchItem, ReloadPlan plan)
        {
            List<DrawableReplacement> replacements = Loader.ReloadElements(watchItem, plan.ChangedIndices);
            if (replacements == null) return false;

            List<DrawableReplacement> additions = Loader.ReloadElements(watchItem, plan.AddedIndices);
            if (additions == null) return false;

            var appended = new List<IDrawable>(additions.Count);
            foreach (var addition in additions)
            {
                appended.Add(addition.Drawable);
            }

            foreach (var replacement in replacements)
            {
                geoDrawer.TransformGeometry(replacement.Drawable);
            }
            foreach (var element in appended)
            {
                geoDrawer.TransformGeometry(element);
            }

            // Captured before the swap: if the selected drawable is one of the replaced ones, the
            // selection has to follow the index, because the object it pointed at is gone.
            int selectedIndex = watchItem.SelectedItem == null
                ? -1
                : watchItem.Drawables.IndexOf(watchItem.SelectedItem);

            watchItem.Drawables.ApplyPartialAndNotify(replacements, appended, plan.FinalCount);

            if (selectedIndex >= 0 && selectedIndex < watchItem.Drawables.Count)
            {
                watchItem.SetSelectedItemQuietly(watchItem.Drawables[selectedIndex]);
            }
            else if (selectedIndex >= 0)
            {
                // The selection was in the part that got dropped.
                watchItem.SetSelectedItemQuietly(watchItem.Drawables.Count > 0 ? watchItem.Drawables[0] : null);
            }

            watchItem.LoadingTotal = watchItem.Drawables.Count;
            watchItem.LoadingCount = watchItem.Drawables.Count;

            // Only now that it landed does the freshly read block become the new baseline.
            Loader.CommitSnapshot(watchItem, plan);

            watchItem.Drawables.NotifyGeometriesChanged();
            return true;
        }

        private async void OnWatchItemReloadAsync(WatchItem watchItem)
        {
            watchItem.Drawables.Error = null;

            // A hidden item is not drawn, so there is nothing to pay COM for. Showing it again
            // raises IsVisibleActivated, which comes back through here.
            if (watchItem.IsLoading && watchItem.IsVisible)
            {
                // C0/C0b: let memory decide what this break costs before touching anything.
                var plan = Loader.PlanReload(watchItem);
                // Byte for byte identical: no load, no redraw, no UI churn at all.
                if (plan.IsUnchanged) return;
                // A handful of elements moved: reload just those and skip the sweep.
                if (plan.IsPartial && TryReloadChangedElements(watchItem, plan)) return;

                // Started here, not above: a skipped reload must not reset the time on display.
                watchItem.StartLoadClock();
                watchItem.CancelLoad();
                var cts = new CancellationTokenSource();
                watchItem.CurrentLoadCts = cts;
                watchItem.IsBusy = true;
                watchItem.LoadingCount = 0;
                watchItem.LoadingTotal = 0;
                IncrementLoading();

                await BackgroundYield();

                // Clear the selection before the reset so that the ComboBox writing null back
                // through its two-way binding finds nothing to change, and asks for no redraw.
                watchItem.SetSelectedItemQuietly(null);
                watchItem.Drawables.ResetAndNotify();
                try
                {
                    if (watchItem.Color == null)
                    {
                        watchItem.Color = Colours.NextColor().AsHex();
                    }

                    Result<Drawables> result;
                    await loadSemaphore.WaitAsync(cts.Token);
                    try
                    {
                        // Wait for an actual WPF render frame so the row's cancel button is materialised
                        // and clickable before the synchronous loader work starts to monopolise the UI thread.
                        await WaitForRenderFrame();
                        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                        result = await Loader.Load(watchItem, cts.Token);
                    }
                    finally
                    {
                        loadSemaphore.Release();
                    }

                    var feedback = result.Feedback;

                    if (feedback.HasError)
                    {
                        watchItem.Drawables.Error = feedback.Detail;
                    }

                    if (result.Data != null && result.Data.Count > 0)
                    {
                        var drawables = result.Data;
                        foreach (var drawable in drawables)
                        {
                            geoDrawer.TransformGeometry(drawable);
                        }
                        watchItem.Drawables.AddAndNotify(drawables);
                        // Selection first, redraw once: both feed the same converters, so
                        // applying the selection quietly keeps this to a single pass per layer.
                        watchItem.SetSelectedItemQuietly(watchItem.Drawables[0]);
                        watchItem.Drawables.NotifyGeometriesChanged();
                        if (drawables.Error != null)
                        {
                            watchItem.Drawables.Error = watchItem.Drawables.Error + " | " + drawables.Error;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    watchItem.Drawables.Error = new Feedback(FeedbackType.Cancelled).Detail;
                }
                catch (NullReferenceException ex)
                {
                    watchItem.Drawables.Error = "Loader item caused: " + ex.Message;
                }
                finally
                {
                    // Stopped in the finally so the total covers the drawables actually reaching
                    // the canvas, and still lands on cancel or error.
                    watchItem.StopLoadClock();
                    watchItem.IsBusy = false;
                    watchItem.IsCancelling = false;
                    DecrementLoading();
                    if (ReferenceEquals(watchItem.CurrentLoadCts, cts))
                    {
                        watchItem.CurrentLoadCts = null;
                    }
                    cts.Dispose();
                }
            }
        }
    }
}
