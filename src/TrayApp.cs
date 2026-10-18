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

        internal static readonly Icon AppIcon = LoadIcon();

        readonly NotifyIcon tray;
        readonly HotkeyWindow hotkey;
        readonly Timer cleanupTimer;
        readonly ToolStripMenuItem startupItem;
        ThumbnailForm thumbnail;
        bool capturing;

        public TrayApp()
        {
            Store.Cleanup();

            var captureItem = new ToolStripMenuItem("Capture area", null, delegate { CaptureSoon(); });
            captureItem.ShortcutKeyDisplayString = HotkeyText;
            startupItem = new ToolStripMenuItem("Start with Windows", null, OnToggleStartup);
            startupItem.Checked = IsStartupEnabled();

            var menu = new ContextMenuStrip();
            menu.Items.Add(captureItem);
            menu.Items.Add("Open screenshots folder", null, delegate { OpenFolder(); });
            menu.Items.Add("Copy last screenshot path", null, delegate { CopyLastPath(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add("Exit", null, delegate { Exit(); });

            tray = new NotifyIcon();
            tray.Icon = new Icon(AppIcon, SystemInformation.SmallIconSize);
            tray.Text = "ShotTray - " + HotkeyText + " to capture";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) CaptureSoon(); };
            tray.Visible = true;

            hotkey = new HotkeyWindow(Native.MOD_CONTROL | Native.MOD_SHIFT, Keys.D4);
            hotkey.Pressed += delegate { Capture(); };
            if (hotkey.Registered)
                tray.ShowBalloonTip(2500, "ShotTray is running", "Press " + HotkeyText + " to capture an area.", ToolTipIcon.None);
            else
                tray.ShowBalloonTip(5000, "ShotTray", HotkeyText + " is already used by another app. Click the tray icon to capture.", ToolTipIcon.Warning);

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
                if (thumbnail != null)
                {
                    thumbnail.Close();
                    Application.DoEvents();
                    Thread.Sleep(80); // give DWM a frame to remove it from the screen
                }

                Bitmap shot = SelectionOverlay.Run();
                if (shot == null) return;

                string path = Store.NewPath();
                shot.Save(path, ImageFormat.Png);
                Clip.SetImageAndFile(shot, path);

                ShowThumbnail(shot, path);
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
            cleanupTimer.Dispose();
            hotkey.Dispose();
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
