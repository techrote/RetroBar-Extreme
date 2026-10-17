using System;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-013): coherent (smooth, natural-looking) noise for procedural textures.
    // Everything is periodic over the tile, so tiles repeat with no seam: lattice coordinates
    // wrap at the tile's cell count, and cell distances are measured across the wrap.
    // Deterministic (fixed hash), so the same pattern renders identically every time.
    public static class CoherentNoise
    {
        // Gradient (Perlin-style) noise at pixel (x, y) on a tile of `size` px divided into
        // `cells` lattice cells per side. Roughly -0.5..0.5.
        public static double Gradient(double x, double y, int size, int cells, uint seed = 0)
        {
            double fx = x * cells / size, fy = y * cells / size;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;

            double dot(int ix, int iy, double dx, double dy)
            {
                double angle = Hash(Wrap(ix, cells), Wrap(iy, cells), seed) * 2 * Math.PI;
                return Math.Cos(angle) * dx + Math.Sin(angle) * dy;
            }

            double n00 = dot(x0, y0, tx, ty), n10 = dot(x0 + 1, y0, tx - 1, ty);
            double n01 = dot(x0, y0 + 1, tx, ty - 1), n11 = dot(x0 + 1, y0 + 1, tx - 1, ty - 1);
            double u = Fade(tx), v = Fade(ty);
            return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v) * 0.7;
        }

        // Several octaves of gradient noise, each with twice the cells and half the strength
        // (cell counts stay whole divisors of the tile, so every octave still tiles).
        public static double Fractal(double x, double y, int size, int baseCells, int octaves)
        {
            double sum = 0, amplitude = 1, total = 0;
            int cells = baseCells;
            for (int o = 0; o < octaves && cells <= size; o++)
            {
                sum += Gradient(x, y, size, cells, (uint)o * 7919u) * amplitude;
                total += amplitude;
                amplitude *= 0.5;
                cells *= 2;
            }
            return sum / total;
        }

        // |noise| summed over octaves: the "turbulence" that bends marble veins. Roughly 0..0.5.
        public static double Turbulence(double x, double y, int size, int baseCells, int octaves)
        {
            double sum = 0, amplitude = 1, total = 0;
            int cells = baseCells;
            for (int o = 0; o < octaves && cells <= size; o++)
            {
                sum += Math.Abs(Gradient(x, y, size, cells, 104729u + (uint)o * 7919u)) * amplitude;
                total += amplitude;
                amplitude *= 0.5;
                cells *= 2;
            }
            return sum / total;
        }

        // Cellular (Worley) noise: one feature point per cell; the level is the distance to the
        // nearest point, so cells read as smooth pebbles with dark centres and light edges. 0..1.
        public static double Cells(double x, double y, int size, int cells)
        {
            double cell = (double)size / cells;
            int cx = (int)Math.Floor(x / cell), cy = (int)Math.Floor(y / cell);
            double nearest = double.MaxValue, second = double.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx, iy = cy + dy;
                    int wx = Wrap(ix, cells), wy = Wrap(iy, cells);
                    // Feature point inside the (unwrapped) neighbour cell, so distances cross the seam.
                    double px = (ix + 0.15 + 0.7 * Hash(wx, wy, 31u)) * cell;
                    double py = (iy + 0.15 + 0.7 * Hash(wx, wy, 977u)) * cell;
                    double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < nearest)
                    {
                        second = nearest;
                        nearest = d;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }

            // Mix of F1 (pebble shading) and F2 - F1 (edges), normalised by the cell size.
            double f1 = nearest / cell, edge = (second - nearest) / cell;
            return Math.Clamp(0.15 + 0.75 * f1 - 0.35 * Math.Max(0, 0.25 - edge) * 4, 0, 1);
        }

        private static int Wrap(int i, int n) => ((i % n) + n) % n;

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // 0..1, deterministic per lattice point.
        private static double Hash(int x, int y, uint seed)
        {
            uint n = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u ^ (seed * 83492791u) ^ 0x9e3779b9u);
            n = unchecked((n ^ (n >> 16)) * 0x7feb352du);
            n = unchecked((n ^ (n >> 15)) * 0x846ca68bu);
            return ((n ^ (n >> 16)) & 0xffffff) / (double)0x1000000;
        }
    }
}
