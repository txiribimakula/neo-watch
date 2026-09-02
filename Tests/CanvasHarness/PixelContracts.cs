using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeoWatch.Converters;
using NeoWatch.Drawing;
using NeoWatch.Drawing.Scene;
using NeoWatch.Rendering;
using Point = NeoWatch.Geometries.Point;

namespace CanvasHarness
{
    internal static class PixelContracts
    {
        private const int Width = 800, Height = 600;
        public static void Run()
        {
            using (var gpu = new NativeRenderer())
            {
                var current = new DrawableCollection();
                current.Add(new DrawablePoint(15, 20));
                current.Add(new DrawableLineSegment(new Point(10, 10), new Point(60, 35)));
                current.Add(new DrawableLineSegment(new Point(80, 20), new Point(80, 20)));
                current.Add(new DrawableArcSegment(new Point(40, 60), -450, -270, 14));
                current.Add(new DrawableArcSegment(new Point(70, 60), 45, 360, 10));
                current.Add(new DrawableArcSegment(new Point(15, 70), 0, 0, 5));
                current.Add(new DrawableArcSegment(new Point(75, 30), 90, 120, 0));
                var previous = new DrawableCollection();
                previous.Add(new DrawablePoint(25, 30));
                previous.Add(new DrawableLineSegment(new Point(15, 15), new Point(65, 40)));
                previous.Add(new DrawableLineSegment(new Point(80, 25), new Point(80, 25)));
                previous.Add(new DrawableArcSegment(new Point(43, 62), -450, -270, 14));
                previous.Add(new DrawableArcSegment(new Point(70, 60), 0, -360, 12));
                var row = new RenderRow { Current = current.CaptureScene(), Previous = previous.CaptureScene(), Color = 0xff126eaf };
                int cases = 0;
                foreach (float dpi in new[] { 1f, 1.25f, 2f })
                foreach (int selection in new[] { 0, 1, 3, 4 })
                foreach (bool rewind in new[] { false, true })
                {
                    var coordinates = new CoordinateSystem(Width, Height, new Box(0, 100, 0, 100));
                    row.SelectedIndex = row.PreviousSelectedIndex = selection;
                    row.ShowPrevious = rewind; row.ShowSense = true;
                    gpu.Resize((int)(Width * dpi), (int)(Height * dpi));
                    gpu.Render(new[] { row }, SceneCamera.From(coordinates), dpi);
                    var actual = gpu.ReadPixelsForTest();
                    var reference = Reference(current, previous, row, coordinates, dpi);
                    string name = "parity-" + selection + "-" + rewind + "-" + dpi.ToString(CultureInfo.InvariantCulture);
                    VerifyCoverage(reference, actual, (int)(Width * dpi), (int)(Height * dpi), (int)Math.Ceiling(dpi), name);
                    if (dpi == 1 && selection == 1 && rewind)
                    {
                        Program.Save(reference, Width, Height, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contract-wpf.png"));
                        Program.Save(actual, Width, Height, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contract-gpu.png"));
                    }
                    cases++;
                }
                VerifyGhostHoles(gpu);
                VerifyOverlap(gpu);
                VerifyPrecisionFallback(gpu);
                Console.WriteLine("Pixel contracts passed: {0} parity cases + ghost holes, overlap and precision guard.", cases);
            }
        }

        private static byte[] Reference(DrawableCollection current, DrawableCollection previous, RenderRow row,
            CoordinateSystem camera, float dpi)
        {
            var visitor = new DrawableVisitor(camera);
            foreach (var item in current) item.TransformGeometry(visitor);
            foreach (var item in previous) item.TransformGeometry(visitor);
            current.SelectedItem = current[row.SelectedIndex]; previous.SelectedItem = previous[row.PreviousSelectedIndex];
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (row.ShowPrevious)
                {
                    Layer(dc, previous, DrawablesGeometryMode.Unselected, 5, 7, .2);
                    Layer(dc, previous, DrawablesGeometryMode.UnselectedPoints, 5, 7, .2);
                }
                Layer(dc, current, DrawablesGeometryMode.Main, 1, 4, .8);
                Layer(dc, current, DrawablesGeometryMode.Selected, 1, 4, .8);
                if (row.ShowSense) Layer(dc, current, DrawablesGeometryMode.Caps, 7, 4, .8);
                Layer(dc, current, DrawablesGeometryMode.Points, 1, 4, .8);
                Layer(dc, current, DrawablesGeometryMode.SelectedPoint, 1, 8, .8);
                if (row.ShowPrevious)
                {
                    Layer(dc, previous, DrawablesGeometryMode.Selected, 5, 8, .2);
                    Layer(dc, previous, DrawablesGeometryMode.SelectedPoint, 5, 8, .2);
                }
            }
            var bitmap = new RenderTargetBitmap((int)(Width * dpi), (int)(Height * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels;
        }

        private static void Layer(DrawingContext dc, DrawableCollection collection, DrawablesGeometryMode mode,
            double thickness, double dot, double opacity)
        {
            var converter = new DrawablesToGeometryConverter { Mode = mode, DotSize = dot };
            var geometry = (Geometry)converter.Convert(new object[] { collection }, typeof(Geometry), null, CultureInfo.InvariantCulture);
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 110, 175));
            bool fill = mode == DrawablesGeometryMode.Points || mode == DrawablesGeometryMode.UnselectedPoints || mode == DrawablesGeometryMode.SelectedPoint;
            var pen = new Pen(brush, thickness) { MiterLimit = 1 };
            if (thickness == 5) pen.StartLineCap = pen.EndLineCap = PenLineCap.Round;
            if (mode == DrawablesGeometryMode.Caps) pen.EndLineCap = PenLineCap.Triangle;
            if (mode == DrawablesGeometryMode.Selected) pen.DashStyle = new DashStyle(new double[] { 3, 2 }, 0);
            dc.PushOpacity(opacity); dc.DrawGeometry(fill ? brush : null, fill ? null : pen, geometry); dc.Pop();
        }

        private static void VerifyCoverage(byte[] expected, byte[] actual, int width, int height, int tolerance, string name)
        {
            int missing = 0, extra = 0, visible = 0;
            var missingPixels = new List<string>();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                if (expected[i + 3] > 30) visible++;
                // One DIP for AA and WPF's flattened-arc dash placement; compare coverage both ways.
                if (expected[i + 3] > 40 && !Nearby(actual, width, height, x, y, tolerance))
                { missing++; if (missingPixels.Count < 20) missingPixels.Add(x + "," + y); }
                if (actual[i + 3] > 40 && !Nearby(expected, width, height, x, y, tolerance)) extra++;
            }
            Console.WriteLine("{0}: visible={1} missing={2} extra={3}", name, visible, missing, extra);
            if (visible == 0 || missing > 10 || extra > 10)
            {
                Console.WriteLine(string.Join(";", missingPixels));
                Program.Save(expected, width, height, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + "-wpf.png"));
                Program.Save(actual, width, height, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + "-gpu.png"));
                throw new Exception("Canvas pixel parity failed: " + name);
            }
        }

        private static bool Nearby(byte[] pixels, int width, int height, int x, int y, int tolerance)
        {
            for (int j = Math.Max(0, y - tolerance); j <= Math.Min(height - 1, y + tolerance); j++)
                for (int i = Math.Max(0, x - tolerance); i <= Math.Min(width - 1, x + tolerance); i++)
                    if (pixels[(j * width + i) * 4 + 3] > 10) return true;
            return false;
        }

        private static void VerifyGhostHoles(NativeRenderer gpu)
        {
            gpu.Resize(100, 100);
            var collection = new DrawableCollection();
            collection.Add(new DrawableLineSegment(new Point(10, 50), new Point(90, 50)));
            var row = new RenderRow { Previous = collection.CaptureScene(), ShowPrevious = true, PreviousSelectedIndex = 0 };
            gpu.Render(new[] { row }, new SceneCamera(0, 0, 1, 100, 100));
            var pixels = gpu.ReadPixelsForTest();
            int stroke = pixels[(50 * 100 + 16) * 4 + 3];
            int hole = pixels[(50 * 100 + 29) * 4 + 3];
            int thickEdge = pixels[(48 * 100 + 16) * 4 + 3];
            if (stroke < 45 || stroke > 55 || hole != 0 || thickEdge < 40)
                throw new Exception("Ghost selection must cut holes in its own thick translucent stroke.");
        }

        private static void VerifyOverlap(NativeRenderer gpu)
        {
            var collection = new DrawableCollection();
            for (int i = 0; i < SceneSnapshot.BlockSize + 1; i++) collection.Add(new DrawablePoint(50, 50));
            gpu.Render(new[] { new RenderRow { Current = collection.CaptureScene() } }, new SceneCamera(0, 0, 1, 100, 100));
            int alpha = gpu.ReadPixelsForTest()[(50 * 100 + 50) * 4 + 3];
            if (alpha < 202 || alpha > 206) throw new Exception("Overlaps across blocks changed layer opacity.");
        }

        private static void VerifyPrecisionFallback(NativeRenderer gpu)
        {
            var collection = new DrawableCollection();
            collection.Add(new DrawableLineSegment(new Point(-1e20f, 0), new Point(1e20f, 0)));
            try
            {
                gpu.Render(new[] { new RenderRow { Current = collection.CaptureScene() } }, new SceneCamera(0, 0, 1, 100, 100));
            }
            catch (InvalidOperationException) { return; }
            throw new Exception("Precision loss must reject the GPU frame for WPF fallback.");
        }
    }
}
