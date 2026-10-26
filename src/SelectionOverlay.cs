using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ShotTray
{
    enum PickMode { Area, Window, Screen }

    // Full-screen frozen snapshot of every monitor. The user drags a box (Area), or clicks a
    // highlighted window or monitor (Window, Screen). Space switches between Area and Window, like macOS.
    class SelectionOverlay : Form
    {
        readonly Bitmap full;
        readonly Bitmap dimmed;
        readonly List<Rectangle> windows, screens;
        readonly string hint;
        PickMode mode;
        int hover = -1;
        Font labelFont;
        Point start;
        bool selecting;
        Rectangle selection;
        Rectangle labelRect;

        // Returns the selected area, or null if cancelled.
        public static Bitmap Run()
        {
            Rectangle area;
            return Run(PickMode.Area, null, out area);
        }

        // Returns the picked part of the frozen screen, or null if cancelled. area is where it is
        // on screen, in physical pixels. hint is shown over the hovered monitor in Screen mode.
        public static Bitmap Run(PickMode mode, string hint, out Rectangle area)
        {
            area = Rectangle.Empty;
            IntPtr previousDpi = Native.EnterPerMonitorDpi();
            try
            {
                Rectangle vs = Native.VirtualScreen();
                List<Rectangle> screens = ToClient(Native.MonitorRects(), vs);
                List<Rectangle> windows = ToClient(Native.WindowRects(), vs);
                using (var full = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(full))
                        g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy);

                    // A single monitor leaves nothing to choose.
                    if (mode == PickMode.Screen && screens.Count == 1)
                        return Crop(full, screens[0], vs, out area);

                    using (var overlay = new SelectionOverlay(full, vs, mode, hint, windows, screens))
                    {
                        if (overlay.ShowDialog() != DialogResult.OK) return null;
                        return Crop(full, overlay.selection, vs, out area);
                    }
                }
            }
            finally
            {
                Native.RestoreDpi(previousDpi);
            }
        }

        // Screen rectangles → overlay client coordinates, clipped to the overlay.
        static List<Rectangle> ToClient(List<Rectangle> rects, Rectangle vs)
        {
            var result = new List<Rectangle>();
            var bounds = new Rectangle(Point.Empty, vs.Size);
            foreach (Rectangle r in rects)
            {
                Rectangle c = Rectangle.Intersect(new Rectangle(r.X - vs.X, r.Y - vs.Y, r.Width, r.Height), bounds);
                if (c.Width > 1 && c.Height > 1) result.Add(c);
            }
            return result;
        }

        static Bitmap Crop(Bitmap full, Rectangle r, Rectangle vs, out Rectangle area)
        {
            area = new Rectangle(r.X + vs.X, r.Y + vs.Y, r.Width, r.Height);
            var shot = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(shot))
                g.DrawImage(full, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            return shot;
        }

        SelectionOverlay(Bitmap full, Rectangle bounds, PickMode mode, string hint, List<Rectangle> windows, List<Rectangle> screens)
        {
            this.full = full;
            this.mode = mode;
            this.hint = hint;
            this.windows = windows;
            this.screens = screens;
            dimmed = new Bitmap(full);
            using (Graphics g = Graphics.FromImage(dimmed))
            using (var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                g.FillRectangle(shade, 0, 0, dimmed.Width, dimmed.Height);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Cursor = mode == PickMode.Area ? Cursors.Cross : Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: keep out of Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            labelFont = new Font("Segoe UI", 13f * Native.WindowScale(Handle), GraphicsUnit.Pixel);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Native.SetForegroundWindow(Handle);
            UpdateHover(PointToClient(Control.MousePosition));
        }

        List<Rectangle> Candidates
        {
            get { return mode == PickMode.Window ? windows : screens; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            else if (e.KeyCode == Keys.Space && !selecting && mode != PickMode.Screen)
            {
                mode = mode == PickMode.Area ? PickMode.Window : PickMode.Area;
                Cursor = mode == PickMode.Area ? Cursors.Cross : Cursors.Hand;
                selection = Rectangle.Empty;
                hover = -1;
                UpdateHover(PointToClient(Control.MousePosition));
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (mode != PickMode.Area)
            {
                UpdateHover(e.Location);
                if (hover >= 0) DialogResult = DialogResult.OK;
                return;
            }
            selecting = true;
            start = e.Location;
            UpdateSelection(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (selecting) UpdateSelection(e.Location);
            else UpdateHover(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !selecting) return;
            selecting = false;
            // A plain click (no real drag) cancels, like macOS.
            DialogResult = selection.Width >= 4 && selection.Height >= 4 ? DialogResult.OK : DialogResult.Cancel;
        }

        // Window and Screen modes: the frontmost candidate under the cursor lights up.
        void UpdateHover(Point p)
        {
            if (mode == PickMode.Area) return;
            int found = Candidates.FindIndex(r => r.Contains(p));
            if (found == hover) return;
            Rectangle old = selection;
            hover = found;
            selection = hover >= 0 ? Candidates[hover] : Rectangle.Empty;
            Invalidate(Rectangle.Inflate(old, 2, 2));
            Invalidate(Rectangle.Inflate(selection, 2, 2));
        }

        void UpdateSelection(Point p)
        {
            Rectangle oldSel = selection, oldLabel = labelRect;

            selection = Rectangle.Intersect(ClientRectangle, Rectangle.FromLTRB(
                Math.Min(start.X, p.X), Math.Min(start.Y, p.Y),
                Math.Max(start.X, p.X) + 1, Math.Max(start.Y, p.Y) + 1));
            labelRect = LabelBounds(p);

            Rectangle dirty = Rectangle.Union(Rectangle.Inflate(oldSel, 2, 2), Rectangle.Inflate(selection, 2, 2));
            dirty = Rectangle.Union(dirty, Rectangle.Union(oldLabel, labelRect));
            Invalidate(dirty);
        }

        string LabelText()
        {
            return selection.Width + " × " + selection.Height;
        }

        // Size badge just below-right of the cursor, flipped to stay on screen.
        Rectangle LabelBounds(Point cursor)
        {
            Size text = TextRenderer.MeasureText(LabelText(), labelFont);
            int w = text.Width + (int)labelFont.Size, h = text.Height + (int)(labelFont.Size / 2);
            int x = cursor.X + 14, y = cursor.Y + 18;
            if (x + w > ClientSize.Width) x = cursor.X - 14 - w;
            if (y + h > ClientSize.Height) y = cursor.Y - 18 - h;
            return new Rectangle(x, y, w, h);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            if (mode != PickMode.Area)
            {
                PaintPick(g, clip);
                return;
            }

            g.DrawImage(dimmed, clip, clip, GraphicsUnit.Pixel);

            if (selection.Width <= 0 || selection.Height <= 0) return;

            Rectangle bright = Rectangle.Intersect(selection, clip);
            if (!bright.IsEmpty) g.DrawImage(full, bright, bright, GraphicsUnit.Pixel);

            g.CompositingMode = CompositingMode.SourceOver;
            using (var pen = new Pen(Color.White))
                g.DrawRectangle(pen, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);

            using (var badge = new SolidBrush(Color.FromArgb(190, 20, 20, 20)))
                g.FillRectangle(badge, labelRect);
            TextRenderer.DrawText(g, LabelText(), labelFont, labelRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Undimmed screen with the hovered window or monitor tinted blue, as on macOS.
        void PaintPick(Graphics g, Rectangle clip)
        {
            g.DrawImage(full, clip, clip, GraphicsUnit.Pixel);
            if (hover < 0) return;

            g.CompositingMode = CompositingMode.SourceOver;
            using (var tint = new SolidBrush(Color.FromArgb(70, 40, 120, 255)))
                g.FillRectangle(tint, selection);

            if (mode != PickMode.Screen || string.IsNullOrEmpty(hint)) return;
            Size text = TextRenderer.MeasureText(hint, labelFont);
            int w = text.Width + 2 * (int)labelFont.Size, h = text.Height + (int)labelFont.Size;
            var badge = new Rectangle(selection.X + (selection.Width - w) / 2, selection.Y + (selection.Height - h) / 2, w, h);
            using (var fill = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(fill, badge);
            TextRenderer.DrawText(g, hint, labelFont, badge, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dimmed.Dispose();
                if (labelFont != null) labelFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
