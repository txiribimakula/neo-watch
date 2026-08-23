using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NeoWatch.Converters;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using GeoPoint = NeoWatch.Geometries.Point;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Measures the block that runs AFTER the loader's progress bar reaches 100 % —
    /// ViewModel.cs:451-465 plus the WPF work it triggers. All of it is synchronous on the
    /// UI thread with no yields, so whatever it costs is a dead freeze of Visual Studio.
    ///
    /// Unlike the pass-counting benchmark, the Path elements here live in a real visual tree
    /// and are actually rasterised, which is where the stroke tessellation cost shows up.
    /// </summary>
    internal static class Pipeline
    {
        private const double CanvasWidth = 800;
        private const double CanvasHeight = 600;

        public static void Run(int[] sizes)
        {
            Console.WriteLine();
            Console.WriteLine("Fase posterior a la carga - vector de puntos, todo en el hilo de UI sin yields");
            Console.WriteLine();
            Console.WriteLine("  +---------+----------+----------+------------+------------+------------+----------+");
            Console.WriteLine("  |  Puntos | Transform| Add+Box  | Converters | ComboBox   | Rasterizar |    TOTAL |");
            Console.WriteLine("  +---------+----------+----------+------------+------------+------------+----------+");

            foreach (int size in sizes)
            {
                Phase phase = Measure(size);
                Console.WriteLine("  | " + size.ToString("N0", CultureInfo.InvariantCulture).PadLeft(7)
                                  + " | " + Ms(phase.Transform).PadLeft(8)
                                  + " | " + Ms(phase.Add).PadLeft(8)
                                  + " | " + Ms(phase.Converters).PadLeft(10)
                                  + " | " + Ms(phase.ComboBox).PadLeft(10)
                                  + " | " + Ms(phase.Raster).PadLeft(10)
                                  + " | " + Ms(phase.Total).PadLeft(8) + " |");
            }

            Console.WriteLine("  +---------+----------+----------+------------+------------+------------+----------+");
            Console.WriteLine();
            Console.WriteLine("  Transform   ViewModel.cs:454 - geoDrawer.TransformGeometry por cada drawable");
            Console.WriteLine("  Add+Box     ViewModel.cs:458 - AddAndNotify: Add + Box.Expand + CollectionChanged");
            Console.WriteLine("  Converters  las 15 pasadas de reconstruccion de StreamGeometry");
            Console.WriteLine("  ComboBox    regeneracion del CollectionView de la columna Items");
            Console.WriteLine("  Rasterizar  teselado del trazo y rasterizado real de WPF");
            Console.WriteLine();
        }

        private static string Ms(double milliseconds)
        {
            return milliseconds >= 1000
                ? (milliseconds / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " s"
                : milliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms";
        }

        private struct Phase
        {
            public double Transform, Add, Converters, ComboBox, Raster, Total;
        }

        private static Phase Measure(int count)
        {
            var visitor = new DrawableVisitor(new CoordinateSystem((float)CanvasWidth, (float)CanvasHeight, new Box(-10f, 10f, -10f, 10f)));
            var drawer = new GeometryDrawer(visitor);
            var item = new WatchItem { Name = "f10Points" };

            Canvas canvas = BuildCanvas(item);
            ComboBox combo = BuildComboBox(item);
            var root = new StackPanel();
            root.Children.Add(canvas);
            root.Children.Add(combo);
            Layout(root);

            // The loader hands over a fresh, untransformed batch on every reload.
            List<IDrawable> fresh = BuildPoints(count);

            var phase = new Phase();
            var total = Stopwatch.StartNew();

            var sw = Stopwatch.StartNew();
            foreach (IDrawable drawable in fresh)
            {
                drawer.TransformGeometry(drawable);          // ViewModel.cs:454
            }
            phase.Transform = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            item.Drawables.AddAndNotify(fresh);              // ViewModel.cs:458
            phase.Add = sw.Elapsed.TotalMilliseconds;
            Pump();
            phase.ComboBox = 0; // measured separately below; AddAndNotify already fired the Reset

            sw.Restart();
            item.Drawables.NotifyGeometriesChanged();        // ViewModel.cs:459
            item.SelectedItem = item.Drawables[0];           // ViewModel.cs:490
            Pump();
            phase.Converters = sw.Elapsed.TotalMilliseconds;

            // Isolate the ComboBox CollectionView regeneration.
            sw.Restart();
            combo.ItemsSource = null;
            combo.ItemsSource = item.Drawables;
            Pump();
            phase.ComboBox = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            Rasterize(root);
            phase.Raster = sw.Elapsed.TotalMilliseconds;

            phase.Total = total.Elapsed.TotalMilliseconds;
            return phase;
        }

        private static List<IDrawable> BuildPoints(int count)
        {
            var list = new List<IDrawable>(count);
            var rnd = new Random(1234);

            for (int i = 0; i < count; i++)
            {
                double t = i * (6.0 * Math.PI / count);
                double r = 0.05 * t;
                double angle = t + (i % 2) * Math.PI;
                list.Add(new DrawablePoint(
                    (float)(10.0 + r * Math.Cos(angle)),
                    (float)(10.0 + r * Math.Sin(angle))));
            }

            GC.KeepAlive(rnd);
            return list;
        }

        /// <summary>The five Path elements of NeoWatchWindow.xaml:94-139, with their real stroke setup.</summary>
        private static Canvas BuildCanvas(WatchItem item)
        {
            var canvas = new Canvas
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                ClipToBounds = true,
                DataContext = item
            };

            Brush stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0x6F, 0xA5));
            stroke.Freeze();

            AddStrokedPath(canvas, item, stroke, DrawablesGeometryMode.Main,     1, null);
            AddStrokedPath(canvas, item, stroke, DrawablesGeometryMode.Selected, 1, new DoubleCollection { 3, 2 });
            AddStrokedPath(canvas, item, stroke, DrawablesGeometryMode.Caps,     7, null);

            // Points are filled, not stroked - see AppendPointDot.
            AddFilledPath(canvas, item, stroke, DrawablesGeometryMode.Points,        4);
            AddFilledPath(canvas, item, stroke, DrawablesGeometryMode.SelectedPoint, 8);

            return canvas;
        }

        private static void AddStrokedPath(Canvas canvas, WatchItem item, Brush stroke,
                                           DrawablesGeometryMode mode, double thickness, DoubleCollection dash)
        {
            var path = new Path
            {
                Opacity = 0.8,
                Stroke = stroke,
                StrokeThickness = thickness,
                DataContext = item
            };
            if (dash != null)
            {
                path.StrokeDashArray = dash;
            }
            Bind(canvas, item, path, mode, 0);
        }

        private static void AddFilledPath(Canvas canvas, WatchItem item, Brush fill,
                                          DrawablesGeometryMode mode, double dotSize)
        {
            var path = new Path { Opacity = 0.8, Fill = fill, DataContext = item };
            Bind(canvas, item, path, mode, dotSize);
        }

        private static void Bind(Canvas canvas, WatchItem item, Path path, DrawablesGeometryMode mode, double dotSize)
        {
            var converter = new DrawablesToGeometryConverter { Mode = mode };
            if (dotSize > 0)
            {
                converter.DotSize = dotSize;
            }

            var multi = new MultiBinding
            {
                Converter = converter,
                Mode = BindingMode.OneWay
            };
            multi.Bindings.Add(new Binding("Drawables"));
            multi.Bindings.Add(new Binding("SelectedItem"));
            multi.Bindings.Add(new Binding("Drawables.GeometryVersion"));

            BindingOperations.SetBinding(path, Path.DataProperty, multi);
            canvas.Children.Add(path);
        }

        /// <summary>The virtualised Items ComboBox of NeoWatchWindow.xaml:310.</summary>
        private static ComboBox BuildComboBox(WatchItem item)
        {
            var combo = new ComboBox
            {
                Width = 240,
                Height = 24,
                DisplayMemberPath = "Description",
                ItemsSource = item.Drawables
            };
            VirtualizingStackPanel.SetIsVirtualizing(combo, true);
            VirtualizingStackPanel.SetVirtualizationMode(combo, VirtualizationMode.Recycling);
            ScrollViewer.SetIsDeferredScrollingEnabled(combo, true);
            return combo;
        }

        private static void Layout(FrameworkElement root)
        {
            root.Measure(new Size(CanvasWidth, CanvasHeight + 40));
            root.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight + 40));
            root.UpdateLayout();
        }

        /// <summary>Forces the stroke tessellation and rasterisation WPF would do on screen.</summary>
        private static void Rasterize(FrameworkElement root)
        {
            Layout(root);
            var bitmap = new RenderTargetBitmap((int)CanvasWidth, (int)(CanvasHeight + 40), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Loaded);
        }
    }
}
