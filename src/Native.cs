using System;
using System.Drawing;
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
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hWnd);

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
