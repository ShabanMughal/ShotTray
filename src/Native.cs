using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShotTray
{
    static class Native
    {
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;

        static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr param);
        delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr param);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO
        {
            public int Size;
            public RECT Monitor, Work;
            public int Flags;
        }

        // The app runs system-DPI-aware so menus and the thumbnail scale nicely, but capture
        // must use real pixels across mixed-DPI monitors, so the overlay switches the thread
        // to per-monitor awareness while it is open.
        public static IntPtr EnterPerMonitorDpi()
        {
            try { return SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch (EntryPointNotFoundException) { return IntPtr.Zero; }
        }

        public static void RestoreDpi(IntPtr previous)
        {
            if (previous == IntPtr.Zero) return;
            try { SetThreadDpiAwarenessContext(previous); }
            catch (EntryPointNotFoundException) { }
        }

        public static float WindowScale(IntPtr hWnd)
        {
            try { return GetDpiForWindow(hWnd) / 96f; }
            catch (EntryPointNotFoundException) { return 1f; }
        }

        // Keeps a window (stop button, countdown, frame) out of screenshots and recordings.
        // WDA_EXCLUDEFROMCAPTURE needs Windows 10 2004; older versions just ignore it.
        public static void ExcludeFromCapture(IntPtr hWnd)
        {
            try { SetWindowDisplayAffinity(hWnd, 0x11); }
            catch (EntryPointNotFoundException) { }
        }

        // Windows 11 rounded corners for borderless windows; no effect on Windows 10.
        public static void RoundCorners(IntPtr hWnd)
        {
            int round = 2; // DWMWCP_ROUND
            try { DwmSetWindowAttribute(hWnd, 33, ref round, 4); } // DWMWA_WINDOW_CORNER_PREFERENCE
            catch (DllNotFoundException) { }
        }

        // Visible top-level windows, front to back, in the calling thread's DPI space.
        public static List<Rectangle> WindowRects()
        {
            var rects = new List<Rectangle>();
            var name = new StringBuilder(64);
            EnumWindowsProc proc = (hWnd, p) =>
            {
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;
                if ((GetWindowLong(hWnd, -20) & 0x20) != 0) return true; // GWL_EXSTYLE, WS_EX_TRANSPARENT: click-through overlays
                int cloaked;
                if (DwmGetWindowAttribute(hWnd, 14, out cloaked, 4) == 0 && cloaked != 0) return true; // DWMWA_CLOAKED: other virtual desktops, suspended apps
                name.Length = 0;
                GetClassName(hWnd, name, name.Capacity);
                string cls = name.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true; // the desktop itself

                // The visible frame, without the invisible resize borders.
                RECT r;
                if (DwmGetWindowAttribute(hWnd, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) return true; // DWMWA_EXTENDED_FRAME_BOUNDS
                Rectangle rect = r.ToRectangle();
                if (rect.Width > 1 && rect.Height > 1) rects.Add(rect);
                return true;
            };
            EnumWindows(proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            return rects;
        }

        // Each monitor's bounds, in the calling thread's DPI space.
        public static List<Rectangle> MonitorRects()
        {
            var rects = new List<Rectangle>();
            MonitorEnumProc proc = (IntPtr m, IntPtr hdc, ref RECT r, IntPtr p) => { rects.Add(r.ToRectangle()); return true; };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            return rects;
        }

        // Work area (monitor minus taskbar) of the monitor that holds most of the rectangle.
        public static Rectangle WorkAreaFor(Rectangle area)
        {
            RECT r = new RECT { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom };
            var info = new MONITORINFO();
            info.Size = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(MonitorFromRect(ref r, 2), ref info); // MONITOR_DEFAULTTONEAREST
            return info.Work.ToRectangle();
        }

        // Bounds of all monitors combined, in the calling thread's DPI space.
        public static Rectangle VirtualScreen()
        {
            return new Rectangle(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        }
    }

    // Message-only window that receives the global hotkey.
    class HotkeyWindow : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const int Id = 1;

        public event EventHandler Pressed;
        public readonly bool Registered;

        public HotkeyWindow(uint modifiers, Keys key)
        {
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
            Registered = Native.RegisterHotKey(Handle, Id, modifiers | Native.MOD_NOREPEAT, (uint)key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && Pressed != null)
                Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Registered) Native.UnregisterHotKey(Handle, Id);
            DestroyHandle();
        }
    }
}
