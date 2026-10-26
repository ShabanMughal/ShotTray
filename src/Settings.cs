using Microsoft.Win32;

namespace ShotTray
{
    enum CaptureMode { Screen, Window, Area, RecordScreen, RecordArea }

    // Capture toolbar choices, remembered in HKCU\Software\ShotTray between sessions.
    static class Settings
    {
        const string Key = @"Software\ShotTray";

        public static CaptureMode Mode = CaptureMode.Area;
        public static int TimerSeconds;
        public static bool ShowPointer = true;
        public static bool ShowClicks;

        public static bool IsRecording(CaptureMode mode)
        {
            return mode == CaptureMode.RecordScreen || mode == CaptureMode.RecordArea;
        }

        public static void Load()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key))
            {
                if (key == null) return;
                int mode = (int)key.GetValue("Mode", (int)Mode);
                if (mode >= (int)CaptureMode.Screen && mode <= (int)CaptureMode.RecordArea) Mode = (CaptureMode)mode;
                TimerSeconds = (int)key.GetValue("TimerSeconds", TimerSeconds);
                ShowPointer = (int)key.GetValue("ShowPointer", ShowPointer ? 1 : 0) != 0;
                ShowClicks = (int)key.GetValue("ShowClicks", ShowClicks ? 1 : 0) != 0;
            }
        }

        public static void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
            {
                key.SetValue("Mode", (int)Mode);
                key.SetValue("TimerSeconds", TimerSeconds);
                key.SetValue("ShowPointer", ShowPointer ? 1 : 0);
                key.SetValue("ShowClicks", ShowClicks ? 1 : 0);
            }
        }
    }
}
