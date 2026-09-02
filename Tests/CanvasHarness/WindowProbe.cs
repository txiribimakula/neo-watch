using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using NeoWatch.Drawing;
using NeoWatch.Drawing.Scene;
using NeoWatch.Rendering;

namespace CanvasHarness
{
    internal static class WindowProbe
    {
        public static void Run(string mode)
        {
            if (mode == "software" || mode == "gpu-software") RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            var app = new Application();
            var window = new Window { Title = "Neo Watch GPU composition test", Width = 800, Height = 600, Background = Brushes.White };
            var grid = new Grid();
            window.Content = grid;
            var image = mode == "wpf" || mode == "software" ? null : new D3DImage();
            if (image != null) grid.Children.Add(new Image { Source = image, Stretch = Stretch.Fill });
            grid.Children.Add(new TextBlock { Text = "WPF overlay", FontSize = 24, Foreground = Brushes.Red, IsHitTestVisible = false });
            NativeRenderer gpu = null;
            var collection = new DrawableCollection();
            collection.Add(new DrawablePoint(50, 50));
            collection.Add(new DrawableLineSegment(new NeoWatch.Geometries.Point(10, 10), new NeoWatch.Geometries.Point(90, 90)));
            var row = new RenderRow { Current = collection.CaptureScene(), Color = 0xff2266bb };
            bool stress = mode == "pan-stress";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(stress ? 1 : 100) };
            int tick = 0;
            var stressClock = new Stopwatch();
            long previousTick = 0;
            long maximumGap = 0;
            window.Loaded += (s, e) =>
            {
                Console.WriteLine("WPF tier={0} mode={1}", RenderCapability.Tier >> 16, RenderOptions.ProcessRenderMode);
                if (image == null) return;
                gpu = new NativeRenderer();
                if (mode == "device") return;
                image.Lock();
                // Diagnostic-only readback tests interop even on a machine whose WPF D3D9 renderer is unavailable.
                image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, gpu.Resize(800, 600), mode == "gpu-software");
                image.Unlock();
                stressClock.Start();
                timer.Start();
            };
            timer.Tick += (s, e) =>
            {
                long now = stressClock.ElapsedMilliseconds;
                if (previousTick != 0) maximumGap = Math.Max(maximumGap, now - previousTick);
                previousTick = now;
                image.Lock();
                double pan = stress ? Math.Sin(tick * .05) * 20 : tick * .1;
                gpu.Render(new[] { row }, new SceneCamera(pan, 0, 6, 800, 600));
                image.AddDirtyRect(new Int32Rect(0, 0, 800, 600));
                image.Unlock();
                tick++;
                if (stress && tick == 500)
                {
                    Console.WriteLine("Pan stress: frames={0} elapsed={1}ms max-gap={2}ms", tick,
                        stressClock.ElapsedMilliseconds, maximumGap);
                    window.Close();
                }
            };
            window.Closed += (s, e) =>
            {
                timer.Stop();
                if (image != null) { image.Lock(); image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero); image.Unlock(); }
                gpu?.Dispose();
            };
            app.Run(window);
        }
    }
}
