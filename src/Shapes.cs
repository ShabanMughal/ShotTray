using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShotTray
{
    // Markup shapes, stored in image pixel coordinates so the same Draw() is used
    // on screen (with a zoom transform) and when rendering the final PNG.
    abstract class Shape
    {
        public Color Color;
        public float Width;

        public abstract void Draw(Graphics g);

        protected Pen MakePen()
        {
            var pen = new Pen(Color, Width);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }
    }

    // Pen and highlighter.
    class StrokeShape : Shape
    {
        public readonly List<PointF> Points = new List<PointF>();

        public override void Draw(Graphics g)
        {
            if (Points.Count == 1)
            {
                using (var brush = new SolidBrush(Color))
                    g.FillEllipse(brush, Points[0].X - Width / 2, Points[0].Y - Width / 2, Width, Width);
                return;
            }
            using (Pen pen = MakePen())
                g.DrawLines(pen, Points.ToArray());
        }
    }

    class BoxShape : Shape
    {
        public PointF Start, End;
        public bool Ellipse;

        public bool IsEmpty
        {
            get { return Math.Abs(End.X - Start.X) < 2 && Math.Abs(End.Y - Start.Y) < 2; }
        }

        public override void Draw(Graphics g)
        {
            RectangleF r = RectangleF.FromLTRB(
                Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
                Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y));
            using (Pen pen = MakePen())
            {
                if (Ellipse) g.DrawEllipse(pen, r);
                else g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
        }
    }

    class ArrowShape : Shape
    {
        public PointF Start, End;

        public bool IsEmpty
        {
            get
            {
                float dx = End.X - Start.X, dy = End.Y - Start.Y;
                return dx * dx + dy * dy < 9;
            }
        }

        public override void Draw(Graphics g)
        {
            using (Pen pen = MakePen())
            using (var head = new AdjustableArrowCap(3f, 3f, true))
            {
                pen.CustomEndCap = head;
                g.DrawLine(pen, Start, End);
            }
        }
    }

    // Width is the font size in image pixels.
    class TextShape : Shape
    {
        public string Text;
        public PointF Location;

        public override void Draw(Graphics g)
        {
            using (var font = new Font("Segoe UI", Width, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color))
                g.DrawString(Text, font, brush, Location, StringFormat.GenericTypographic);
        }
    }
}
