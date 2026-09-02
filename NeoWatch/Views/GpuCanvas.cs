using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using NeoWatch.Rendering;

namespace NeoWatch
{
    public sealed class GpuCanvas : FrameworkElement
    {
        private readonly D3DImage image = new D3DImage();
        private readonly List<WatchItem> watchedRows = new List<WatchItem>();
        private readonly List<DrawableCollection> watchedCollections = new List<DrawableCollection>();
        private NativeRenderer renderer;
        private ViewModel model;
        private ICollectionView rowsView;
        private bool requested;
        private DispatcherOperation pendingFrame;

        public GpuCanvas()
        {
            IsHitTestVisible = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += (s, e) => { Detach(); if (IsLoaded) Attach(); };
            SizeChanged += (s, e) => RequestFrame();
            IsVisibleChanged += (s, e) => { if (IsVisible) RequestFrame(); };
            image.IsFrontBufferAvailableChanged += (s, e) =>
            {
                if (!image.IsFrontBufferAvailable && renderer != null)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (renderer != null && !image.IsFrontBufferAvailable)
                            Fail(new InvalidOperationException("The WPF front buffer is unavailable."));
                    }));
                else RequestFrame();
            };
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (renderer != null && ActualWidth > 0 && ActualHeight > 0)
                drawingContext.DrawImage(image, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => Attach();
        private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();
        private void Attach()
        {
            if (model != null) return;
            model = DataContext as ViewModel;
            if (model == null) return;
            model.PropertyChanged += ModelChanged;
            rowsView = CollectionViewSource.GetDefaultView(model.WatchItems);
            rowsView.CollectionChanged += RowsChanged;
            RebindRows(); RequestFrame();
        }

        private void Detach()
        {
            if (model != null)
            {
                model.PropertyChanged -= ModelChanged;
                rowsView.CollectionChanged -= RowsChanged;
            }
            UnbindRows();
            model = null;
            rowsView = null;
            Release();
        }

        private void ModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsGpuCanvasActive))
            {
                RebindRows();
                if (!model.IsGpuCanvasActive) Release();
                else RequestFrame();
            }
            else if (e.PropertyName == nameof(ViewModel.CanvasCamera) || e.PropertyName == nameof(ViewModel.IsSenseShown))
                RequestFrame();
        }

        private void RowsChanged(object sender, NotifyCollectionChangedEventArgs e)
        { RebindRows(); RequestFrame(); }

        private void UnbindRows()
        {
            foreach (var row in watchedRows) row.PropertyChanged -= RowChanged;
            foreach (var collection in watchedCollections)
            {
                collection.PropertyChanged -= GeometryChanged;
                collection.CollectionChanged -= GeometryCollectionChanged;
            }
            watchedRows.Clear(); watchedCollections.Clear();
        }

        private void RebindRows()
        {
            UnbindRows();
            if (model?.IsGpuCanvasActive != true) return;
            foreach (var row in model.WatchItems)
            {
                watchedRows.Add(row); row.PropertyChanged += RowChanged;
                foreach (var collection in new[] { row.Drawables, row.PreviousDrawables })
                {
                    if (collection == null) continue;
                    watchedCollections.Add(collection);
                    collection.PropertyChanged += GeometryChanged;
                    collection.CollectionChanged += GeometryCollectionChanged;
                }
            }
        }

        private void RowChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(WatchItem.Drawables):
                case nameof(WatchItem.PreviousDrawables): RebindRows(); RequestFrame(); break;
                case nameof(WatchItem.IsVisible):
                case nameof(WatchItem.IsShowingPrevious):
                case nameof(WatchItem.Color):
                case nameof(WatchItem.SelectedItem): RequestFrame(); break;
            }
        }

        private void GeometryChanged(object sender, PropertyChangedEventArgs e)
        { if (e.PropertyName == nameof(DrawableCollection.GeometryVersion)) RequestFrame(); }
        private void GeometryCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => RequestFrame();

        private void RequestFrame()
        {
            if (requested || !IsLoaded || model?.IsGpuCanvasActive != true) return;
            requested = true;
            pendingFrame = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderFrame));
        }

        private void RenderFrame()
        {
            pendingFrame = null;
            requested = false;
            if (model?.IsGpuCanvasActive != true || !IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return;
            if (model.CanvasCamera.Width <= 0 || model.CanvasCamera.Height <= 0) return;
            try
            {
                if (!model.CanvasCamera.IsValid) throw new NotSupportedException("Invalid canvas camera.");
                if ((RenderCapability.Tier >> 16) == 0 || RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly)
                    throw new NotSupportedException("WPF is using software rendering.");
                if (renderer == null) renderer = new NativeRenderer();
                var dpi = VisualTreeHelper.GetDpi(this);
                // The camera uses uniform DIPs. A non-square scale needs the WPF reference backend.
                if (Math.Abs(dpi.DpiScaleX - dpi.DpiScaleY) > .0001) throw new NotSupportedException("Non-uniform canvas DPI.");
                int width = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
                int height = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));
                image.Lock();
                try
                {
                    if (renderer.Width != width || renderer.Height != height)
                    {
                        image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                        IntPtr surface = renderer.Resize(width, height);
                        image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface);
                    }
                    var rows = new List<RenderRow>();
                    var prepare = Stopwatch.StartNew();
                    foreach (object value in rowsView)
                    {
                        if (!(value is WatchItem row)) continue;
                        if (!row.IsVisible) continue;
                        var color = row.Color == null ? System.Windows.Media.Colors.Transparent :
                            (System.Windows.Media.Color)ColorConverter.ConvertFromString(row.Color);
                        rows.Add(new RenderRow
                        {
                            Current = row.Drawables.CaptureScene(), Previous = row.PreviousDrawables.CaptureScene(),
                            SelectedIndex = row.Drawables.IndexOfReference(row.Drawables.SelectedItem),
                            PreviousSelectedIndex = row.PreviousDrawables.IndexOfReference(row.PreviousDrawables.SelectedItem),
                            Color = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B),
                            ShowPrevious = row.IsShowingPrevious, ShowSense = model.IsSenseShown
                        });
                    }
                    double prepareMs = prepare.Elapsed.TotalMilliseconds;
                    renderer.Render(rows, model.CanvasCamera, (float)dpi.DpiScaleX);
                    image.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    model.RecordCanvasFrame(prepareMs, renderer.LastFrame);
                }
                finally { image.Unlock(); }
                InvalidateVisual();
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException)) { Fail(exception); }
        }

        private void Fail(Exception exception)
        {
            Release();
            model?.UseCanvasFallback(exception.Message);
        }

        private void Release()
        {
            if (pendingFrame?.Status == DispatcherOperationStatus.Pending) pendingFrame.Abort();
            pendingFrame = null;
            requested = false;
            if (renderer != null)
            {
                image.Lock();
                try { image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero); }
                finally { image.Unlock(); }
                renderer.Dispose(); renderer = null;
                InvalidateVisual();
            }
        }
    }
}
