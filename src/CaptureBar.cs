using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShotTray
{
    // The Ctrl+Shift+5 toolbar, after macOS's Cmd+Shift+5: pick what to capture or record,
    // set options, then press Capture / Record. Esc or the X closes it.
    class CaptureBar : Form
    {
        enum Kind { Close, Mode, Options, Go }

        class Item
        {
            public Kind Kind;
            public CaptureMode Mode;
            public Rectangle Bounds;
            public string Tip;
        }

        static readonly Color Back = Color.FromArgb(38, 38, 40);
        static readonly Color Fore = Color.FromArgb(236, 236, 236);
        static readonly Color Accent = Color.FromArgb(10, 132, 255);

        readonly float scale;
        readonly Font font;
        readonly List<Item> items = new List<Item>();
        readonly List<int> separators = new List<int>();
        readonly ToolTip tip = new ToolTip();
        readonly ContextMenuStrip options;
        Item hot, pressed;

        // Raised after the bar has closed, with the chosen mode.
        public event Action<CaptureMode> Chosen;

        public CaptureBar()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            font = new Font("Segoe UI", 9.5f);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Back;
            DoubleBuffered = true;
            Text = "ShotTray";

            options = BuildOptions();
            LayoutItems();

            Rectangle area = Screen.FromPoint(Control.MousePosition).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - S(28));
        }

        int S(float v) { return (int)Math.Round(v * scale); }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;         // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        void LayoutItems()
        {
            int h = S(52), pad = S(8), x = pad;
            int btn = S(40), btnW = S(44);
            int top = (h - btn) / 2;

            Add(Kind.Close, CaptureMode.Area, new Rectangle(x, top + S(6), S(28), S(28)), "Close (Esc)");
            x += S(28) + pad;
            separators.Add(x); x += pad;

            Add(Kind.Mode, CaptureMode.Screen, new Rectangle(x, top, btnW, btn), "Capture Entire Screen"); x += btnW + S(2);
            Add(Kind.Mode, CaptureMode.Window, new Rectangle(x, top, btnW, btn), "Capture Selected Window"); x += btnW + S(2);
            Add(Kind.Mode, CaptureMode.Area, new Rectangle(x, top, btnW, btn), "Capture Selected Portion"); x += btnW + pad;
            separators.Add(x); x += pad;

            Add(Kind.Mode, CaptureMode.RecordScreen, new Rectangle(x, top, btnW, btn), "Record Entire Screen"); x += btnW + S(2);
            Add(Kind.Mode, CaptureMode.RecordArea, new Rectangle(x, top, btnW, btn), "Record Selected Portion"); x += btnW + pad;
            separators.Add(x); x += pad;

            int optW = TextRenderer.MeasureText("Options", font).Width + S(28);
            Add(Kind.Options, CaptureMode.Area, new Rectangle(x, top + S(4), optW, btn - S(8)), "Timer and mouse pointer options");
            x += optW + S(6);

            int goW = Math.Max(TextRenderer.MeasureText("Capture", font).Width, TextRenderer.MeasureText("Record", font).Width) + S(32);
            Add(Kind.Go, CaptureMode.Area, new Rectangle(x, top + S(4), goW, btn - S(8)), null);
            x += goW + pad;

            ClientSize = new Size(x, h);
        }

        void Add(Kind kind, CaptureMode mode, Rectangle bounds, string tipText)
        {
            items.Add(new Item { Kind = kind, Mode = mode, Bounds = bounds, Tip = tipText });
        }

        ContextMenuStrip BuildOptions()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Header("Timer"));
            foreach (int seconds in new[] { 0, 5, 10 })
            {
                int s = seconds;
                var item = new ToolStripMenuItem(s == 0 ? "None" : s + " Seconds");
                item.Click += delegate { Settings.TimerSeconds = s; Settings.Save(); };
                item.Tag = s;
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Header("Options"));
            var pointer = new ToolStripMenuItem("Show Mouse Pointer");
            pointer.Click += delegate { Settings.ShowPointer = !Settings.ShowPointer; Settings.Save(); };
            var clicks = new ToolStripMenuItem("Show Mouse Clicks");
            clicks.Click += delegate { Settings.ShowClicks = !Settings.ShowClicks; Settings.Save(); };
            menu.Items.Add(pointer);
            menu.Items.Add(clicks);

            menu.Opening += delegate
            {
                foreach (ToolStripItem i in menu.Items)
                {
                    var m = i as ToolStripMenuItem;
                    if (m != null && m.Tag is int) m.Checked = (int)m.Tag == Settings.TimerSeconds;
                }
                pointer.Checked = Settings.ShowPointer;
                clicks.Checked = Settings.ShowClicks;
            };
            menu.Closed += delegate { hot = null; Invalidate(); };
            return menu;
        }

        static ToolStripItem Header(string text)
        {
            var label = new ToolStripMenuItem(text);
            label.Enabled = false;
            return label;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.Enter) { Go(); return true; }
            if (keyData == Keys.Left || keyData == Keys.Right)
            {
                int m = (int)Settings.Mode + (keyData == Keys.Left ? -1 : 1);
                if (m >= (int)CaptureMode.Screen && m <= (int)CaptureMode.RecordArea) SetMode((CaptureMode)m);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void SetMode(CaptureMode mode)
        {
            Settings.Mode = mode;
            Settings.Save();
            Invalidate();
        }

        void Go()
        {
            CaptureMode mode = Settings.Mode;
            Close();
            if (Chosen != null) Chosen(mode);
        }

        Item ItemAt(Point p)
        {
            return items.Find(i => i.Bounds.Contains(p));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Item item = ItemAt(e.Location);
            if (item == hot) return;
            hot = item;
            string text = item == null ? null : item.Kind == Kind.Go ? GoText() : item.Tip;
            tip.SetToolTip(this, text);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (options.Visible) return;
            hot = null;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = ItemAt(e.Location);
            if (pressed == null)
            {
                // Dragging the empty parts moves the bar.
                Native.ReleaseCapture();
                Native.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
                return;
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Item item = pressed;
            pressed = null;
            Invalidate();
            if (item == null || item != ItemAt(e.Location)) return;

            switch (item.Kind)
            {
                case Kind.Close: Close(); break;
                case Kind.Mode: SetMode(item.Mode); break;
                case Kind.Go: Go(); break;
                case Kind.Options:
                    Point below = PointToScreen(new Point(item.Bounds.Left, item.Bounds.Bottom + S(10)));
                    options.Show(below.Y + options.PreferredSize.Height > Screen.FromControl(this).WorkingArea.Bottom
                        ? PointToScreen(new Point(item.Bounds.Left, -options.PreferredSize.Height - S(6)))
                        : below);
                    break;
            }
        }

        static string GoText()
        {
            return Settings.IsRecording(Settings.Mode) ? "Record" : "Capture";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var line = new Pen(Color.FromArgb(78, 78, 82), Math.Max(1f, scale)))
                foreach (int x in separators)
                    g.DrawLine(line, x, S(12), x, ClientSize.Height - S(12));

            foreach (Item item in items)
            {
                Rectangle r = item.Bounds;
                bool selected = item.Kind == Kind.Mode && item.Mode == Settings.Mode;
                bool down = item == pressed;

                if (item.Kind == Kind.Go)
                {
                    using (var fill = new SolidBrush(down ? ControlPaint.Dark(Accent, 0.1f) : item == hot ? ControlPaint.Light(Accent, 0.15f) : Accent))
                    using (GraphicsPath path = RoundRect(r, S(6)))
                        g.FillPath(fill, path);
                    TextRenderer.DrawText(g, GoText(), font, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    continue;
                }

                int alpha = selected ? 60 : down ? 50 : item == hot ? 28 : 0;
                if (alpha > 0)
                    using (var fill = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                    using (GraphicsPath path = item.Kind == Kind.Close ? Circle(r) : RoundRect(r, S(6)))
                        g.FillPath(fill, path);

                switch (item.Kind)
                {
                    case Kind.Close: DrawClose(g, r); break;
                    case Kind.Mode: DrawModeIcon(g, item.Mode, r); break;
                    case Kind.Options:
                        Rectangle text = new Rectangle(r.X + S(8), r.Y, r.Width - S(20), r.Height);
                        TextRenderer.DrawText(g, "Options", font, text, Fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        DrawChevron(g, new Point(r.Right - S(12), r.Y + r.Height / 2));
                        break;
                }
            }
        }

        void DrawClose(Graphics g, Rectangle r)
        {
            using (var ring = new SolidBrush(Color.FromArgb(70, 70, 74)))
                g.FillEllipse(ring, Rectangle.Inflate(r, -S(3), -S(3)));
            using (var pen = new Pen(Fore, 1.6f * scale))
            {
                int c = S(5);
                Point m = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                g.DrawLine(pen, m.X - c, m.Y - c, m.X + c, m.Y + c);
                g.DrawLine(pen, m.X - c, m.Y + c, m.X + c, m.Y - c);
            }
        }

        void DrawChevron(Graphics g, Point c)
        {
            using (var pen = new Pen(Fore, 1.4f * scale))
            {
                int w = S(4);
                g.DrawLines(pen, new[] { new Point(c.X - w, c.Y - w / 2), new Point(c.X, c.Y + w / 2), new Point(c.X + w, c.Y - w / 2) });
            }
        }

        // Icons drawn to match macOS's: a screen, a window, a dashed portion; recording adds a dot.
        void DrawModeIcon(Graphics g, CaptureMode mode, Rectangle r)
        {
            int w = S(24), h = S(17);
            var box = new Rectangle(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
            float stroke = 1.5f * scale;

            using (var pen = new Pen(Fore, stroke))
            using (var fill = new SolidBrush(Fore))
            {
                switch (mode)
                {
                    case CaptureMode.Screen:
                    case CaptureMode.RecordScreen:
                        using (GraphicsPath p = RoundRect(box, S(2))) g.DrawPath(pen, p);
                        using (var dim = new SolidBrush(Color.FromArgb(90, Fore)))
                            g.FillRectangle(dim, Rectangle.Inflate(box, -S(3), -S(3)));
                        break;
                    case CaptureMode.Window:
                        using (GraphicsPath p = RoundRect(box, S(2))) g.DrawPath(pen, p);
                        g.FillRectangle(fill, box.X, box.Y, box.Width, S(4));
                        break;
                    default:
                        pen.DashPattern = new[] { 2f, 1.5f };
                        g.DrawRectangle(pen, box);
                        break;
                }

                if (Settings.IsRecording(mode))
                {
                    int d = S(10);
                    var dot = new Rectangle(box.Right - d + S(3), box.Bottom - d + S(3), d, d);
                    using (var gap = new SolidBrush(Back)) g.FillEllipse(gap, Rectangle.Inflate(dot, S(2), S(2)));
                    g.FillEllipse(fill, dot);
                }
            }
        }

        static GraphicsPath Circle(Rectangle r)
        {
            var p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tip.Dispose();
                options.Dispose();
                font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
