using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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
    /// Replays the notification sequence that a single F10 triggers today in
    /// ViewModel.OnWatchItemReloadAsync, against the real WPF MultiBindings declared in
    /// NeoWatchWindow.xaml, and counts how many times the geometry converters actually run.
    ///
    /// Nothing here is a mock: DrawablesToGeometryConverter and WatchItem are compiled from
    /// the production sources (see the Compile/Link items in the .csproj).
    /// </summary>
    internal static class Program
    {
        private enum Variant
        {
            Current,
            MoveInsideIf,
            QuietSelection,
            SingleSource
        }

        private static readonly FieldInfo SelectedItemField =
            typeof(WatchItem).GetField("selectedItem", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo OnPropertyChangedMethod =
            typeof(WatchItem).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        [STAThread]
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }

            if (args.Length > 0 && args[0] == "pipeline")
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
                Pipeline.Run(sizes.Count > 0 ? sizes.ToArray() : new[] { 5000, 20000, 50000 });
                return 0;
            }

            if (args.Length > 0 && args[0] == "raster")
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
                RasterExperiment.Run(sizes.Count > 0 ? sizes.ToArray() : new[] { 5000, 20000, 50000 });
                return 0;
            }

            int drawableCount = 5000;
            int steps = 20;

            if (args.Length > 0 && !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out drawableCount))
            {
                Console.Error.WriteLine("Uso: NeoWatch.Benchmark [numDrawables] [numPasos]");
                return 1;
            }
            if (args.Length > 1 && !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out steps))
            {
                Console.Error.WriteLine("Uso: NeoWatch.Benchmark [numDrawables] [numPasos]");
                return 1;
            }

            var visitor = new DrawableVisitor(new CoordinateSystem(800f, 600f, new Box(-10f, 10f, -10f, 10f)));
            string formattedCount = drawableCount.ToString("N0", CultureInfo.InvariantCulture);

            Console.WriteLine();
            Console.WriteLine("Coste de render por F10 - " + formattedCount + " drawables, media de " + steps + " pasos");
            Console.WriteLine("Una 'pasada' recorre los " + formattedCount + " drawables y reconstruye un StreamGeometry.");
            Console.WriteLine();

            Report("ITEM CARGANDO (IsLoading = true)", true, drawableCount, steps, visitor);
            Console.WriteLine();
            Report("ITEM CON CARGA DESACTIVADA (IsLoading = false)", false, drawableCount, steps, visitor);

            Console.WriteLine();
            Console.WriteLine("  Actual  ViewModel.cs:488 fuera del if + WatchItem.cs:92 notifica siempre");
            Console.WriteLine("  V1      mover el bloque dentro del if (IsLoading) + guarda de igualdad");
            Console.WriteLine("  V2      V1 + el setter de SelectedItem deja de llamar a NotifyGeometriesChanged");
            Console.WriteLine("  V3      V2 + los 5 MultiBinding dejan de enlazar SelectedItem (lo lee el converter)");
            Console.WriteLine();

            return 0;
        }

        private static void Report(string title, bool isLoading, int drawableCount, int steps, DrawableVisitor visitor)
        {
            Console.WriteLine(title);
            Console.WriteLine("  +----------+-----------------+--------------------+-----------+");
            Console.WriteLine("  | Variante | Pasadas por F10 | ms/F10 (converter) | vs actual |");
            Console.WriteLine("  +----------+-----------------+--------------------+-----------+");

            double baseMs = 0;
            foreach (Variant variant in Enum.GetValues(typeof(Variant)))
            {
                // Best of three: the pass count is exact, but the timing is noisy enough
                // that a single run can make two identical variants look different.
                Measurement measurement = Measure(variant, isLoading, drawableCount, steps, visitor);
                for (int repeat = 1; repeat < 3; repeat++)
                {
                    Measurement candidate = Measure(variant, isLoading, drawableCount, steps, visitor);
                    if (candidate.Milliseconds < measurement.Milliseconds)
                    {
                        measurement = candidate;
                    }
                }

                if (variant == Variant.Current)
                {
                    baseMs = measurement.Milliseconds;
                }

                string delta;
                if (variant == Variant.Current || baseMs <= 0.0001)
                {
                    delta = "-";
                }
                else
                {
                    double percent = 100.0 * (measurement.Milliseconds - baseMs) / baseMs;
                    delta = (percent >= 0 ? "+" : "") + percent.ToString("F0", CultureInfo.InvariantCulture) + "%";
                }

                Console.WriteLine("  | " + Label(variant).PadRight(8)
                                  + " | " + measurement.Passes.ToString("F1", CultureInfo.InvariantCulture).PadLeft(15)
                                  + " | " + measurement.Milliseconds.ToString("F2", CultureInfo.InvariantCulture).PadLeft(18)
                                  + " | " + delta.PadLeft(9) + " |");
            }

            Console.WriteLine("  +----------+-----------------+--------------------+-----------+");
        }

        private static string Label(Variant variant)
        {
            switch (variant)
            {
                case Variant.Current: return "Actual";
                case Variant.MoveInsideIf: return "V1";
                case Variant.QuietSelection: return "V2";
                default: return "V3";
            }
        }

        private struct Measurement
        {
            public double Passes;
            public double Milliseconds;
        }

        private static Measurement Measure(Variant variant, bool isLoading, int drawableCount, int steps, DrawableVisitor visitor)
        {
            var item = new WatchItem { Name = "demo" };
            bool bindSelection = variant != Variant.SingleSource;
            var harness = new Harness(item, bindSelection);

            // Prime: one load, so the collection is populated even when IsLoading is false.
            item.Drawables.AddAndNotify(BuildDrawables(drawableCount, visitor));
            SetSelectionWithoutGeometryNotify(item, item.Drawables[0]);
            item.Drawables.NotifyGeometriesChanged();
            Pump();

            item.IsLoading = isLoading;

            for (int i = 0; i < 3; i++)
            {
                Step(item, variant, drawableCount, visitor);
                Pump();
            }

            harness.Reset();
            for (int i = 0; i < steps; i++)
            {
                Step(item, variant, drawableCount, visitor);
                Pump();
            }

            return new Measurement
            {
                Passes = harness.TotalCalls / (double)steps,
                Milliseconds = harness.TotalMilliseconds / steps
            };
        }

        /// <summary>Replays one F10 for the given variant, mirroring ViewModel.OnWatchItemReloadAsync.</summary>
        private static void Step(WatchItem item, Variant variant, int drawableCount, DrawableVisitor visitor)
        {
            item.Drawables.Error = null;                                             // ViewModel.cs:405

            if (item.IsLoading)
            {
                item.Drawables.ResetAndNotify();                                     // ViewModel.cs:421
                item.Drawables.AddAndNotify(BuildDrawables(drawableCount, visitor)); // ViewModel.cs:458

                if (variant == Variant.QuietSelection || variant == Variant.SingleSource)
                {
                    // Proposed setter: assign + PropertyChanged, but no NotifyGeometriesChanged.
                    SetSelectionWithoutGeometryNotify(item, item.Drawables[0]);
                }

                item.Drawables.NotifyGeometriesChanged();                            // ViewModel.cs:459
            }

            // ViewModel.cs:488 - today this block sits OUTSIDE the if (IsLoading).
            switch (variant)
            {
                case Variant.Current:
                    if (item.Drawables.Count > 0)
                    {
                        item.SelectedItem = item.Drawables[0];
                    }
                    break;

                case Variant.MoveInsideIf:
                    if (item.IsLoading && item.Drawables.Count > 0
                        && !ReferenceEquals(item.SelectedItem, item.Drawables[0]))
                    {
                        item.SelectedItem = item.Drawables[0];
                    }
                    break;

                default:
                    break;                                                           // already applied above
            }
        }

        private static void SetSelectionWithoutGeometryNotify(WatchItem item, IDrawable value)
        {
            SelectedItemField.SetValue(item, value);
            OnPropertyChangedMethod.Invoke(item, new object[] { "SelectedItem" });
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

        /// <summary>The five Path elements and MultiBindings from NeoWatchWindow.xaml:96-137.</summary>
        private sealed class Harness
        {
            private readonly CountingConverter[] converters;
            private readonly Path[] paths;

            public Harness(WatchItem item, bool bindSelection)
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
                    converters[i] = new CountingConverter(
                        new DrawablesToGeometryConverter { Mode = modes[i] },
                        bindSelection ? null : new Func<IDrawable>(delegate { return item.SelectedItem; }));

                    var multi = new MultiBinding { Converter = converters[i], Mode = BindingMode.OneWay };
                    multi.Bindings.Add(new Binding("Drawables"));
                    if (bindSelection)
                    {
                        multi.Bindings.Add(new Binding("SelectedItem"));
                    }
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
            private readonly Func<IDrawable> selectionProvider;

            public int Calls;
            public long Ticks;

            public CountingConverter(DrawablesToGeometryConverter inner, Func<IDrawable> selectionProvider)
            {
                this.inner = inner;
                this.selectionProvider = selectionProvider;
            }

            public void Reset()
            {
                Calls = 0;
                Ticks = 0;
            }

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                // V3 drops the SelectedItem binding, so the selection is handed to the converter
                // directly. The inner converter then does exactly the same amount of work.
                object[] effective = selectionProvider == null
                    ? values
                    : new object[] { values[0], selectionProvider(), null };

                Calls++;
                long start = Stopwatch.GetTimestamp();
                object result = inner.Convert(effective, targetType, parameter, culture);
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
