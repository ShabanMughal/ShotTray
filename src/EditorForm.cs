using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace ShotTray
{
    enum Tool { Pen, Highlighter, Arrow, Rectangle, Ellipse, Text }

    // Markup window. Done saves over the screenshot file and copies the result.
    class EditorForm : Form
    {
        static readonly Color[] Palette =
        {
            Color.FromArgb(234, 67, 53), Color.FromArgb(251, 140, 0), Color.FromArgb(253, 216, 53),
            Color.FromArgb(52, 168, 83), Color.FromArgb(66, 133, 244), Color.FromArgb(156, 39, 176),
            Color.Black, Color.White
        };
        static readonly float[] StrokeSizes = { 2f, 4f, 8f };
        static readonly float[] TextSizes = { 16f, 24f, 36f };

        readonly Bitmap image;
        readonly string path;
        readonly float scale;
        readonly List<Shape> shapes = new List<Shape>();
        readonly Stack<Shape> redo = new Stack<Shape>();
        readonly CanvasPanel canvas = new CanvasPanel();
        readonly ToolStrip bar = new ToolStrip();
        readonly Dictionary<Tool, ToolStripButton> toolButtons = new Dictionary<Tool, ToolStripButton>();
        readonly List<ToolStripButton> colorButtons = new List<ToolStripButton>();
        readonly List<ToolStripButton> sizeButtons = new List<ToolStripButton>();
        ToolStripButton undoButton, redoButton;

        Tool tool = Tool.Pen;
        int colorIndex;
        int sizeIndex = 1;
        Shape drawing;
        TextBox textBox;
        PointF textAt;
        float zoom = 1f;
        Point offset;
        Bitmap display;
        Bitmap result;

        public EditorForm(Bitmap image, string path)
        {
            this.image = image;
            this.path = path;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

            Text = "ShotTray - Markup";
            Icon = TrayApp.AppIcon;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size((int)(560 * scale), (int)(360 * scale));

            BuildToolbar();
            canvas.Dock = DockStyle.Fill;
            canvas.BackColor = Color.FromArgb(45, 45, 48);
            canvas.Paint += OnCanvasPaint;
            canvas.Resize += delegate { CommitText(); UpdateLayout(); };
            canvas.MouseDown += OnCanvasMouseDown;
            canvas.MouseMove += OnCanvasMouseMove;
            canvas.MouseUp += OnCanvasMouseUp;
            Controls.Add(canvas);
            Controls.Add(bar);

            // Fit the window to the image, up to 85% of the screen.
            Rectangle area = Screen.FromPoint(Control.MousePosition).WorkingArea;
            int margin = (int)(24 * scale);
            int chromeW = Width - ClientSize.Width, chromeH = Height - ClientSize.Height;
            int w = Math.Max((int)(760 * scale), image.Width + 2 * margin + chromeW);
            int h = Math.Max((int)(480 * scale), image.Height + 2 * margin + bar.PreferredSize.Height + chromeH);
            w = Math.Min(w, (int)(area.Width * 0.85));
            h = Math.Min(h, (int)(area.Height * 0.85));
            Bounds = new Rectangle(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);

            UpdateChecks();
        }

        // The annotated image, if the user pressed Done. Caller takes ownership.
        public Bitmap TakeResult()
        {
            Bitmap r = result;
            result = null;
            return r;
        }

        // ---- toolbar ----

        void BuildToolbar()
        {
            int px = (int)(20 * scale);
            int pad = (int)(5 * scale);
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.ImageScalingSize = new Size(px, px);
            bar.Padding = new Padding(pad, pad, pad, pad);

            AddTool(Tool.Pen, "Pen (P)", px);
            AddTool(Tool.Highlighter, "Highlighter (H)", px);
            AddTool(Tool.Arrow, "Arrow (A)", px);
            AddTool(Tool.Rectangle, "Rectangle (R) - Shift for square", px);
            AddTool(Tool.Ellipse, "Ellipse (E) - Shift for circle", px);
            AddTool(Tool.Text, "Text (T)", px);
            bar.Items.Add(new ToolStripSeparator());

            for (int i = 0; i < Palette.Length; i++)
            {
                int index = i;
                var b = new ToolStripButton(null, ColorIcon(Palette[i], px), delegate { CommitText(); colorIndex = index; UpdateChecks(); });
                b.DisplayStyle = ToolStripItemDisplayStyle.Image;
                b.ToolTipText = "Colour";
                colorButtons.Add(b);
                bar.Items.Add(b);
            }
            bar.Items.Add(new ToolStripSeparator());

            string[] sizeNames = { "Small (1)", "Medium (2)", "Large (3)" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = new ToolStripButton(null, SizeIcon(i, px), delegate { CommitText(); sizeIndex = index; UpdateChecks(); });
                b.DisplayStyle = ToolStripItemDisplayStyle.Image;
                b.ToolTipText = sizeNames[i];
                sizeButtons.Add(b);
                bar.Items.Add(b);
            }

            // Right-aligned items are laid out right to left.
            var done = new ToolStripButton("Done", null, delegate { Done(); });
            done.Font = new Font(bar.Font, FontStyle.Bold);
            done.ToolTipText = "Save and copy (Ctrl+S)";
            AddRight(done);
            AddRight(new ToolStripButton("Save as...", null, delegate { SaveAs(); }));
            var copy = new ToolStripButton("Copy", null, delegate { CopyImage(); });
            copy.ToolTipText = "Copy image (Ctrl+C)";
            AddRight(copy);
            AddRight(new ToolStripSeparator());
            redoButton = new ToolStripButton("Redo", null, delegate { Redo(); });
            redoButton.ToolTipText = "Redo (Ctrl+Y)";
            AddRight(redoButton);
            undoButton = new ToolStripButton("Undo", null, delegate { Undo(); });
            undoButton.ToolTipText = "Undo (Ctrl+Z)";
            AddRight(undoButton);
        }

        void AddTool(Tool t, string tip, int px)
        {
            var b = new ToolStripButton(null, ToolIcon(t, px), delegate { SelectTool(t); });
            b.DisplayStyle = ToolStripItemDisplayStyle.Image;
            b.ToolTipText = tip;
            toolButtons[t] = b;
            bar.Items.Add(b);
        }

        void AddRight(ToolStripItem item)
        {
            item.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(item);
        }

        void SelectTool(Tool t)
        {
            CommitText();
            tool = t;
            UpdateChecks();
        }

        void UpdateChecks()
        {
            foreach (KeyValuePair<Tool, ToolStripButton> kv in toolButtons)
                kv.Value.Checked = kv.Key == tool;
            for (int i = 0; i < colorButtons.Count; i++)
                colorButtons[i].Checked = i == colorIndex;
            for (int i = 0; i < sizeButtons.Count; i++)
                sizeButtons[i].Checked = i == sizeIndex;
            undoButton.Enabled = shapes.Count > 0;
            redoButton.Enabled = redo.Count > 0;
            canvas.Cursor = tool == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        static Bitmap ToolIcon(Tool t, int px)
        {
            var bmp = new Bitmap(px, px);
            float u = px / 20f;
            using (Graphics g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), Math.Max(1.5f, 1.8f * u)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                switch (t)
                {
                    case Tool.Pen:
                        g.DrawBezier(pen, 3 * u, 15 * u, 7 * u, 2 * u, 12 * u, 19 * u, 17 * u, 5 * u);
                        break;
                    case Tool.Highlighter:
                        using (var marker = new Pen(Color.FromArgb(200, 253, 200, 0), 7 * u))
                            g.DrawLine(marker, 4 * u, 10 * u, 16 * u, 10 * u);
                        break;
                    case Tool.Arrow:
                        using (var head = new AdjustableArrowCap(3f, 3f, true))
                        {
                            pen.CustomEndCap = head;
                            g.DrawLine(pen, 4 * u, 16 * u, 15 * u, 5 * u);
                        }
                        break;
                    case Tool.Rectangle:
                        g.DrawRectangle(pen, 3 * u, 5 * u, 14 * u, 10 * u);
                        break;
                    case Tool.Ellipse:
                        g.DrawEllipse(pen, 3 * u, 4 * u, 14 * u, 12 * u);
                        break;
                    case Tool.Text:
                        using (var font = new Font("Segoe UI", 16 * u, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var brush = new SolidBrush(pen.Color))
                        using (var center = new StringFormat())
                        {
                            center.Alignment = StringAlignment.Center;
                            center.LineAlignment = StringAlignment.Center;
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                            g.DrawString("T", font, brush, new RectangleF(0, 0, px, px), center);
                        }
                        break;
                }
            }
            return bmp;
        }

        static Bitmap ColorIcon(Color c, int px)
        {
            var bmp = new Bitmap(px, px);
            float u = px / 20f;
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(c))
            using (var outline = new Pen(Color.FromArgb(120, 0, 0, 0), u))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, 3 * u, 3 * u, 14 * u, 14 * u);
                g.DrawEllipse(outline, 3 * u, 3 * u, 14 * u, 14 * u);
            }
            return bmp;
        }

        static Bitmap SizeIcon(int index, int px)
        {
            var bmp = new Bitmap(px, px);
            float d = px / 20f * new[] { 4f, 8f, 13f }[index];
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, (px - d) / 2, (px - d) / 2, d, d);
            }
            return bmp;
        }

        // ---- canvas ----

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateLayout();
            Activate();
        }

        void UpdateLayout()
        {
            int margin = (int)(24 * scale);
            int availW = Math.Max(1, canvas.ClientSize.Width - 2 * margin);
            int availH = Math.Max(1, canvas.ClientSize.Height - 2 * margin);
            zoom = Math.Min(1f, Math.Min(availW / (float)image.Width, availH / (float)image.Height));
            int dw = Math.Max(1, (int)Math.Round(image.Width * zoom));
            int dh = Math.Max(1, (int)Math.Round(image.Height * zoom));
            offset = new Point((canvas.ClientSize.Width - dw) / 2, (canvas.ClientSize.Height - dh) / 2);

            // Cache a scaled copy so painting while drawing stays fast.
            if (display == null || display.Width != dw || display.Height != dh)
            {
                if (display != null && display != image) display.Dispose();
                if (zoom >= 1f)
                {
                    display = image;
                }
                else
                {
                    display = new Bitmap(dw, dh);
                    using (Graphics g = Graphics.FromImage(display))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(image, 0, 0, dw, dh);
                    }
                }
            }
            canvas.Invalidate();
        }

        void OnCanvasPaint(object sender, PaintEventArgs e)
        {
            if (display == null) return;
            Graphics g = e.Graphics;
            var imageRect = new Rectangle(offset, display.Size);

            using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                g.FillRectangle(shadow, imageRect.X + 3, imageRect.Y + 3, imageRect.Width, imageRect.Height);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(display, imageRect);

            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.SetClip(imageRect);
            g.TranslateTransform(offset.X, offset.Y);
            g.ScaleTransform(zoom, zoom);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            foreach (Shape s in shapes) s.Draw(g);
            if (drawing != null) drawing.Draw(g);
        }

        PointF ToImage(Point p)
        {
            return new PointF((p.X - offset.X) / zoom, (p.Y - offset.Y) / zoom);
        }

        void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (textBox != null)
            {
                CommitText();
                return;
            }

            PointF p = ToImage(e.Location);
            Color c = Palette[colorIndex];
            float w = StrokeSizes[sizeIndex] * scale / zoom;

            switch (tool)
            {
                case Tool.Text:
                    BeginText(e.Location, p);
                    return;
                case Tool.Pen:
                case Tool.Highlighter:
                    var stroke = new StrokeShape();
                    if (tool == Tool.Highlighter)
                    {
                        stroke.Color = Color.FromArgb(110, c);
                        stroke.Width = w * 3.5f;
                    }
                    else
                    {
                        stroke.Color = c;
                        stroke.Width = w;
                    }
                    stroke.Points.Add(p);
                    drawing = stroke;
                    break;
                case Tool.Arrow:
                    var arrow = new ArrowShape();
                    arrow.Color = c;
                    arrow.Width = w;
                    arrow.Start = arrow.End = p;
                    drawing = arrow;
                    break;
                default:
                    var box = new BoxShape();
                    box.Color = c;
                    box.Width = w;
                    box.Ellipse = tool == Tool.Ellipse;
                    box.Start = box.End = p;
                    drawing = box;
                    break;
            }
            canvas.Invalidate();
        }

        void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (drawing == null) return;
            PointF p = ToImage(e.Location);

            var stroke = drawing as StrokeShape;
            var arrow = drawing as ArrowShape;
            var box = drawing as BoxShape;
            if (stroke != null)
            {
                PointF last = stroke.Points[stroke.Points.Count - 1];
                float dx = p.X - last.X, dy = p.Y - last.Y;
                if ((dx * dx + dy * dy) * zoom * zoom < 1) return; // under a screen pixel
                stroke.Points.Add(p);
            }
            else if (arrow != null)
            {
                arrow.End = p;
            }
            else if (box != null)
            {
                if ((ModifierKeys & Keys.Shift) != 0)
                {
                    float dx = p.X - box.Start.X, dy = p.Y - box.Start.Y;
                    float side = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    p = new PointF(box.Start.X + (dx < 0 ? -side : side), box.Start.Y + (dy < 0 ? -side : side));
                }
                box.End = p;
            }
            canvas.Invalidate();
        }

        void OnCanvasMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || drawing == null) return;
            Shape s = drawing;
            drawing = null;

            var box = s as BoxShape;
            var arrow = s as ArrowShape;
            if ((box == null || !box.IsEmpty) && (arrow == null || !arrow.IsEmpty))
                AddShape(s);
            canvas.Invalidate();
        }

        void AddShape(Shape s)
        {
            shapes.Add(s);
            redo.Clear();
            UpdateChecks();
        }

        // ---- text ----

        void BeginText(Point screen, PointF imagePoint)
        {
            Color c = Palette[colorIndex];
            float brightness = (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;

            textAt = imagePoint;
            textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Multiline = true;
            textBox.Font = new Font("Segoe UI", TextSizes[sizeIndex] * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            textBox.ForeColor = c;
            textBox.BackColor = brightness > 0.7f ? Color.FromArgb(60, 60, 60) : Color.White;
            textBox.Location = screen;
            textBox.TextChanged += delegate { FitTextBox(); };
            textBox.KeyDown += OnTextKeyDown;
            textBox.LostFocus += delegate { CommitText(); };
            FitTextBox();
            canvas.Controls.Add(textBox);
            textBox.Focus();
        }

        void FitTextBox()
        {
            Size size = TextRenderer.MeasureText(textBox.Text + "W", textBox.Font);
            textBox.Size = new Size(Math.Max((int)(80 * scale), size.Width), size.Height + 4);
        }

        void OnTextKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                CommitText();
            }
        }

        void CommitText()
        {
            if (textBox == null) return;
            TextBox tb = textBox;
            textBox = null;

            string text = tb.Text.TrimEnd();
            if (text.Length > 0)
            {
                var shape = new TextShape();
                shape.Text = text;
                shape.Location = textAt;
                shape.Color = tb.ForeColor;
                shape.Width = tb.Font.Size / zoom;
                AddShape(shape);
            }
            RemoveTextBox(tb);
        }

        void CancelText()
        {
            if (textBox == null) return;
            TextBox tb = textBox;
            textBox = null;
            RemoveTextBox(tb);
        }

        void RemoveTextBox(TextBox tb)
        {
            canvas.Controls.Remove(tb);
            BeginInvoke(new Action(delegate { tb.Font.Dispose(); tb.Dispose(); }));
            canvas.Invalidate();
        }

        // ---- commands ----

        void Undo()
        {
            CommitText();
            if (shapes.Count == 0) return;
            redo.Push(shapes[shapes.Count - 1]);
            shapes.RemoveAt(shapes.Count - 1);
            UpdateChecks();
            canvas.Invalidate();
        }

        void Redo()
        {
            CommitText();
            if (redo.Count == 0) return;
            shapes.Add(redo.Pop());
            UpdateChecks();
            canvas.Invalidate();
        }

        Bitmap Render()
        {
            var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(image, 0, 0, image.Width, image.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                foreach (Shape s in shapes) s.Draw(g);
            }
            return bmp;
        }

        void Done()
        {
            CommitText();
            if (shapes.Count == 0)
            {
                Close();
                return;
            }

            Bitmap rendered = Render();
            try
            {
                rendered.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                rendered.Dispose();
                ShowError(ex);
                return;
            }
            Clip.SetImageAndFile(rendered, path);
            result = rendered;
            Close();
        }

        void CopyImage()
        {
            CommitText();
            using (Bitmap rendered = Render())
                Clip.SetImage(rendered);
        }

        void SaveAs()
        {
            CommitText();
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = Path.GetFileName(path);
                dlg.Filter = "PNG image (*.png)|*.png";
                dlg.DefaultExt = "png";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Bitmap rendered = Render())
                        rendered.Save(dlg.FileName, ImageFormat.Png);
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        void ShowError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ShotTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (textBox != null)
            {
                if (keyData == Keys.Escape)
                {
                    CancelText();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            switch (keyData)
            {
                case Keys.Control | Keys.Z: Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: Redo(); return true;
                case Keys.Control | Keys.S:
                case Keys.Enter: Done(); return true;
                case Keys.Control | Keys.C: CopyImage(); return true;
                case Keys.Escape: Close(); return true;
                case Keys.P: SelectTool(Tool.Pen); return true;
                case Keys.H: SelectTool(Tool.Highlighter); return true;
                case Keys.A: SelectTool(Tool.Arrow); return true;
                case Keys.R: SelectTool(Tool.Rectangle); return true;
                case Keys.E:
                case Keys.O: SelectTool(Tool.Ellipse); return true;
                case Keys.T: SelectTool(Tool.Text); return true;
                case Keys.D1: sizeIndex = 0; UpdateChecks(); return true;
                case Keys.D2: sizeIndex = 1; UpdateChecks(); return true;
                case Keys.D3: sizeIndex = 2; UpdateChecks(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitText();
            if (result == null && shapes.Count > 0 && e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult answer = MessageBox.Show(this, "Discard your markup?", "ShotTray",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (display != null && display != image) display.Dispose();
                image.Dispose();
                if (result != null) result.Dispose();
            }
            base.Dispose(disposing);
        }

        class CanvasPanel : Panel
        {
            public CanvasPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }
    }
}
