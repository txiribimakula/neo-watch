using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using NeoWatch.Converters;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using GeoPoint = NeoWatch.Geometries.Point;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Times each of the five converter modes separately, and reports how many figures each one
    /// actually emits. Selected and SelectedPoint walk the whole collection to emit at most one
    /// figure, which is what A5 removes.
    /// </summary>
    internal static class ModeCost
    {
        public static void Run(int[] sizes)
        {
            Console.WriteLine();
            Console.WriteLine("Coste de cada modo del converter - una pasada sobre N drawables");
            Console.WriteLine();
            Console.Write("  " + "Modo".PadRight(16) + "Figuras".PadLeft(10));
            foreach (int size in sizes)
            {
                Console.Write(size.ToString("N0", CultureInfo.InvariantCulture).PadLeft(11));
            }
            Console.WriteLine();
            Console.WriteLine("  " + new string('-', 26 + 11 * sizes.Length));

            var modes = new[]
            {
                DrawablesGeometryMode.Main,
                DrawablesGeometryMode.Selected,
                DrawablesGeometryMode.Caps,
                DrawablesGeometryMode.Points,
                DrawablesGeometryMode.SelectedPoint
            };

            foreach (DrawablesGeometryMode mode in modes)
            {
                Console.Write("  " + mode.ToString().PadRight(16));

                bool figuresWritten = false;
                var cells = new List<string>();

                foreach (int size in sizes)
                {
                    DrawableCollection drawables = Build(size);
                    var converter = new DrawablesToGeometryConverter { Mode = mode };
                    var values = new object[] { drawables, 0 };

                    converter.Convert(values, typeof(Geometry), null, CultureInfo.InvariantCulture);

                    var sw = Stopwatch.StartNew();
                    const int repeats = 20;
                    object result = null;
                    for (int i = 0; i < repeats; i++)
                    {
                        result = converter.Convert(values, typeof(Geometry), null, CultureInfo.InvariantCulture);
                    }
                    double ms = sw.Elapsed.TotalMilliseconds / repeats;

                    if (!figuresWritten)
                    {
                        Console.Write(CountFigures(result as Geometry).ToString("N0", CultureInfo.InvariantCulture).PadLeft(10));
                        figuresWritten = true;
                    }

                    cells.Add(ms.ToString("F2", CultureInfo.InvariantCulture).PadLeft(11));
                }

                Console.WriteLine(string.Concat(cells));
            }

            Console.WriteLine();
            Console.WriteLine("  Figuras = las emitidas con el tamano mas pequeno de la tabla.");
            Console.WriteLine("  Selected y SelectedPoint recorren los N para emitir como mucho una: eso es A5.");
            Console.WriteLine();
        }

        private static int CountFigures(Geometry geometry)
        {
            PathGeometry path = geometry == null ? null : PathGeometry.CreateFromGeometry(geometry);
            return path == null ? 0 : path.Figures.Count;
        }

        /// <summary>Mixed collection with one element selected, like a loaded watch item.</summary>
        private static DrawableCollection Build(int count)
        {
            var drawables = new DrawableCollection();
            var visitor = new DrawableVisitor(new CoordinateSystem(800f, 600f, new Box(-10f, 10f, -10f, 10f)));
            var rnd = new Random(1234);
            var batch = new List<IDrawable>(count);

            for (int i = 0; i < count; i++)
            {
                IDrawable drawable;
                if (i % 2 == 0)
                {
                    var start = new GeoPoint(Coord(rnd), Coord(rnd));
                    drawable = new DrawableLineSegment(start, new GeoPoint(start.X + 1f, start.Y + 1f));
                }
                else
                {
                    drawable = new DrawablePoint(Coord(rnd), Coord(rnd));
                }
                drawable.TransformGeometry(visitor);
                batch.Add(drawable);
            }

            drawables.AddAndNotify(batch);
            // Selection in the middle: the loop cannot short-circuit early either way.
            drawables.SelectedItem = drawables[count / 2];
            return drawables;
        }

        private static float Coord(Random rnd)
        {
            return (float)(rnd.NextDouble() * 20.0 - 10.0);
        }
    }
}
