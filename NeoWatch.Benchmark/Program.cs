using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Threading;
using NeoWatch.Converters;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using GeoPoint = NeoWatch.Geometries.Point;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Regression check for the redraw cost of a single F10.
    ///
    /// Replays the notification sequence of ViewModel.OnWatchItemReloadAsync against the real
    /// WPF MultiBindings declared in NeoWatchWindow.xaml, and counts how many times the geometry
    /// converters actually run. DrawablesToGeometryConverter and WatchItem are compiled from the
    /// production sources, so this cannot drift from the shipping code.
    ///
    /// Expected after A1+A2+A3: 5 passes while loading (one per layer), 0 when loading is off.
    /// Before them it was 15 and 10.
    /// </summary>
    internal static class Program
    {
        private const double ExpectedPassesLoading = 5;
        private const double ExpectedPassesIdle = 0;

        [STAThread]
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }

            if (args.Length > 0 && args[0] == "pipeline")
            {
                Pipeline.Run(ParseSizes(args, new[] { 5000, 20000, 50000 }));
                return 0;
            }

            if (args.Length > 0 && args[0] == "load")
            {
                LoadPhase.Run(ParseSizes(args, new[] { 5000, 20000, 50000 }));
                return 0;
            }

            if (args.Length > 0 && args[0] == "raster")
            {
                RasterExperiment.Run(ParseSizes(args, new[] { 5000, 20000, 50000 }));
                return 0;
            }

            int drawableCount = 5000;
            int steps = 20;

            if (args.Length > 0 && !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out drawableCount))
            {
                Console.Error.WriteLine("Uso: NeoWatch.Benchmark [numDrawables] [numPasos] | pipeline | load | raster");
                return 1;
            }
            if (args.Length > 1 && !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out steps))
            {
                Console.Error.WriteLine("Uso: NeoWatch.Benchmark [numDrawables] [numPasos] | pipeline | load | raster");
                return 1;
            }

            var visitor = new DrawableVisitor(new CoordinateSystem(800f, 600f, new Box(-10f, 10f, -10f, 10f)));
            string formattedCount = drawableCount.ToString("N0", CultureInfo.InvariantCulture);

            Console.WriteLine();
            Console.WriteLine("Coste de render por F10 - " + formattedCount + " drawables, media de " + steps + " pasos");
            Console.WriteLine("Una 'pasada' recorre los " + formattedCount + " drawables y reconstruye un StreamGeometry.");
            Console.WriteLine();
            Console.WriteLine("  +-------------------------------+----------+----------+--------+---------+");
            Console.WriteLine("  | Caso                          | Esperado |   Medido |     ms |         |");
            Console.WriteLine("  +-------------------------------+----------+----------+--------+---------+");

            bool ok = true;
            ok &= Report("Item cargando", true, ExpectedPassesLoading, drawableCount, steps, visitor);
            ok &= Report("Item con carga desactivada", false, ExpectedPassesIdle, drawableCount, steps, visitor);

            Console.WriteLine("  +-------------------------------+----------+----------+--------+---------+");
            Console.WriteLine();
            Console.WriteLine("  Antes de A1+A2+A3 estos numeros eran 15 y 10.");
            Console.WriteLine();

            return ok ? 0 : 1;
        }

        private static int[] ParseSizes(string[] args, int[] fallback)
        {
            var sizes = new List<int>();
            for (int i = 1; i < args.Length; i++)
            {
                int size;
                if (int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
                {
                    sizes.Add(size);
                }
            }
            return sizes.Count > 0 ? sizes.ToArray() : fallback;
        }

        private static bool Report(string label, bool isLoading, double expected, int drawableCount, int steps, DrawableVisitor visitor)
        {
            Measurement measurement = Measure(isLoading, drawableCount, steps, visitor);
            bool ok = Math.Abs(measurement.Passes - expected) < 0.001;

            Console.WriteLine("  | " + label.PadRight(29)
                              + " | " + expected.ToString("F0", CultureInfo.InvariantCulture).PadLeft(8)
                              + " | " + measurement.Passes.ToString("F1", CultureInfo.InvariantCulture).PadLeft(8)
                              + " | " + measurement.Milliseconds.ToString("F2", CultureInfo.InvariantCulture).PadLeft(6)
                              + " | " + (ok ? "  ok" : "FALLO").PadLeft(7) + " |");

            return ok;
        }

        private struct Measurement
        {
            public double Passes;
            public double Milliseconds;
        }

        private static Measurement Measure(bool isLoading, int drawableCount, int steps, DrawableVisitor visitor)
        {
            var item = new WatchItem { Name = "demo" };
            var harness = new Harness(item);

            // Prime: one load, so the collection is populated even when IsLoading is false.
            item.Drawables.AddAndNotify(BuildDrawables(drawableCount, visitor));
            item.SetSelectedItemQuietly(item.Drawables[0]);
            item.Drawables.NotifyGeometriesChanged();
            Pump();

            item.IsLoading = isLoading;

            for (int i = 0; i < 3; i++)
            {
                Step(item, drawableCount, visitor);
                Pump();
            }

            harness.Reset();
            for (int i = 0; i < steps; i++)
            {
                Step(item, drawableCount, visitor);
                Pump();
            }

            return new Measurement
            {
                Passes = harness.TotalCalls / (double)steps,
                Milliseconds = harness.TotalMilliseconds / steps
            };
        }

        /// <summary>One F10, mirroring ViewModel.OnWatchItemReloadAsync.</summary>
        private static void Step(WatchItem item, int drawableCount, DrawableVisitor visitor)
        {
            item.Drawables.Error = null;                                             // ViewModel.cs:405

            if (!item.IsLoading) return;                                             // A1

            item.SetSelectedItemQuietly(null);                                       // ViewModel.cs:423
            item.Drawables.ResetAndNotify();                                         // ViewModel.cs:424
            item.Drawables.AddAndNotify(BuildDrawables(drawableCount, visitor));     // ViewModel.cs:461
            item.SetSelectedItemQuietly(item.Drawables[0]);                          // ViewModel.cs:464  (A2)
            item.Drawables.NotifyGeometriesChanged();                                // ViewModel.cs:465
        }

        private static void Pump()
        {
            // Loaded sits below DataBind, so any deferred binding transfer has already run.
            Dispatcher.CurrentDispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Loaded);
        }

        private static List<IDrawable> BuildDrawables(int count, DrawableVisitor visitor)
        {
            var list = new List<IDrawable>(count);
            var rnd = new Random(1234);

            for (int i = 0; i < count; i++)
            {
                IDrawable drawable;
                switch (i % 5)
                {
                    case 0:
                    case 1:
                    case 2:
                        var start = new GeoPoint(Coord(rnd), Coord(rnd));
                        drawable = new DrawableLineSegment(start, new GeoPoint(start.X + 0.5f + Coord(rnd), start.Y + 0.5f + Coord(rnd)));
                        break;
                    case 3:
                        drawable = new DrawableArcSegment(new GeoPoint(Coord(rnd), Coord(rnd)), 0f, 120f, 1f + (float)rnd.NextDouble());
                        break;
                    default:
                        drawable = new DrawablePoint(Coord(rnd), Coord(rnd));
                        break;
                }

                drawable.TransformGeometry(visitor);
                list.Add(drawable);
            }

            return list;
        }

        private static float Coord(Random rnd)
        {
            return (float)(rnd.NextDouble() * 20.0 - 10.0);
        }

        /// <summary>The five Path elements and MultiBindings of NeoWatchWindow.xaml:94-137.</summary>
        private sealed class Harness
        {
            private readonly CountingConverter[] converters;
            private readonly Path[] paths;

            public Harness(WatchItem item)
            {
                var modes = new[]
                {
                    DrawablesGeometryMode.Main,
                    DrawablesGeometryMode.Selected,
                    DrawablesGeometryMode.Caps,
                    DrawablesGeometryMode.Points,
                    DrawablesGeometryMode.SelectedPoint
                };

                converters = new CountingConverter[modes.Length];
                paths = new Path[modes.Length];

                for (int i = 0; i < modes.Length; i++)
                {
                    converters[i] = new CountingConverter(new DrawablesToGeometryConverter { Mode = modes[i] });

                    // Two sources only: the selection now travels inside the collection.
                    var multi = new MultiBinding { Converter = converters[i], Mode = BindingMode.OneWay };
                    multi.Bindings.Add(new Binding("Drawables"));
                    multi.Bindings.Add(new Binding("Drawables.GeometryVersion"));

                    var path = new Path { DataContext = item };
                    BindingOperations.SetBinding(path, Path.DataProperty, multi);
                    paths[i] = path;
                }
            }

            public int TotalCalls
            {
                get
                {
                    int total = 0;
                    foreach (CountingConverter converter in converters)
                    {
                        total += converter.Calls;
                    }
                    return total;
                }
            }

            public double TotalMilliseconds
            {
                get
                {
                    long ticks = 0;
                    foreach (CountingConverter converter in converters)
                    {
                        ticks += converter.Ticks;
                    }
                    return ticks * 1000.0 / Stopwatch.Frequency;
                }
            }

            public void Reset()
            {
                foreach (CountingConverter converter in converters)
                {
                    converter.Reset();
                }
            }
        }

        private sealed class CountingConverter : IMultiValueConverter
        {
            private readonly DrawablesToGeometryConverter inner;

            public int Calls;
            public long Ticks;

            public CountingConverter(DrawablesToGeometryConverter inner)
            {
                this.inner = inner;
            }

            public void Reset()
            {
                Calls = 0;
                Ticks = 0;
            }

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                Calls++;
                long start = Stopwatch.GetTimestamp();
                object result = inner.Convert(values, targetType, parameter, culture);
                Ticks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                return null;
            }
        }
    }
}
