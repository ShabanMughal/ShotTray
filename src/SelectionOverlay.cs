using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ShotTray
{
    // Full-screen frozen snapshot of every monitor; the user drags a box to pick an area.
    class SelectionOverlay : Form
    {
        readonly Bitmap full;
        readonly Bitmap dimmed;
        Font labelFont;
        Point start;
        bool selecting;
        Rectangle selection;
        Rectangle labelRect;

        // Returns the selected area, or null if cancelled.
        public static Bitmap Run()
        {
            IntPtr previousDpi = Native.EnterPerMonitorDpi();
            try
            {
                Rectangle vs = Native.VirtualScreen();
                using (var full = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(full))
                        g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy);

                    using (var overlay = new SelectionOverlay(full, vs))
                    {
                        if (overlay.ShowDialog() != DialogResult.OK) return null;

                        Rectangle r = overlay.selection;
                        var shot = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
                        using (Graphics g = Graphics.FromImage(shot))
                            g.DrawImage(full, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
                        return shot;
                    }
                }
            }
            finally
            {
                Native.RestoreDpi(previousDpi);
            }
        }

        SelectionOverlay(Bitmap full, Rectangle bounds)
        {
            this.full = full;
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
            Cursor = Cursors.Cross;
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
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
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
            selecting = true;
            start = e.Location;
            UpdateSelection(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (selecting) UpdateSelection(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !selecting) return;
            selecting = false;
            // A plain click (no real drag) cancels, like macOS.
            DialogResult = selection.Width >= 4 && selection.Height >= 4 ? DialogResult.OK : DialogResult.Cancel;
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
