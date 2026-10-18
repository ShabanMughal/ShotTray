using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Builds a multi-size Windows .ico from a large PNG.
// Usage: MakeIcon.exe input.png output.ico
// Sizes up to 128 are stored as 32-bit BMP frames (best compatibility), 256 as PNG.
static class MakeIcon
{
    static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: MakeIcon input.png output.ico");
            return 2;
        }

        byte[][] frames = new byte[Sizes.Length][];
        using (var source = new Bitmap(args[0]))
        {
            for (int i = 0; i < Sizes.Length; i++)
                using (Bitmap scaled = Resize(source, Sizes[i]))
                    frames[i] = Sizes[i] >= 256 ? EncodePng(scaled) : EncodeBmp(scaled);
        }

        using (var w = new BinaryWriter(File.Create(args[1])))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte dim = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                w.Write(dim);
                w.Write(dim);
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(frames[i].Length);
                w.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] f in frames) w.Write(f);
        }
        Console.WriteLine("Wrote " + args[1]);
        return 0;
    }

    // Halve repeatedly, then a final step, so small sizes stay crisp.
    static Bitmap Resize(Bitmap source, int size)
    {
        Bitmap current = new Bitmap(source);
        while (current.Width / 2 >= size)
        {
            Bitmap half = Scale(current, current.Width / 2);
            current.Dispose();
            current = half;
        }
        if (current.Width == size) return current;
        Bitmap result = Scale(current, size);
        current.Dispose();
        return result;
    }

    static Bitmap Scale(Bitmap src, int size)
    {
        var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        using (var attrs = new ImageAttributes())
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(src, new Rectangle(0, 0, size, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        return dst;
    }

    static byte[] EncodePng(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    // BITMAPINFOHEADER + bottom-up BGRA pixels + 1-bit AND mask.
    static byte[] EncodeBmp(Bitmap bmp)
    {
        int size = bmp.Width;
        var pixels = new byte[size * size * 4];
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
            Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * size * 4, size * 4);
        bmp.UnlockBits(data);

        int maskStride = ((size + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40);
            w.Write(size);
            w.Write(size * 2);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(0);
            w.Write(size * size * 4 + maskStride * size);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);

            for (int y = size - 1; y >= 0; y--)
                w.Write(pixels, y * size * 4, size * 4);

            for (int y = size - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < size; x++)
                    if (pixels[(y * size + x) * 4 + 3] == 0)
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            return ms.ToArray();
        }
    }
}
