using System.Drawing;
using System.Windows.Forms;

namespace ShotTray
{
    // Picture of the thumbnail that follows the cursor during drag & drop.
    // Layered + transparent, so the mouse and the drop "see through" it to the target below.
    class DragGhost : Form
    {
        readonly Bitmap picture;

        public DragGhost(Bitmap picture)
        {
            this.picture = picture;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            Size = picture.Size;
            Opacity = 0.85;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                cp.ExStyle |= 0x08000000 | 0x80000 | 0x20 | 0x80 | 0x8;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(picture, 0, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) picture.Dispose();
            base.Dispose(disposing);
        }
    }
}
