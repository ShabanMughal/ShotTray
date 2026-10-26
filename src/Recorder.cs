using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShotTray
{
    // Records an area of the screen to an H.264 MP4 with Media Foundation, which ships with
    // Windows, so there's no encoder to bundle. Frames are grabbed with BitBlt on a worker thread.
    class Recorder
    {
        public const int Fps = 30;
        const long FrameTicks = 10000000 / Fps; // Media Foundation time is in 100 ns units

        readonly Rectangle area;
        readonly string path;
        readonly bool showPointer, showClicks;
        readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        readonly ManualResetEvent started = new ManualResetEvent(false);
        readonly Stopwatch clock = new Stopwatch();
        SynchronizationContext sync;
        Thread thread;
        Exception startError;
        Bitmap poster;

        IntPtr screenDC, memDC, dib, oldBitmap, bits;
        int width, height;
        IMFSinkWriter writer;
        int stream;
        IntPtr lastCursor;
        Point hotspot;

        // Raised on the UI thread once the file is finished (or recording failed).
        public event EventHandler Finished;

        public Exception Error { get; private set; }
        public string FilePath { get { return path; } }
        public TimeSpan Elapsed { get { return clock.Elapsed; } }

        // area is in physical pixels (per-monitor DPI space), as returned by SelectionOverlay.
        public Recorder(Rectangle area, string path, bool showPointer, bool showClicks)
        {
            // H.264 needs even dimensions.
            this.area = new Rectangle(area.X, area.Y, area.Width & ~1, area.Height & ~1);
            this.path = path;
            this.showPointer = showPointer;
            this.showClicks = showClicks;
        }

        // Starts recording; throws if the encoder can't be set up.
        public void Start()
        {
            if (area.Width < 32 || area.Height < 32) throw new InvalidOperationException("The area is too small to record.");
            sync = SynchronizationContext.Current;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            started.WaitOne();
            if (startError != null)
            {
                thread.Join();
                throw startError;
            }
        }

        public void Stop()
        {
            stopEvent.Set();
        }

        // For app exit: make sure the MP4 is finalized before the process ends.
        public void StopAndWait()
        {
            stopEvent.Set();
            if (thread != null) thread.Join();
        }

        // First frame, for the thumbnail. The caller owns it.
        public Bitmap TakePoster()
        {
            Bitmap p = poster;
            poster = null;
            return p;
        }

        void Run()
        {
            // Physical pixels, like the overlay, so the area and the cursor line up on every monitor.
            IntPtr previousDpi = Native.EnterPerMonitorDpi();
            bool mfStarted = false;
            try
            {
                MF.Check(MF.MFStartup(MF.Version, 0));
                mfStarted = true;
                OpenCapture();
                try { writer = CreateWriter(true); }
                catch (Exception) { writer = CreateWriter(false); } // no usable hardware encoder
            }
            catch (Exception ex)
            {
                startError = ex is COMException
                    ? new InvalidOperationException("Video encoding isn't available (" + ex.Message + "). On Windows N editions, install the Media Feature Pack.", ex)
                    : ex;
            }

            if (startError != null)
            {
                Cleanup(mfStarted);
                TryDelete();
                started.Set();
                return;
            }

            started.Set();
            MF.timeBeginPeriod(1); // sharper Sleep/Wait timing for an even frame rate
            try
            {
                Loop();
                writer.Finish();
            }
            catch (Exception ex)
            {
                Error = ex;
            }
            finally
            {
                MF.timeEndPeriod(1);
                clock.Stop();
                Cleanup(mfStarted);
                Native.RestoreDpi(previousDpi);
            }
            if (Error != null) TryDelete();

            if (sync != null) sync.Post(delegate { RaiseFinished(); }, null);
            else RaiseFinished();
        }

        void RaiseFinished()
        {
            if (Finished != null) Finished(this, EventArgs.Empty);
        }

        void Loop()
        {
            clock.Start();
            long next = 0;
            while (true)
            {
                long now = clock.Elapsed.Ticks;
                if (now < next)
                {
                    if (stopEvent.WaitOne((int)Math.Max(1, (next - now) / 10000))) break;
                    continue;
                }
                if (stopEvent.WaitOne(0)) break;

                // If capture or encoding falls behind, skip frames rather than drift.
                next += FrameTicks;
                if (next <= now) next = now + FrameTicks;

                GrabFrame();
                if (poster == null) poster = CopyFrame();
                WriteFrame(now);
            }
        }

        void OpenCapture()
        {
            width = area.Width;
            height = area.Height;
            screenDC = GDI.GetDC(IntPtr.Zero);
            memDC = GDI.CreateCompatibleDC(screenDC);
            var bmi = new GDI.BITMAPINFOHEADER();
            bmi.Size = Marshal.SizeOf(typeof(GDI.BITMAPINFOHEADER));
            bmi.Width = width;
            bmi.Height = -height; // top-down
            bmi.Planes = 1;
            bmi.BitCount = 32;
            dib = GDI.CreateDIBSection(memDC, ref bmi, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) throw new OutOfMemoryException("Couldn't allocate the frame buffer.");
            oldBitmap = GDI.SelectObject(memDC, dib);
        }

        void GrabFrame()
        {
            GDI.BitBlt(memDC, 0, 0, width, height, screenDC, area.X, area.Y, 0x00CC0020); // SRCCOPY
            if (!showPointer && !showClicks) return;

            var ci = new GDI.CURSORINFO();
            ci.Size = Marshal.SizeOf(typeof(GDI.CURSORINFO));
            if (!GDI.GetCursorInfo(ref ci)) return;
            int x = ci.X - area.X, y = ci.Y - area.Y;

            if (showClicks && ((GDI.GetAsyncKeyState(0x01) | GDI.GetAsyncKeyState(0x02)) & 0x8000) != 0) // VK_LBUTTON, VK_RBUTTON
            {
                using (Graphics g = Graphics.FromHdc(memDC))
                using (var fill = new SolidBrush(Color.FromArgb(90, 255, 204, 0)))
                using (var ring = new Pen(Color.FromArgb(200, 255, 170, 0), 2f))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    const int r = 20;
                    g.FillEllipse(fill, x - r, y - r, 2 * r, 2 * r);
                    g.DrawEllipse(ring, x - r, y - r, 2 * r, 2 * r);
                }
            }

            if (showPointer && (ci.Flags & 1) != 0 && ci.Cursor != IntPtr.Zero) // CURSOR_SHOWING
            {
                if (ci.Cursor != lastCursor)
                {
                    lastCursor = ci.Cursor;
                    hotspot = Point.Empty;
                    GDI.ICONINFO ii;
                    if (GDI.GetIconInfo(ci.Cursor, out ii))
                    {
                        hotspot = new Point(ii.HotspotX, ii.HotspotY);
                        if (ii.Mask != IntPtr.Zero) GDI.DeleteObject(ii.Mask);
                        if (ii.Color != IntPtr.Zero) GDI.DeleteObject(ii.Color);
                    }
                }
                GDI.DrawIconEx(memDC, x - hotspot.X, y - hotspot.Y, ci.Cursor, 0, 0, 0, IntPtr.Zero, 3); // DI_NORMAL
            }
        }

        Bitmap CopyFrame()
        {
            var copy = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (var view = new Bitmap(width, height, width * 4, PixelFormat.Format32bppRgb, bits))
            using (Graphics g = Graphics.FromImage(copy))
                g.DrawImageUnscaled(view, 0, 0);
            return copy;
        }

        void WriteFrame(long time)
        {
            int size = width * 4 * height;
            IMFMediaBuffer buffer;
            MF.Check(MF.MFCreateMemoryBuffer(size, out buffer));
            IMFSample sample = null;
            try
            {
                IntPtr dest;
                int max, current;
                buffer.Lock(out dest, out max, out current);
                GDI.CopyMemory(dest, bits, new IntPtr(size));
                buffer.Unlock();
                buffer.SetCurrentLength(size);

                MF.Check(MF.MFCreateSample(out sample));
                sample.AddBuffer(buffer);
                sample.SetSampleTime(time);
                sample.SetSampleDuration(FrameTicks);
                writer.WriteSample(stream, sample);
            }
            finally
            {
                if (sample != null) Marshal.ReleaseComObject(sample);
                Marshal.ReleaseComObject(buffer);
            }
        }

        IMFSinkWriter CreateWriter(bool hardware)
        {
            IMFAttributes attrs = null;
            IMFSinkWriter w = null;
            IMFMediaType output = null, input = null;
            try
            {
                MF.Check(MF.MFCreateAttributes(out attrs, 1));
                attrs.SetUINT32(ref MF.ReadWriteEnableHardwareTransforms, hardware ? 1 : 0);
                MF.Check(MF.MFCreateSinkWriterFromURL(path, IntPtr.Zero, attrs, out w));

                // Roughly 0.15 bits per pixel: crisp text without huge files.
                long bitrate = Math.Max(1000000L, Math.Min(40000000L, (long)width * height * Fps * 15 / 100));
                MF.Check(MF.MFCreateMediaType(out output));
                output.SetGUID(ref MF.MajorType, ref MF.MediaTypeVideo);
                output.SetGUID(ref MF.Subtype, ref MF.VideoFormatH264);
                output.SetUINT32(ref MF.AvgBitrate, (int)bitrate);
                SetVideoFormat(output);
                w.AddStream(output, out stream);

                MF.Check(MF.MFCreateMediaType(out input));
                input.SetGUID(ref MF.MajorType, ref MF.MediaTypeVideo);
                input.SetGUID(ref MF.Subtype, ref MF.VideoFormatRGB32);
                input.SetUINT32(ref MF.DefaultStride, width * 4); // positive stride = top-down rows, like the DIB
                SetVideoFormat(input);
                w.SetInputMediaType(stream, input, IntPtr.Zero);

                w.BeginWriting();
                IMFSinkWriter result = w;
                w = null;
                return result;
            }
            finally
            {
                if (w != null) Marshal.ReleaseComObject(w);
                if (input != null) Marshal.ReleaseComObject(input);
                if (output != null) Marshal.ReleaseComObject(output);
                if (attrs != null) Marshal.ReleaseComObject(attrs);
            }
        }

        void SetVideoFormat(IMFMediaType type)
        {
            type.SetUINT32(ref MF.InterlaceMode, 2); // MFVideoInterlace_Progressive
            type.SetUINT64(ref MF.FrameSize, ((long)width << 32) | (uint)height);
            type.SetUINT64(ref MF.FrameRate, ((long)Fps << 32) | 1);
            type.SetUINT64(ref MF.PixelAspectRatio, (1L << 32) | 1);
        }

        void Cleanup(bool mfStarted)
        {
            if (writer != null) { Marshal.ReleaseComObject(writer); writer = null; }
            if (oldBitmap != IntPtr.Zero) GDI.SelectObject(memDC, oldBitmap);
            if (dib != IntPtr.Zero) GDI.DeleteObject(dib);
            if (memDC != IntPtr.Zero) GDI.DeleteDC(memDC);
            if (screenDC != IntPtr.Zero) GDI.ReleaseDC(IntPtr.Zero, screenDC);
            oldBitmap = dib = memDC = screenDC = bits = IntPtr.Zero;
            if (mfStarted) MF.MFShutdown();
        }

        void TryDelete()
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    static class GDI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int Size, Flags;
            public IntPtr Cursor;
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool IsIcon;
            public int HotspotX, HotspotY;
            public IntPtr Mask, Color;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO info);
        [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
        [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int w, int h, int step, IntPtr brush, int flags);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, int usage, out IntPtr bits, IntPtr section, int offset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] public static extern void CopyMemory(IntPtr dest, IntPtr src, IntPtr count);
    }

    // Just enough of Media Foundation for the sink writer. Interfaces list vtable slots in
    // order; the Unused ones are never called but must be there to keep the slots aligned.
    static class MF
    {
        public const int Version = 0x00020070; // MF_VERSION

        public static Guid MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static Guid Subtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        public static Guid AvgBitrate = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
        public static Guid InterlaceMode = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        public static Guid FrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        public static Guid FrameRate = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        public static Guid PixelAspectRatio = new Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
        public static Guid DefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        public static Guid MediaTypeVideo = new Guid("73646976-0000-0010-8000-00aa00389b71");
        public static Guid VideoFormatH264 = new Guid("34363248-0000-0010-8000-00aa00389b71");
        public static Guid VideoFormatRGB32 = new Guid("00000016-0000-0010-8000-00aa00389b71");
        public static Guid ReadWriteEnableHardwareTransforms = new Guid("a634a91c-822b-41b9-a494-4de4643612b0");

        [DllImport("mfplat.dll")] public static extern int MFStartup(int version, int flags);
        [DllImport("mfplat.dll")] public static extern int MFShutdown();
        [DllImport("mfplat.dll")] public static extern int MFCreateAttributes(out IMFAttributes attrs, int size);
        [DllImport("mfplat.dll")] public static extern int MFCreateMediaType(out IMFMediaType type);
        [DllImport("mfplat.dll")] public static extern int MFCreateSample(out IMFSample sample);
        [DllImport("mfplat.dll")] public static extern int MFCreateMemoryBuffer(int length, out IMFMediaBuffer buffer);
        [DllImport("mfreadwrite.dll")]
        public static extern int MFCreateSinkWriterFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr byteStream, IMFAttributes attrs, out IMFSinkWriter writer);
        [DllImport("winmm.dll")] public static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll")] public static extern int timeEndPeriod(int ms);

        public static void Check(int hr)
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }
    }

    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFAttributes
    {
        void Unused0(); void Unused1(); void Unused2(); void Unused3(); void Unused4(); void Unused5();
        void Unused6(); void Unused7(); void Unused8(); void Unused9(); void Unused10(); void Unused11();
        void Unused12(); void Unused13(); void Unused14(); void Unused15(); void Unused16(); void Unused17();
        void SetUINT32(ref Guid key, int value);
        void SetUINT64(ref Guid key, long value);
        void SetDouble(ref Guid key, double value);
        void SetGUID(ref Guid key, ref Guid value);
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFMediaType
    {
        void Unused0(); void Unused1(); void Unused2(); void Unused3(); void Unused4(); void Unused5();
        void Unused6(); void Unused7(); void Unused8(); void Unused9(); void Unused10(); void Unused11();
        void Unused12(); void Unused13(); void Unused14(); void Unused15(); void Unused16(); void Unused17();
        void SetUINT32(ref Guid key, int value);
        void SetUINT64(ref Guid key, long value);
        void SetDouble(ref Guid key, double value);
        void SetGUID(ref Guid key, ref Guid value);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFSample
    {
        // IMFAttributes (30 slots)
        void Unused0(); void Unused1(); void Unused2(); void Unused3(); void Unused4(); void Unused5();
        void Unused6(); void Unused7(); void Unused8(); void Unused9(); void Unused10(); void Unused11();
        void Unused12(); void Unused13(); void Unused14(); void Unused15(); void Unused16(); void Unused17();
        void Unused18(); void Unused19(); void Unused20(); void Unused21(); void Unused22(); void Unused23();
        void Unused24(); void Unused25(); void Unused26(); void Unused27(); void Unused28(); void Unused29();
        // IMFSample
        void GetSampleFlags(out int flags);
        void SetSampleFlags(int flags);
        void GetSampleTime(out long time);
        void SetSampleTime(long time);
        void GetSampleDuration(out long duration);
        void SetSampleDuration(long duration);
        void GetBufferCount(out int count);
        void GetBufferByIndex(int index, out IMFMediaBuffer buffer);
        void ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
        void AddBuffer(IMFMediaBuffer buffer);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFMediaBuffer
    {
        void Lock(out IntPtr buffer, out int maxLength, out int currentLength);
        void Unlock();
        void GetCurrentLength(out int length);
        void SetCurrentLength(int length);
        void GetMaxLength(out int length);
    }

    [ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFSinkWriter
    {
        void AddStream(IMFMediaType type, out int streamIndex);
        void SetInputMediaType(int streamIndex, IMFMediaType type, IntPtr encodingParameters);
        void BeginWriting();
        void WriteSample(int streamIndex, IMFSample sample);
        void SendStreamTick(int streamIndex, long timestamp);
        void PlaceMarker(int streamIndex, IntPtr context);
        void NotifyEndOfSegment(int streamIndex);
        void Flush(int streamIndex);
        void Finish(); // IMFSinkWriter::Finalize
    }
}
