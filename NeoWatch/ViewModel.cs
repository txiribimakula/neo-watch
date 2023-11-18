using System.Collections.Generic;
using System.ComponentModel;
using EnvDTE;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System;
using System.Windows.Input;
using NeoWatch.Drawing;
using NeoWatch.Loading;

namespace NeoWatch
{
    public class ViewModel : INotifyPropertyChanged
    {
        public bool IsSenseShown { get; set; } = false;

        public ViewModel(IDebugger debugger, Dictionary<PatternKind, string> patterns, Dictionary<string, PatternKind> typeKindPairs)
        {
            WatchItems = new ObservableCollection<WatchItem>();
            WatchItems.CollectionChanged += OnWatchItemsCollectionChanged;

            Loader = new Loader(debugger, new Interpreter(patterns, typeKindPairs));
        }

        public void OnToolsOptionsBlueprintsChanged(Dictionary<PatternKind, string> patterns, Dictionary<string, PatternKind> typeKindPairs)
        {
            Loader.Interpreter.Patterns = patterns;
            Loader.Interpreter.TypeKindPairs = typeKindPairs;
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
            ToggleSenseCommand = new RelayCommand(_ => ToggleSense());
            PickColorCommand = new RelayCommand(watchItem => PickColor((WatchItem)watchItem));
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
                geoDrawer.TransformGeometries(watchItem.Drawables);
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
                geoDrawer.TransformGeometries(watchItem.Drawables);
            }
            Axes.Item1.Box = new Box(0, (float)args.NewSize.Width, 0, 0);
            Axes.Item2.Box = new Box(0, 0, 0, (float)args.NewSize.Height);
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
        }

        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
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
                geoDrawer.TransformGeometries(watchItem.Drawables);
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
                geoDrawer.TransformGeometries(watchItem.Drawables);
            }
            geoDrawer.TransformGeometries(Axes);
            OnPropertyChanged(nameof(Axes));
            if (isMeasuring)
            {
                geoDrawer.TransformGeometry(Ruler);
                OnPropertyChanged(nameof(Ruler));
            }
        }

        private async void OnWatchItemReloadAsync(WatchItem watchItem)
        {
            watchItem.Drawables.Error = null;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if (watchItem.IsLoading)
            {
                watchItem.Drawables.ResetAndNotify();
                try
                {
                    watchItem.Drawables.Progress = 0;
                    if (watchItem.Color == null)
                    {
                        watchItem.Color = Colours.NextColor().AsHex();
                    }

                    var result = await Loader.Load(watchItem);

                    var feedback = result.Feedback;

                    if (feedback.Description != "OK")
                    {
                        watchItem.Drawables.Error = feedback.Description;
                        return;
                    }

                    var drawables = result.Data;
                    watchItem.Description = drawables.Type;
                    foreach (var drawable in drawables)
                    {
                        geoDrawer.TransformGeometry(drawable);
                    }
                    watchItem.Drawables.AddAndNotify(drawables);
                    watchItem.Drawables.Error = drawables.Error;
                    if (string.IsNullOrEmpty(watchItem.Drawables.Error))
                    {
                        watchItem.Drawables.Progress = watchItem.Drawables.Count;
                    }
                }
                catch (LoadingException ex)
                {
                    watchItem.Drawables.Progress = 0;
                    watchItem.Drawables.Error = ex.Message;
                }
                catch (NullReferenceException ex)
                {
                    watchItem.Drawables.Progress = 0;
                    watchItem.Drawables.Error = "Loader item caused: " + ex.Message;
                }
            }

            stopwatch.Stop();

            watchItem.Drawables.Error = watchItem.Drawables.Error + " (" + stopwatch.Elapsed.TotalMilliseconds.ToString("F0") + "ms)";

            if (watchItem.Drawables.Count > 0)
            {
                watchItem.SelectedItem = watchItem.Drawables[0];
            }
        }
    }
}
