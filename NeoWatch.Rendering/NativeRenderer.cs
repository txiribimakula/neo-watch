using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NeoWatch.Drawing.Scene;

namespace NeoWatch.Rendering
{
    public sealed class RenderRow
    {
        public SceneSnapshot Current { get; set; } = SceneSnapshot.Empty;
        public SceneSnapshot Previous { get; set; } = SceneSnapshot.Empty;
        public int SelectedIndex { get; set; } = -1;
        public int PreviousSelectedIndex { get; set; } = -1;
        public uint Color { get; set; } = 0xff000000;
        public bool ShowPrevious { get; set; }
        public bool ShowSense { get; set; }
    }

    public sealed class FrameStatistics
    {
        public double UploadMilliseconds { get; internal set; }
        public double SubmitAndWaitMilliseconds { get; internal set; }
        public int UploadedBlocks { get; internal set; }
        public int ResidentBlocks { get; internal set; }
        public int VisibleBlocks { get; internal set; }
    }

    public sealed class NativeRenderer : IDisposable
    {
        private IntPtr handle;
        private static readonly object libraryLock = new object();
        private static IntPtr library;
        private readonly HashSet<long> resident = new HashSet<long>();
        private readonly HashSet<long> retained = new HashSet<long>();
        private readonly List<long> ids = new List<long>();
        private readonly List<int> offsets = new List<int>();
        private long[] idBuffer = new long[0];
        private int[] offsetBuffer = new int[0];
        private readonly List<long> released = new List<long>();
        public FrameStatistics LastFrame { get; private set; } = new FrameStatistics();
        public int Width { get; private set; }
        public int Height { get; private set; }

        public NativeRenderer(bool enableComparisonBackend = false)
        {
            lock (libraryLock)
            {
                if (library == IntPtr.Zero)
                {
                    string path = Path.Combine(Path.GetDirectoryName(typeof(NativeRenderer).Assembly.Location),
                        "NeoWatch.Renderer.Native.dll");
                    library = LoadLibraryEx(path, IntPtr.Zero, 0x100 | 0x800);
                    if (library == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            if (nw_abi_version() != 1) throw new NotSupportedException("Incompatible native canvas ABI.");
            Check(nw_create(out handle, enableComparisonBackend ? 1 : 0));
        }

        public IntPtr Resize(int width, int height)
        {
            Check(nw_resize(handle, width, height, out IntPtr surface));
            Width = width; Height = height;
            return surface;
        }

        public void Render(IList<RenderRow> rows, SceneCamera camera, float dpi = 1, bool compareDirect2D = false)
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(NativeRenderer));
            if (!camera.IsValid || dpi <= 0) throw new ArgumentException("Invalid canvas camera.");
            LastFrame = new FrameStatistics();
            retained.Clear();
            foreach (var row in rows) { Keep(row.Current); Keep(row.Previous); }
            released.Clear();
            foreach (long id in resident) if (!retained.Contains(id)) released.Add(id);
            foreach (long id in released) { Check(nw_release(handle, id)); resident.Remove(id); }
            var elapsed = Stopwatch.StartNew();
            Check(nw_begin(handle, camera.Left, camera.Bottom, camera.PixelsPerUnit * dpi, dpi));
            SceneBounds viewport = camera.VisibleBounds();
            foreach (var row in rows)
            {
                if (row.ShowPrevious)
                {
                    Prepare(row.Previous, viewport);
                    Layer(row.PreviousSelectedIndex, 0, false, 5, 7, row.Color, .2f);
                    Layer(row.PreviousSelectedIndex, 3, false, 5, 7, row.Color, .2f);
                }
                Prepare(row.Current, viewport);
                Layer(row.SelectedIndex, 0, row.Current.Count == 1, 1, 4, row.Color, .8f, compareDirect2D);
                Layer(row.SelectedIndex, 1, false, 1, 4, row.Color, .8f);
                if (row.ShowSense) Layer(row.SelectedIndex, 2, false, 7, 4, row.Color, .8f);
                Layer(row.SelectedIndex, 3, row.Current.Count == 1, 1, 4, row.Color, .8f);
                Layer(row.SelectedIndex, 4, false, 1, 8, row.Color, .8f);
                if (row.ShowPrevious)
                {
                    Prepare(row.Previous, viewport);
                    Layer(row.PreviousSelectedIndex, 1, false, 5, 8, row.Color, .2f);
                    Layer(row.PreviousSelectedIndex, 4, false, 5, 8, row.Color, .2f);
                }
            }
            Check(nw_end(handle));
            LastFrame.SubmitAndWaitMilliseconds = elapsed.Elapsed.TotalMilliseconds - LastFrame.UploadMilliseconds;
            LastFrame.ResidentBlocks = resident.Count;
        }

        private void Keep(SceneSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.BlockCount; i++) retained.Add(snapshot[i].Id);
        }

        private void Prepare(SceneSnapshot snapshot, SceneBounds viewport)
        {
            ids.Clear(); offsets.Clear();
            snapshot.VisitVisible(viewport, (index, block) =>
            {
                if (!block.IsFinite) throw new NotSupportedException("Non-finite canvas geometry.");
                if (!resident.Contains(block.Id))
                {
                    if (resident.Count >= 2048)
                        throw new NotSupportedException("The GPU canvas cache limit has been reached.");
                    var clock = Stopwatch.StartNew();
                    var primitives = block.CopyPrimitives();
                    Check(nw_upload(handle, block.Id, primitives, primitives.Length));
                    resident.Add(block.Id);
                    LastFrame.UploadMilliseconds += clock.Elapsed.TotalMilliseconds;
                    LastFrame.UploadedBlocks++;
                }
                ids.Add(block.Id); offsets.Add(index * SceneSnapshot.BlockSize);
            });
            LastFrame.VisibleBlocks += ids.Count;
            if (idBuffer.Length < ids.Count)
            {
                int capacity = Math.Max(ids.Count, idBuffer.Length * 2);
                idBuffer = new long[capacity]; offsetBuffer = new int[capacity];
            }
            ids.CopyTo(idBuffer); offsets.CopyTo(offsetBuffer);
        }

        private void Layer(int selected, int mode, bool single, float thickness, float dot,
            uint color, float opacity, bool direct2D = false)
        {
            if (ids.Count == 0 || ((mode == 1 || mode == 4) && selected < 0)) return;
            Check(nw_layer(handle, idBuffer, offsetBuffer, ids.Count, selected, mode,
                single ? 1 : 0, thickness, dot, color, opacity, direct2D ? 1 : 0));
        }

        public byte[] ReadPixelsForTest()
        {
            var pixels = new byte[checked(Width * Height * 4)];
            Check(nw_read_pixels(handle, pixels, pixels.Length));
            return pixels;
        }

        private static void Check(int result)
        {
            if (result >= 0) return;
            string detail = Marshal.PtrToStringAnsi(nw_error());
            throw new InvalidOperationException("Canvas GPU: 0x" + result.ToString("X8") + " " + detail,
                Marshal.GetExceptionForHR(result));
        }
        public void Dispose()
        {
            if (handle != IntPtr.Zero) { nw_destroy(handle); handle = IntPtr.Zero; }
            resident.Clear();
            GC.SuppressFinalize(this);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        private const string Dll = "NeoWatch.Renderer.Native.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_abi_version();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr nw_error();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_create(out IntPtr renderer, int comparison);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_destroy(IntPtr renderer);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_resize(IntPtr renderer, int width, int height, out IntPtr surface);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_upload(IntPtr renderer, long id, [In] ScenePrimitive[] primitives, int count);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_release(IntPtr renderer, long id);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_begin(IntPtr renderer, double left, double bottom, double scale, float dpi);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_layer(IntPtr renderer, long[] ids, int[] offsets,
            int count, int selected, int mode, int single, float thickness, float dot, uint color, float opacity, int direct2D);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_end(IntPtr renderer);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int nw_read_pixels(IntPtr renderer, [Out] byte[] pixels, int size);
    }
}
