using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-012): make a fixed-size button image any length without distorting its
    // ends ("3-slice"): keep everything left of a slice column and everything right of it (the
    // rounded tip) as drawn, and repeat the one slice column in between. Works per pixel row,
    // so sprite sheets with states stacked vertically (XP's normal / hover / pressed) keep all
    // their states. Narrower than the original: the middle is cut out instead, tip kept whole.
    public static class SlicedImage
    {
        // sliceAt: where the repeated column is, as a fraction of the width. XP Start buttons are
        // flat there; their left edge and right tip are what must not stretch.
        public static BitmapSource StretchWidth(BitmapSource source, int targetWidth, double sliceAt = 0.45)
        {
            if (source == null || targetWidth <= 0)
            {
                return null;
            }

            BitmapSource bgra = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = bgra.PixelWidth, height = bgra.PixelHeight;
            if (width < 3 || targetWidth == width)
            {
                return source;
            }

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            int slice = Math.Clamp((int)Math.Round(width * sliceAt), 1, width - 2);
            int tip = width - slice - 1; // columns right of the slice column, kept whole

            int outStride = targetWidth * 4;
            byte[] output = new byte[outStride * height];

            for (int y = 0; y < height; y++)
            {
                int src = y * stride, dst = y * outStride;

                if (targetWidth >= width)
                {
                    // left part, then the slice column repeated, then the tip
                    int repeat = targetWidth - slice - tip;
                    Buffer.BlockCopy(pixels, src, output, dst, slice * 4);
                    for (int i = 0; i < repeat; i++)
                    {
                        Buffer.BlockCopy(pixels, src + slice * 4, output, dst + (slice + i) * 4, 4);
                    }
                    Buffer.BlockCopy(pixels, src + (slice + 1) * 4, output, dst + (slice + repeat) * 4, tip * 4);
                }
                else
                {
                    // shorter: keep the tip whole, take what fits of the left part
                    int keepTip = Math.Min(tip, targetWidth);
                    int keepLeft = targetWidth - keepTip;
                    Buffer.BlockCopy(pixels, src, output, dst, keepLeft * 4);
                    Buffer.BlockCopy(pixels, src + (width - keepTip) * 4, output, dst + keepLeft * 4, keepTip * 4);
                }
            }

            var result = BitmapSource.Create(targetWidth, height, bgra.DpiX, bgra.DpiY, bgra.Format, null, output, outStride);
            result.Freeze();
            return result;
        }
    }
}
