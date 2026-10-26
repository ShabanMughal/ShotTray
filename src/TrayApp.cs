using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace ShotTray
{
    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "ShotTray";
        const string HotkeyText = "Ctrl+Shift+4";
        const string BarHotkeyText = "Ctrl+Shift+5";

        internal static readonly Icon AppIcon = LoadIcon();

        readonly NotifyIcon tray;
        readonly HotkeyWindow hotkey, barHotkey;
        readonly Timer cleanupTimer;
        readonly ToolStripMenuItem startupItem, stopItem;
        ThumbnailForm thumbnail;
        CaptureBar bar;
        Recorder recorder;
        RecordingBar recordingBar;
        RegionFrame regionFrame;
        bool capturing;

        public TrayApp()
        {
            Store.Cleanup();
            try { Settings.Load(); }
            catch (Exception) { } // unreadable values: keep the defaults

            var captureItem = new ToolStripMenuItem("Capture area", null, delegate { CaptureSoon(); });
            captureItem.ShortcutKeyDisplayString = HotkeyText;
            var barItem = new ToolStripMenuItem("Capture or record...", null, delegate { ToggleBar(); });
            barItem.ShortcutKeyDisplayString = BarHotkeyText;
            stopItem = new ToolStripMenuItem("Stop recording", null, delegate { StopRecording(); });
            stopItem.ShortcutKeyDisplayString = BarHotkeyText;
            stopItem.Visible = false;
            startupItem = new ToolStripMenuItem("Start with Windows", null, OnToggleStartup);
            startupItem.Checked = IsStartupEnabled();

            var menu = new ContextMenuStrip();
            menu.Items.Add(stopItem);
            menu.Items.Add(captureItem);
            menu.Items.Add(barItem);
            menu.Items.Add("Open screenshots folder", null, delegate { OpenFolder(); });
            menu.Items.Add("Copy last screenshot path", null, delegate { CopyLastPath(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add("Exit", null, delegate { Exit(); });

            tray = new NotifyIcon();
            tray.Icon = new Icon(AppIcon, SystemInformation.SmallIconSize);
            tray.Text = "ShotTray - " + HotkeyText + " to capture";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (recorder != null) StopRecording();
                else CaptureSoon();
            };
            tray.Visible = true;

            hotkey = new HotkeyWindow(Native.MOD_CONTROL | Native.MOD_SHIFT, Keys.D4);
            hotkey.Pressed += delegate { Capture(); };
            barHotkey = new HotkeyWindow(Native.MOD_CONTROL | Native.MOD_SHIFT, Keys.D5);
            barHotkey.Pressed += delegate { ToggleBar(); };
            if (!hotkey.Registered)
                tray.ShowBalloonTip(5000, "ShotTray", HotkeyText + " is already used by another app. Click the tray icon to capture.", ToolTipIcon.Warning);
            else if (!barHotkey.Registered)
                tray.ShowBalloonTip(5000, "ShotTray", BarHotkeyText + " is already used by another app. Use the tray menu to record.", ToolTipIcon.Warning);
            else
                tray.ShowBalloonTip(2500, "ShotTray is running", "Press " + HotkeyText + " to capture an area, or " + BarHotkeyText + " to record the screen.", ToolTipIcon.None);

            cleanupTimer = new Timer();
            cleanupTimer.Interval = 6 * 60 * 60 * 1000;
            cleanupTimer.Tick += delegate { Store.Cleanup(); };
            cleanupTimer.Start();
        }

        // From the tray: wait for the menu to disappear so it isn't in the shot.
        void CaptureSoon()
        {
            var t = new Timer();
            t.Interval = 200;
            t.Tick += delegate { t.Dispose(); Capture(); };
            t.Start();
        }

        void Capture()
        {
            if (capturing) return;
            capturing = true;
            try
            {
                if (bar != null) bar.Close();
                ClearScreen();
                Bitmap shot = SelectionOverlay.Run();
                if (shot != null) SaveShot(shot);
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(4000, "ShotTray", "Screenshot failed: " + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                capturing = false;
            }
        }

        // Close the thumbnail and give DWM a frame to remove it before the screen is captured.
        void ClearScreen()
        {
            if (thumbnail == null) return;
            thumbnail.Close();
            Application.DoEvents();
            Thread.Sleep(80);
        }

        void SaveShot(Bitmap shot)
        {
            string path = Store.NewPath();
            shot.Save(path, ImageFormat.Png);
            Clip.SetImageAndFile(shot, path);
            ShowThumbnail(shot, path);
        }

        // Ctrl+Shift+5: open or close the toolbar; while recording, stop instead.
        void ToggleBar()
        {
            if (recorder != null) { StopRecording(); return; }
            if (capturing) return;
            if (bar != null) { bar.Close(); return; }

            var b = new CaptureBar();
            b.FormClosed += delegate { if (bar == b) bar = null; b.Dispose(); };
            b.Chosen += mode =>
            {
                // Wait for the bar to leave the screen before freezing it.
                var t = new Timer();
                t.Interval = 150;
                t.Tick += delegate { t.Dispose(); RunMode(mode); };
                t.Start();
            };
            bar = b;
            b.Show();
        }

        void RunMode(CaptureMode mode)
        {
            if (capturing || recorder != null) return;
            capturing = true;
            bool record = Settings.IsRecording(mode);
            try
            {
                ClearScreen();

                // Screenshots: the timer runs before the screen freezes, so a menu can be opened first.
                if (!record && Settings.TimerSeconds > 0)
                {
                    if (!Countdown.Run(Settings.TimerSeconds)) return;
                    Application.DoEvents();
                    Thread.Sleep(80);
                }

                PickMode pick = mode == CaptureMode.Window ? PickMode.Window
                    : mode == CaptureMode.Screen || mode == CaptureMode.RecordScreen ? PickMode.Screen
                    : PickMode.Area;
                Rectangle area;
                Bitmap shot = SelectionOverlay.Run(pick, record ? "Click to record this screen" : "Click to capture this screen", out area);
                if (shot == null) return;
                if (!record)
                {
                    SaveShot(shot);
                    return;
                }
                shot.Dispose();

                // Recordings: pick the area first, then count down.
                if (Settings.TimerSeconds > 0 && !Countdown.Run(Settings.TimerSeconds)) return;
                StartRecording(area, mode == CaptureMode.RecordArea);
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(4000, "ShotTray", (record ? "Recording" : "Screenshot") + " failed: " + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                capturing = false;
            }
        }

        void StartRecording(Rectangle area, bool showFrame)
        {
            var rec = new Recorder(area, Store.NewVideoPath(), Settings.ShowPointer, Settings.ShowClicks);
            rec.Finished += OnRecordingFinished;
            rec.Start();
            recorder = rec;

            recordingBar = RecordingBar.ShowFor(rec, area);
            recordingBar.StopClicked += delegate { StopRecording(); };
            if (showFrame) regionFrame = RegionFrame.ShowFor(area);

            stopItem.Visible = true;
            tray.Text = "ShotTray - recording (click to stop)";
        }

        void StopRecording()
        {
            if (recorder == null) return;
            recorder.Stop();
            CloseRecordingUI();
        }

        void CloseRecordingUI()
        {
            if (recordingBar != null) { recordingBar.Close(); recordingBar.Dispose(); recordingBar = null; }
            if (regionFrame != null) { regionFrame.Close(); regionFrame.Dispose(); regionFrame = null; }
            stopItem.Visible = false;
            tray.Text = "ShotTray - " + HotkeyText + " to capture";
        }

        // Runs on the UI thread once the MP4 is finished.
        void OnRecordingFinished(object sender, EventArgs e)
        {
            var rec = (Recorder)sender;
            if (rec == recorder) recorder = null;
            CloseRecordingUI();

            Bitmap poster = rec.TakePoster();
            if (rec.Error != null)
            {
                if (poster != null) poster.Dispose();
                tray.ShowBalloonTip(4000, "ShotTray", "Recording failed: " + rec.Error.Message, ToolTipIcon.Error);
                return;
            }
            if (poster == null) return;
            Clip.SetFile(rec.FilePath);
            ShowThumbnail(poster, rec.FilePath);
        }

        void ShowThumbnail(Bitmap shot, string path)
        {
            if (thumbnail != null) thumbnail.Close();
            var thumb = new ThumbnailForm(shot, path);
            thumb.FormClosed += delegate { if (thumbnail == thumb) thumbnail = null; thumb.Dispose(); };
            thumb.EditRequested += OnEditRequested;
            thumbnail = thumb;
            thumb.Show();
        }

        // Thumbnail clicked: open markup; after Done, show the edited thumbnail again.
        void OnEditRequested(object sender, EventArgs e)
        {
            var thumb = (ThumbnailForm)sender;
            string path = thumb.FilePath;
            var editor = new EditorForm(new Bitmap(thumb.Screenshot), path);
            editor.FormClosed += delegate
            {
                Bitmap edited = editor.TakeResult();
                editor.Dispose();
                if (edited != null) ShowThumbnail(edited, path);
            };
            editor.Show();
        }

        static void OpenFolder()
        {
            Directory.CreateDirectory(Store.Folder);
            Process.Start("explorer.exe", "\"" + Store.Folder + "\"");
        }

        void CopyLastPath()
        {
            FileInfo[] files = Store.Recent();
            if (files.Length == 0)
            {
                tray.ShowBalloonTip(2500, "ShotTray", "No screenshots yet.", ToolTipIcon.None);
                return;
            }
            Clip.SetText(files[0].FullName);
        }

        static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(RunName) != null;
        }

        void OnToggleStartup(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (startupItem.Checked)
                    key.DeleteValue(RunName, false);
                else
                    key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            }
            startupItem.Checked = IsStartupEnabled();
        }

        void Exit()
        {
            if (recorder != null)
            {
                recorder.Finished -= OnRecordingFinished;
                recorder.StopAndWait(); // finish the MP4 so it stays playable
                recorder = null;
            }
            CloseRecordingUI();
            if (bar != null) bar.Close();
            cleanupTimer.Dispose();
            hotkey.Dispose();
            barHotkey.Dispose();
            if (thumbnail != null) thumbnail.Close();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        // assets\ShotTray.ico, embedded by build.bat. Holds every size; windows pick the one they need.
        static Icon LoadIcon()
        {
            using (Stream s = typeof(TrayApp).Assembly.GetManifestResourceStream("ShotTray.ico"))
                return s != null ? new Icon(s) : SystemIcons.Application;
        }
    }
}
