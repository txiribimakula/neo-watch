using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "window") { WindowProbe.Run(args.Length > 1 ? args[1] : "gpu"); return 0; }
                if (args.Length > 0 && args[0] == "verify") { PixelContracts.Run(); return 0; }
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                int count = args.Length > 0 ? int.Parse(args[0]) : 10000;
                string kind = args.Length > 1 ? args[1] : "mixed";
                string output = args.Length > 2 ? args[2] : AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(output);
                var collection = Scene(count, kind);
                var coordinates = new CoordinateSystem(1000, 700, new Box(-1, 101, -1, 71));
                var camera = SceneCamera.From(coordinates);
                var clock = Stopwatch.StartNew();
                var scene = collection.CaptureScene();
                double prepare = clock.Elapsed.TotalMilliseconds;
                var row = new RenderRow { Current = scene, SelectedIndex = count > 1 ? 1 : 0, Color = 0xff126eaf };
                var deviceClock = Stopwatch.StartNew();
                using (var gpu = new NativeRenderer())
                {
                    Console.WriteLine("Device and shader initialization={0:F2} ms", deviceClock.Elapsed.TotalMilliseconds);
                    gpu.Resize(1000, 700);
                    clock.Restart();
                    gpu.Render(new[] { row }, camera);
                    Console.WriteLine("{0} {1}: scene={2:F2} ms first-complete-GPU={3:F2} ms upload={4:F2} ms blocks={5}",
                        count, kind, prepare, clock.Elapsed.TotalMilliseconds, gpu.LastFrame.UploadMilliseconds, gpu.LastFrame.UploadedBlocks);
                    Save(gpu.ReadPixelsForTest(), 1000, 700, Path.Combine(output, kind + "-gpu.png"));
                    if (count <= 10000)
                    {
                        clock.Restart();
                        var reference = Reference(collection, coordinates);
                        Console.WriteLine("WPF reference (software raster, not VS FPS)={0:F2} ms", clock.Elapsed.TotalMilliseconds);
                        Save(reference, 1000, 700, Path.Combine(output, kind + "-wpf.png"));
                        Compare(reference, gpu.ReadPixelsForTest());
                    }
                    Measure(gpu, row, camera, false);
                    if (kind == "lines" || kind == "arcs")
                    {
                        using (var d2d = new NativeRenderer(true))
                        {
                            d2d.Resize(1000, 700);
                            d2d.Render(new[] { row }, camera, compareDirect2D: true);
                            Measure(d2d, row, camera, true);
                        }
                    }
                    row.Previous = row.Current;
                    collection[count / 2] = new DrawablePoint(12, 15);
                    row.Current = collection.CaptureScene();
                    row.ShowPrevious = true;
                    row.PreviousSelectedIndex = row.SelectedIndex;
                    gpu.Render(new[] { row }, camera);
                    Console.WriteLine("Mutation + rewind: uploaded={0}, resident={1}", gpu.LastFrame.UploadedBlocks, gpu.LastFrame.ResidentBlocks);
                    if (gpu.LastFrame.UploadedBlocks != 1) throw new Exception("Mutation rebuilt unchanged blocks.");
                    gpu.Render(new[] { row }, camera);
                    if (gpu.LastFrame.UploadedBlocks != 0) throw new Exception("Unchanged frame uploaded geometry.");
                    Save(gpu.ReadPixelsForTest(), 1000, 700, Path.Combine(output, kind + "-rewind.png"));
                    gpu.Render(new RenderRow[0], camera);
                    if (gpu.LastFrame.ResidentBlocks != 0) throw new Exception("Old versions retained after clearing.");
                    if (gpu.ReadPixelsForTest().Any(b => b != 0)) throw new Exception("Cleared frame contains old pixels.");
                }
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }

        private static void Measure(NativeRenderer gpu, RenderRow row, SceneCamera camera, bool d2d)
        {
            var samples = new List<double>();
            for (int i = 0; i < 65; i++)
            {
                var moved = new SceneCamera(camera.Left + i * .01, camera.Bottom, camera.PixelsPerUnit, camera.Width, camera.Height);
                var clock = Stopwatch.StartNew();
                gpu.Render(new[] { row }, moved, compareDirect2D: d2d);
                if (i >= 5) samples.Add(clock.Elapsed.TotalMilliseconds);
                if (gpu.LastFrame.UploadedBlocks != 0) throw new Exception("Camera uploaded unchanged geometry.");
            }
            samples.Sort();
            Console.WriteLine("{0} GPU completion (excludes WPF presentation): median={1:F2} p95={2:F2} p99={3:F2} ms",
                d2d ? "D2D paths" : "D3D instances", samples[30], samples[56], samples[59]);
        }

        private static DrawableCollection Scene(int count, string kind)
        {
            var result = new DrawableCollection();
            for (int i = 0; i < count; i++)
            {
                float x = (i * 71 % 997) / 10f, y = (i * 43 % 691) / 10f;
                int type = kind == "points" ? 0 : kind == "lines" ? 1 : kind == "arcs" ? 2 : i % 3;
                if (type == 0) result.Add(new DrawablePoint(x, y));
                else if (type == 1) result.Add(new DrawableLineSegment(new Point(x, y), new Point(x + 1, y + .4f)));
                else result.Add(new DrawableArcSegment(new Point(x, y), (i % 8) * 45, i % 2 == 0 ? 240 : -210, .7f));
            }
            return result;
        }

        private static byte[] Reference(DrawableCollection collection, CoordinateSystem camera)
        {
            var visitor = new DrawableVisitor(camera);
            foreach (var drawable in collection) drawable.TransformGeometry(visitor);
            collection.SelectedItem = collection.Count > 1 ? collection[1] : collection[0];
            var visual = new DrawingVisual();
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 110, 175));
            using (var drawing = visual.RenderOpen())
            {
                Layer(drawing, collection, DrawablesGeometryMode.Main, brush, false);
                Layer(drawing, collection, DrawablesGeometryMode.Selected, brush, false);
                Layer(drawing, collection, DrawablesGeometryMode.Points, brush, true);
                Layer(drawing, collection, DrawablesGeometryMode.SelectedPoint, brush, true);
            }
            var bitmap = new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var bytes = new byte[1000 * 700 * 4]; bitmap.CopyPixels(bytes, 4000, 0); return bytes;
        }

        private static void Layer(DrawingContext drawing, DrawableCollection collection, DrawablesGeometryMode mode, Brush brush, bool fill)
        {
            var converter = new DrawablesToGeometryConverter { Mode = mode, DotSize = mode == DrawablesGeometryMode.SelectedPoint ? 8 : 4 };
            var geometry = (Geometry)converter.Convert(new object[] { collection }, typeof(Geometry), null, CultureInfo.InvariantCulture);
            var pen = new Pen(brush, 1) { MiterLimit = 1 };
            if (mode == DrawablesGeometryMode.Selected) pen.DashStyle = new DashStyle(new double[] { 3, 2 }, 0);
            drawing.PushOpacity(.8); drawing.DrawGeometry(fill ? brush : null, fill ? null : pen, geometry); drawing.Pop();
        }

        private static void Compare(byte[] a, byte[] b)
        {
            int visibleA = 0, visibleB = 0, different = 0;
            for (int i = 3; i < a.Length; i += 4)
            {
                if (a[i] > 30) visibleA++;
                if (b[i] > 30) visibleB++;
                if (Math.Abs(a[i] - b[i]) > 80) different++;
            }
            Console.WriteLine("Pixels alpha>30: WPF={0} GPU={1}; alpha difference>80={2}", visibleA, visibleB, different);
            if (visibleB == 0 || visibleA == 0) throw new Exception("Blank renderer.");
        }

        internal static void Save(byte[] pixels, int width, int height, string file)
        {
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file)) encoder.Save(stream);
        }
    }
}
