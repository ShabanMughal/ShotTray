using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ShotTray
{
    // The floating preview in the bottom-right corner. Drag it out to copy the file,
    // click to mark up, right-click for more. Fades after 6 s; the file stays in the temp folder.
    class ThumbnailForm : Form
    {
        const int LifeMs = 6000;

        readonly Bitmap image;
        readonly Bitmap preview;
        readonly string path;
        readonly Rectangle previewRect;
        readonly Timer lifeTimer = new Timer();
        readonly Timer animTimer = new Timer();
        readonly int restLeft;
        readonly int slideDistance;
        int animStep;
        bool fadingOut, mouseDown, dragging, menuOpen;
        Point downPoint;
        DragGhost ghost;

        public event EventHandler EditRequested;

        public Bitmap Screenshot { get { return image; } }
        public string FilePath { get { return path; } }

        public ThumbnailForm(Bitmap image, string path)
        {
            this.image = image;
            this.path = path;

            float scale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            int pad = (int)(5 * scale);
            int maxW = (int)(220 * scale), maxH = (int)(140 * scale), minSide = (int)(48 * scale);

            float ratio = Math.Min(1f, Math.Min(maxW / (float)image.Width, maxH / (float)image.Height));
            int w = Math.Max(1, (int)Math.Round(image.Width * ratio));
            int h = Math.Max(1, (int)Math.Round(image.Height * ratio));
            int boxW = Math.Max(w, minSide), boxH = Math.Max(h, minSide);
            previewRect = new Rectangle(pad + (boxW - w) / 2, pad + (boxH - h) / 2, w, h);

            preview = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(preview))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, 0, 0, w, h);
            }

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            ClientSize = new Size(boxW + 2 * pad, boxH + 2 * pad);

            Rectangle area = Screen.FromPoint(Control.MousePosition).WorkingArea;
            int margin = (int)(16 * scale);
            restLeft = area.Right - Width - margin;
            slideDistance = (int)(40 * scale);
            Location = new Point(restLeft + slideDistance, area.Bottom - Height - margin);
            Opacity = 0;

            ContextMenuStrip = BuildMenu();

            lifeTimer.Interval = LifeMs;
            lifeTimer.Tick += delegate { lifeTimer.Stop(); FadeOut(); };
            animTimer.Interval = 15;
            animTimer.Tick += OnAnimTick;
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

        ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Edit", null, delegate { RequestEdit(); });
            menu.Items.Add("Open", null, delegate { OpenFile(); CloseSoon(); });
            menu.Items.Add("Copy image", null, delegate { Clip.SetImageAndFile(image, path); });
            menu.Items.Add("Copy path", null, delegate { Clip.SetText(path); });
            menu.Items.Add("Save as...", null, delegate { SaveAs(); });
            menu.Items.Add("Show in folder", null, delegate { ShowInFolder(); CloseSoon(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, delegate { Delete(); });
            menu.Items.Add("Close", null, delegate { CloseSoon(); });
            menu.Opened += delegate { menuOpen = true; lifeTimer.Stop(); };
            menu.Closed += delegate { menuOpen = false; if (!IsDisposed) RestartLife(); };
            return menu;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            animTimer.Start();
            lifeTimer.Start();
        }

        void OnAnimTick(object sender, EventArgs e)
        {
            if (fadingOut)
            {
                Opacity = Math.Max(0, Opacity - 0.08);
                if (Opacity <= 0.01) { animTimer.Stop(); Close(); }
                return;
            }

            // Slide in from the right with an ease-out.
            animStep++;
            double t = Math.Min(1.0, animStep / 12.0);
            double ease = 1 - Math.Pow(1 - t, 3);
            Left = restLeft + (int)Math.Round(slideDistance * (1 - ease));
            Opacity = t;
            if (t >= 1) animTimer.Stop();
        }

        bool MouseInside()
        {
            return ClientRectangle.Contains(PointToClient(Control.MousePosition));
        }

        void RestartLife()
        {
            lifeTimer.Stop();
            lifeTimer.Start();
        }

        void FadeOut()
        {
            if (dragging || menuOpen || MouseInside()) return;
            fadingOut = true;
            animTimer.Start();
        }

        void CancelFade()
        {
            if (!fadingOut) return;
            fadingOut = false;
            animTimer.Stop();
            Left = restLeft;
            Opacity = 1;
        }

        void CloseSoon()
        {
            // Let the click / menu finish before the window goes away.
            BeginInvoke(new Action(Close));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            lifeTimer.Stop();
            CancelFade();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!menuOpen && !dragging) RestartLife();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            mouseDown = true;
            downPoint = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!mouseDown) return;
            Size slop = SystemInformation.DragSize;
            if (Math.Abs(e.X - downPoint.X) > slop.Width / 2 || Math.Abs(e.Y - downPoint.Y) > slop.Height / 2)
            {
                mouseDown = false;
                StartDrag();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !mouseDown) return;
            mouseDown = false;
            RequestEdit();
        }

        void RequestEdit()
        {
            if (EditRequested != null) EditRequested(this, EventArgs.Empty);
            CloseSoon();
        }

        void StartDrag()
        {
            dragging = true;
            lifeTimer.Stop();
            CancelFade();
            animTimer.Stop();
            Left = restLeft;

            // The thumbnail "lifts off": a click-through copy follows the cursor while the
            // real window (the drag source) stays put, invisible.
            var snapshot = new Bitmap(Width, Height);
            DrawToBitmap(snapshot, new Rectangle(Point.Empty, Size));
            ghost = new DragGhost(snapshot);
            MoveGhost();
            ghost.Show();
            Opacity = 0;

            var data = new DataObject();
            data.SetFileDropList(new StringCollection { path });
            // Copy (not move) so the temp copy stays available to `shot last` until cleanup.
            DragDropEffects result = DoDragDrop(data, DragDropEffects.Copy);

            dragging = false;
            if (result != DragDropEffects.None)
            {
                DisposeGhost();
                CloseSoon();
                return;
            }

            SlideGhostBack();
            DisposeGhost();
            Opacity = 1;
            if (!MouseInside()) RestartLife();
        }

        void MoveGhost()
        {
            if (ghost == null) return;
            Point c = Control.MousePosition;
            ghost.Location = new Point(c.X - downPoint.X, c.Y - downPoint.Y);
        }

        // Dropped somewhere that doesn't accept files: glide back to the corner.
        void SlideGhostBack()
        {
            Point from = ghost.Location, to = Location;
            const int steps = 12;
            for (int i = 1; i <= steps; i++)
            {
                double t = 1 - Math.Pow(1 - i / (double)steps, 3);
                ghost.Location = new Point(
                    from.X + (int)Math.Round((to.X - from.X) * t),
                    from.Y + (int)Math.Round((to.Y - from.Y) * t));
                ghost.Update();
                Thread.Sleep(12);
            }
        }

        void DisposeGhost()
        {
            if (ghost == null) return;
            ghost.Close();
            ghost.Dispose();
            ghost = null;
        }

        // Called repeatedly by the drag loop; keeps the ghost under the cursor.
        protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
        {
            base.OnGiveFeedback(e);
            MoveGhost();
        }

        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            base.OnQueryContinueDrag(e);
            MoveGhost();
        }

        void OpenFile()
        {
            try { Process.Start(path); }
            catch (Exception ex) { ShowError(ex); }
        }

        void ShowInFolder()
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { ShowError(ex); }
        }

        void SaveAs()
        {
            lifeTimer.Stop();
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = Path.GetFileName(path);
                dlg.Filter = "PNG image (*.png)|*.png";
                dlg.DefaultExt = "png";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    RestartLife();
                    return;
                }
                try
                {
                    File.Copy(path, dlg.FileName, true);
                    CloseSoon();
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        void Delete()
        {
            try { File.Delete(path); }
            catch (Exception ex) { ShowError(ex); }
            CloseSoon();
        }

        static void ShowError(Exception ex)
        {
            MessageBox.Show(ex.Message, "ShotTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(preview, previewRect.Location);
            using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0)))
            {
                g.DrawRectangle(pen, previewRect.X - 1, previewRect.Y - 1, previewRect.Width + 1, previewRect.Height + 1);
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lifeTimer.Dispose();
                animTimer.Dispose();
                if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
                preview.Dispose();
                image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
