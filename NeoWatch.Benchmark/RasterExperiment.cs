using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Isolates WHY rasterising the points path is superlinear, by drawing the same N dots
    /// several different ways and timing the rasterisation of each.
    /// </summary>
    internal static class RasterExperiment
    {
        private const double W = 800;
        private const double H = 600;
        private const double Thickness = 4;

        public static void Run(int[] sizes)
        {
            Console.WriteLine();
            Console.WriteLine("Rasterizado de N puntos - misma imagen, distintas formas de construirla");
            Console.WriteLine();
            Console.Write("  " + "Variante".PadRight(34));
            foreach (int size in sizes)
            {
                Console.Write(size.ToString("N0", CultureInfo.InvariantCulture).PadLeft(11));
            }
            Console.WriteLine();
            Console.WriteLine("  " + new string('-', 34 + 11 * sizes.Length));

            Report("Actual: 1 Path, caps redondos", sizes, (pts) => StreamPath(pts, PenLineCap.Round));
            Report("1 Path, caps planos", sizes, (pts) => StreamPath(pts, PenLineCap.Flat));
            Report("Trozeado: 1 Path por 2.000 pts", sizes, (pts) => Chunked(pts, 2000));
            Report("Trozeado: 1 Path por 500 pts", sizes, (pts) => Chunked(pts, 500));
            Report("DrawingVisual + DrawEllipse", sizes, (pts) => VisualEllipses(pts));
            Report("1 Path relleno, cuadrados 4px", sizes, (pts) => FilledPath(pts));
            Report("DrawingVisual + DrawGeometry", sizes, (pts) => VisualFilled(pts));
            Report("1 Path relleno, circulos (2 arcos)", sizes, (pts) => FilledCirclePath(pts));
            Report("1 Path relleno, octogonos", sizes, (pts) => FilledPolyPath(pts, 8));

            Console.WriteLine();
            Console.WriteLine("  Los caps planos no son un arreglo: sobre una linea degenerada no dibujan nada.");
            Console.WriteLine("  Estan aqui para aislar el coste, que es integramente la generacion de caps redondos.");
            Console.WriteLine();
        }

        /// <summary>N small filled squares in one geometry — filled, never stroked.</summary>
        private static Geometry BuildFilledQuads(IEnumerable<Point> points)
        {
            double half = Thickness / 2.0;
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };

            using (StreamGeometryContext ctx = geometry.Open())
            {
                foreach (Point p in points)
                {
                    ctx.BeginFigure(new Point(p.X - half, p.Y - half), true, true);
                    ctx.LineTo(new Point(p.X + half, p.Y - half), false, false);
                    ctx.LineTo(new Point(p.X + half, p.Y + half), false, false);
                    ctx.LineTo(new Point(p.X - half, p.Y + half), false, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>N filled circles, each as two half-arcs — keeps the dots round.</summary>
        private static Geometry BuildFilledCircles(IEnumerable<Point> points)
        {
            double r = Thickness / 2.0;
            var size = new Size(r, r);
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };

            using (StreamGeometryContext ctx = geometry.Open())
            {
                foreach (Point p in points)
                {
                    var left = new Point(p.X - r, p.Y);
                    var right = new Point(p.X + r, p.Y);
                    ctx.BeginFigure(left, true, true);
                    ctx.ArcTo(right, size, 0, false, SweepDirection.Clockwise, false, false);
                    ctx.ArcTo(left, size, 0, false, SweepDirection.Clockwise, false, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>N filled regular polygons — a cheap round-looking dot.</summary>
        private static Geometry BuildFilledPolys(IEnumerable<Point> points, int sides)
        {
            double r = Thickness / 2.0;
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };

            var offsets = new Point[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = i * 2.0 * Math.PI / sides;
                offsets[i] = new Point(r * Math.Cos(a), r * Math.Sin(a));
            }

            using (StreamGeometryContext ctx = geometry.Open())
            {
                foreach (Point p in points)
                {
                    ctx.BeginFigure(new Point(p.X + offsets[0].X, p.Y + offsets[0].Y), true, true);
                    for (int i = 1; i < sides; i++)
                    {
                        ctx.LineTo(new Point(p.X + offsets[i].X, p.Y + offsets[i].Y), false, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static Visual FilledCirclePath(List<Point> points)
        {
            return Host(new Path { Data = BuildFilledCircles(points), Fill = Stroke(), Opacity = 0.8 });
        }

        private static Visual FilledPolyPath(List<Point> points, int sides)
        {
            return Host(new Path { Data = BuildFilledPolys(points, sides), Fill = Stroke(), Opacity = 0.8 });
        }

        private static Visual FilledPath(List<Point> points)
        {
            var path = new Path
            {
                Data = BuildFilledQuads(points),
                Fill = Stroke(),
                Opacity = 0.8
            };
            return Host(path);
        }

        private static Visual VisualFilled(List<Point> points)
        {
            Geometry geometry = BuildFilledQuads(points);
            Brush fill = Stroke();

            var visual = new DrawingVisual();
            using (DrawingContext ctx = visual.RenderOpen())
            {
                ctx.PushOpacity(0.8);
                ctx.DrawGeometry(fill, null, geometry);
                ctx.Pop();
            }
            return visual;
        }

        private static void Report(string label, int[] sizes, Func<List<Point>, Visual> build)
        {
            Console.Write("  " + label.PadRight(34));
            foreach (int size in sizes)
            {
                List<Point> pts = MakePoints(size);
                Visual visual = build(pts);

                var sw = Stopwatch.StartNew();
                Rasterize(visual);
                double ms = sw.Elapsed.TotalMilliseconds;

                Console.Write(Format(ms).PadLeft(11));
            }
            Console.WriteLine();
        }

        private static string Format(double ms)
        {
            return ms >= 1000
                ? (ms / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + " s"
                : ms.ToString("F0", CultureInfo.InvariantCulture) + " ms";
        }

        private static List<Point> MakePoints(int count)
        {
            var list = new List<Point>(count);
            for (int i = 0; i < count; i++)
            {
                double t = i * (6.0 * Math.PI / count);
                double r = (0.05 * t) * (W / 20.0);
                double angle = t + (i % 2) * Math.PI;
                list.Add(new Point(W / 2 + r * Math.Cos(angle), H / 2 + r * Math.Sin(angle)));
            }
            return list;
        }

        private static Brush Stroke()
        {
            Brush brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0x6F, 0xA5));
            brush.Freeze();
            return brush;
        }

        /// <summary>What the app does today: one StreamGeometry holding N degenerate figures.</summary>
        private static Geometry BuildStream(IEnumerable<Point> points)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (StreamGeometryContext ctx = geometry.Open())
            {
                foreach (Point p in points)
                {
                    ctx.BeginFigure(p, false, false);
                    ctx.LineTo(p, true, false);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static Visual StreamPath(List<Point> points, PenLineCap cap)
        {
            var path = new Path
            {
                Data = BuildStream(points),
                Stroke = Stroke(),
                StrokeThickness = Thickness,
                StrokeStartLineCap = cap,
                StrokeEndLineCap = cap,
                Opacity = 0.8
            };
            return Host(path);
        }

        private static Visual Chunked(List<Point> points, int chunkSize)
        {
            var canvas = new Canvas { Width = W, Height = H };
            Brush stroke = Stroke();

            for (int start = 0; start < points.Count; start += chunkSize)
            {
                int length = Math.Min(chunkSize, points.Count - start);
                canvas.Children.Add(new Path
                {
                    Data = BuildStream(points.GetRange(start, length)),
                    Stroke = stroke,
                    StrokeThickness = Thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0.8
                });
            }

            return Host(canvas);
        }

        /// <summary>No stroking at all: filled dots straight into a DrawingVisual.</summary>
        private static Visual VisualEllipses(List<Point> points)
        {
            Brush fill = Stroke();
            double radius = Thickness / 2.0;

            var visual = new DrawingVisual();
            using (DrawingContext ctx = visual.RenderOpen())
            {
                ctx.PushOpacity(0.8);
                foreach (Point p in points)
                {
                    ctx.DrawEllipse(fill, null, p, radius, radius);
                }
                ctx.Pop();
            }
            return visual;
        }

        private static Visual Host(UIElement child)
        {
            var canvas = new Canvas { Width = W, Height = H };
            canvas.Children.Add(child);
            return canvas;
        }

        private static void Rasterize(Visual visual)
        {
            var element = visual as FrameworkElement;
            if (element != null)
            {
                element.Measure(new Size(W, H));
                element.Arrange(new Rect(0, 0, W, H));
                element.UpdateLayout();
            }

            var bitmap = new RenderTargetBitmap((int)W, (int)H, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
        }
    }
}
