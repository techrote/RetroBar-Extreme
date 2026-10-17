using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: scaling and sharpening for textures, done on the CPU with the classic
    // separable filter kernels (the set ImageMagick documents at
    // https://usage.imagemagick.org/filter/) instead of leaving it to the display's bilinear
    // scaler. Tiles repeat, so every filter wraps around the tile edges and a scaled tile still
    // joins its neighbours without a seam. The same kernels are meant to serve text later.
    public static class TextureResampler
    {
        // "smooth" leaves scaling to WPF, exactly as textures behaved before this existed.
        public const string Smooth = "smooth";

        public static readonly IReadOnlyList<string> Methods = Array.AsReadOnly(new[]
            { Smooth, "nearest", "box", "triangle", "hermite", "gaussian", "mitchell", "catrom", "lanczos" });

        public const int MaxSide = 2048;

        public static bool IsValid(string method) => method != null && Methods.Contains(method);

        private struct Kernel
        {
            public double Support;
            public Func<double, double> Weight;
        }

        private static Kernel Of(string method)
        {
            switch (method)
            {
                case "box": return new Kernel { Support = 0.5, Weight = x => Math.Abs(x) < 0.5 ? 1 : 0 };
                case "triangle": return new Kernel { Support = 1, Weight = x => Math.Max(0, 1 - Math.Abs(x)) };
                case "hermite": return new Kernel { Support = 1, Weight = x => { x = Math.Abs(x); return x < 1 ? (2 * x - 3) * x * x + 1 : 0; } };
                case "gaussian": return new Kernel { Support = 2, Weight = x => Math.Exp(-2 * x * x) };
                case "mitchell": return new Kernel { Support = 2, Weight = x => Cubic(x, 1.0 / 3, 1.0 / 3) };
                case "catrom": return new Kernel { Support = 2, Weight = x => Cubic(x, 0, 0.5) };
                case "lanczos": return new Kernel { Support = 3, Weight = x => Math.Abs(x) < 3 ? Sinc(x) * Sinc(x / 3) : 0 };
                default: throw new ArgumentException("unknown resample method", nameof(method));
            }
        }

        // Mitchell-Netravali family: (B, C) = (1/3, 1/3) Mitchell, (0, 1/2) Catmull-Rom.
        private static double Cubic(double x, double b, double c)
        {
            x = Math.Abs(x);
            if (x < 1) return ((12 - 9 * b - 6 * c) * x * x * x + (-18 + 12 * b + 6 * c) * x * x + (6 - 2 * b)) / 6;
            if (x < 2) return ((-b - 6 * c) * x * x * x + (6 * b + 30 * c) * x * x + (-12 * b - 48 * c) * x + (8 * b + 24 * c)) / 6;
            return 0;
        }

        private static double Sinc(double x) => Math.Abs(x) < 1e-9 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

        // True when a tile of this size, scaled by `scale`, can be resampled here.
        public static bool CanResample(int width, int height, double scale) =>
            (int)Math.Round(width * scale) is int w && (int)Math.Round(height * scale) is int h &&
            w >= 1 && h >= 1 && w <= MaxSide && h <= MaxSide;

        // One channel (0..255 floats), tile-wrapped, scaled to the new size.
        public static float[] Scale(float[] source, int width, int height, int newWidth, int newHeight, string method)
        {
            if (newWidth == width && newHeight == height) return source;
            float[] across = Pass(source, width, height, newWidth, true, method);
            return Pass(across, newWidth, height, newHeight, false, method);
        }

        private static float[] Pass(float[] source, int width, int height, int newSize, bool horizontal, string method)
        {
            int length = horizontal ? width : height;
            int lines = horizontal ? height : width;
            double scale = (double)newSize / length;
            float[] result = horizontal ? new float[newSize * height] : new float[width * newSize];

            // Weights depend only on the output position, so work them out once per pass.
            var starts = new int[newSize];
            var weights = new float[newSize][];
            if (method == "nearest")
            {
                for (int o = 0; o < newSize; o++)
                {
                    starts[o] = Math.Min(length - 1, (int)Math.Floor((o + 0.5) / scale));
                    weights[o] = new[] { 1f };
                }
            }
            else
            {
                Kernel kernel = Of(method);
                double widen = Math.Max(1, 1 / scale); // shrinking: look at proportionally more source pixels
                double radius = kernel.Support * widen;
                for (int o = 0; o < newSize; o++)
                {
                    double centre = (o + 0.5) / scale - 0.5;
                    int first = (int)Math.Floor(centre - radius), last = (int)Math.Ceiling(centre + radius);
                    var w = new float[last - first + 1];
                    double sum = 0;
                    for (int i = first; i <= last; i++)
                    {
                        double weight = kernel.Weight((i - centre) / widen);
                        w[i - first] = (float)weight;
                        sum += weight;
                    }
                    if (Math.Abs(sum) < 1e-9) { w = new[] { 1f }; first = (int)Math.Round(centre); sum = 1; }
                    for (int i = 0; i < w.Length; i++) w[i] = (float)(w[i] / sum);
                    starts[o] = first;
                    weights[o] = w;
                }
            }

            for (int line = 0; line < lines; line++)
            {
                for (int o = 0; o < newSize; o++)
                {
                    float value = 0;
                    float[] w = weights[o];
                    for (int k = 0; k < w.Length; k++)
                    {
                        int index = Wrap(starts[o] + k, length);
                        value += w[k] * (horizontal ? source[line * width + index] : source[index * width + line]);
                    }
                    if (horizontal) result[line * newSize + o] = value;
                    else result[o * width + line] = value;
                }
            }
            return result;
        }

        // Unsharp mask: value + amount * (value - gaussian blur). Wraps at the tile edges.
        public static float[] Sharpen(float[] source, int width, int height, double amount, double radius)
        {
            if (amount <= 0) return source;
            int reach = Math.Max(1, (int)Math.Ceiling(radius * 3));
            var kernel = new float[reach * 2 + 1];
            float total = 0;
            for (int i = -reach; i <= reach; i++)
            {
                kernel[i + reach] = (float)Math.Exp(-(i * i) / (2 * radius * radius));
                total += kernel[i + reach];
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= total;

            float[] blurred = Blur(Blur(source, width, height, kernel, reach, true), width, height, kernel, reach, false);
            var result = new float[source.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (float)(source[i] + amount * (source[i] - blurred[i]));
            return result;
        }

        private static float[] Blur(float[] source, int width, int height, float[] kernel, int reach, bool horizontal)
        {
            var result = new float[source.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int k = -reach; k <= reach; k++)
                    {
                        int sx = horizontal ? Wrap(x + k, width) : x;
                        int sy = horizontal ? y : Wrap(y + k, height);
                        sum += kernel[k + reach] * source[sy * width + sx];
                    }
                    result[y * width + x] = sum;
                }
            return result;
        }

        private static int Wrap(int i, int n) => ((i % n) + n) % n;
    }
}
