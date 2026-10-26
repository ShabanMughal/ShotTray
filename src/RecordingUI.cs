using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ShotTray
{
    // Big "3, 2, 1" in the middle of the screen for the toolbar's timer. Esc or a click cancels.
    class Countdown : Form
    {
        readonly Timer tick = new Timer();
        readonly Font font;
        int left;

        // Returns false if cancelled.
        public static bool Run(int seconds)
        {
            using (var c = new Countdown(seconds))
                return c.ShowDialog() == DialogResult.OK;
        }

        Countdown(int seconds)
        {
            left = seconds;
            float scale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            font = new Font("Segoe UI Semibold", 44f * scale, GraphicsUnit.Pixel);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(30, 30, 32);
            ForeColor = Color.White;
            Opacity = 0.92;
            DoubleBuffered = true;
            int side = (int)(110 * scale);
            Size = new Size(side, side);
            Rectangle area = Screen.FromPoint(Control.MousePosition).WorkingArea;
            Location = new Point(area.Left + (area.Width - side) / 2, area.Top + (area.Height - side) / 2);
            Cursor = Cursors.Hand;

            tick.Interval = 1000;
            tick.Tick += delegate
            {
                left--;
                if (left <= 0) { tick.Stop(); DialogResult = DialogResult.OK; }
                else Invalidate();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
            Native.ExcludeFromCapture(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Native.SetForegroundWindow(Handle);
            tick.Start();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            DialogResult = DialogResult.Cancel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, left.ToString(), font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose();
                font.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Floating pill at the top of the recorded screen: blinking dot, elapsed time, stop button.
    // It's created per-monitor DPI aware so it can be placed with the recorder's physical
    // coordinates, and it's kept out of the recording itself.
    class RecordingBar : Form
    {
        readonly Recorder recorder;
        readonly Timer tick = new Timer();
        readonly ToolTip tip = new ToolTip();
        readonly Rectangle target;
        float scale = 1f;
        Font font;
        Rectangle stopRect;
        bool hot, blink;

        public event EventHandler StopClicked;

        public static RecordingBar ShowFor(Recorder recorder, Rectangle area)
        {
            IntPtr previousDpi = Native.EnterPerMonitorDpi();
            try
            {
                var bar = new RecordingBar(recorder, area);
                bar.Show();
                return bar;
            }
            finally
            {
                Native.RestoreDpi(previousDpi);
            }
        }

        RecordingBar(Recorder recorder, Rectangle area)
        {
            this.recorder = recorder;
            target = Native.WorkAreaFor(area);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(32, 32, 34);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            // Start on the right monitor so the window gets that monitor's DPI; sized in OnHandleCreated.
            Bounds = new Rectangle(target.X + target.Width / 2, target.Y + 8, 10, 10);

            tick.Interval = 500;
            tick.Tick += delegate { blink = !blink; Invalidate(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                cp.ClassStyle |= 0x20000;              // CS_DROPSHADOW
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        int S(float v) { return (int)Math.Round(v * scale); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
            Native.ExcludeFromCapture(Handle);

            scale = Native.WindowScale(Handle);
            font = new Font("Segoe UI", 13f * scale, GraphicsUnit.Pixel);
            int w = S(132), h = S(36);
            stopRect = new Rectangle(w - S(34), (h - S(28)) / 2, S(28), S(28));
            Bounds = new Rectangle(target.X + (target.Width - w) / 2, target.Y + S(10), w, h);
            tip.SetToolTip(this, "Stop recording (Ctrl+Shift+5)");
            tick.Start();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && StopClicked != null) StopClicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int d = S(10);
            using (var red = new SolidBrush(Color.FromArgb(blink ? 120 : 255, 255, 59, 48)))
                g.FillEllipse(red, S(12), (Height - d) / 2, d, d);

            TimeSpan t = recorder.Elapsed;
            string text = ((int)t.TotalMinutes).ToString() + ":" + t.Seconds.ToString("00");
            var textRect = new Rectangle(S(28), 0, stopRect.Left - S(30), Height);
            TextRenderer.DrawText(g, text, font, textRect, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            using (var ring = new SolidBrush(hot ? Color.FromArgb(255, 59, 48) : Color.FromArgb(72, 72, 76)))
                g.FillEllipse(ring, stopRect);
            int sq = S(10);
            using (var white = new SolidBrush(Color.White))
                g.FillRectangle(white, stopRect.X + (stopRect.Width - sq) / 2, stopRect.Y + (stopRect.Height - sq) / 2, sq, sq);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose();
                tip.Dispose();
                if (font != null) font.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Thin red frame just outside the recorded area, so it never shows up in the video.
    // Transparent in the middle and click-through everywhere.
    class RegionFrame : Form
    {
        const int Gap = 2, Thickness = 2;

        public static RegionFrame ShowFor(Rectangle area)
        {
            IntPtr previousDpi = Native.EnterPerMonitorDpi();
            try
            {
                var frame = new RegionFrame(area);
                frame.Show();
                return frame;
            }
            finally
            {
                Native.RestoreDpi(previousDpi);
            }
        }

        RegionFrame(Rectangle area)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = Rectangle.Inflate(area, Gap + Thickness, Gap + Thickness);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8 | 0x80000 | 0x20;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ExcludeFromCapture(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(Color.FromArgb(255, 59, 48)))
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                e.Graphics.FillRectangle(brush, 0, 0, w, Thickness);
                e.Graphics.FillRectangle(brush, 0, h - Thickness, w, Thickness);
                e.Graphics.FillRectangle(brush, 0, 0, Thickness, h);
                e.Graphics.FillRectangle(brush, w - Thickness, 0, Thickness, h);
            }
        }
    }
}
