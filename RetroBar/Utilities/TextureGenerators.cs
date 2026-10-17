using System;
using System.Collections.Generic;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: the procedural patterns. Each is periodic on a 64 x 64 lattice, so any
    // setting that changes how often a feature repeats uses whole repeats per tile. A spec's
    // Seed / Octaves / Frequency / Strength change a pattern only where it uses them (Uses);
    // at their defaults every pattern renders exactly as it did before they existed.
    public static class TextureGenerators
    {
        public const int TileSize = 64;

        [Flags]
        public enum Param { None = 0, Seed = 1, Octaves = 2, Frequency = 4, Strength = 8 }

        private static readonly Dictionary<string, (Param uses, int octaves, int frequency)> Table =
            new Dictionary<string, (Param, int, int)>
            {
                ["brushed"] = (Param.Seed | Param.Frequency | Param.Strength, 0, 1),     // frequency: strokes per tile; strength: grain
                ["carbon"] = (Param.Frequency, 0, 8),                                    // frequency: weave blocks per side
                ["noise"] = (Param.Seed | Param.Frequency | Param.Strength, 0, 64),      // frequency: grains per side
                ["scanlines"] = (Param.Frequency, 0, 16),                                // frequency: lines per tile
                ["checker"] = (Param.Frequency, 0, 8),                                   // frequency: squares per side
                ["diamond"] = (Param.Frequency, 0, 4),                                   // frequency: diamonds per side
                ["clouds"] = (Param.Seed | Param.Octaves | Param.Frequency | Param.Strength, 4, 4),
                ["cells"] = (Param.Seed | Param.Frequency | Param.Strength, 0, 6),
                ["marble"] = (Param.Seed | Param.Octaves | Param.Frequency | Param.Strength, 4, 2), // strength: how far veins wander
                ["weave"] = (Param.Seed | Param.Frequency | Param.Strength, 0, 8),       // threads per side; strength: shade variation
                ["bricks"] = (Param.Seed | Param.Frequency | Param.Strength, 0, 8),      // courses per tile; strength: shade variation
                ["dots"] = (Param.Frequency | Param.Strength, 0, 8),                     // dots per side; strength: dot size
                ["waves"] = (Param.Seed | Param.Octaves | Param.Frequency | Param.Strength, 2, 2), // interfering ripples
            };

        public static Param Uses(string name) => Table.TryGetValue(name, out var entry) ? entry.uses : Param.None;

        public static int DefaultOctaves(string name) => Table.TryGetValue(name, out var entry) ? entry.octaves : 0;

        public static int DefaultFrequency(string name) => Table.TryGetValue(name, out var entry) ? entry.frequency : 0;

        public static int Frequency(TextureSpec spec)
        {
            int f = spec.Frequency > 0 ? spec.Frequency : DefaultFrequency(spec.Name);
            // Patterns whose cells alternate need an even count to join up around the tile.
            if (spec.Name == "checker" || spec.Name == "diamond" || spec.Name == "weave" || spec.Name == "bricks" || spec.Name == "carbon")
                f = Math.Max(2, f - f % 2);
            return Math.Max(1, f);
        }

        public static int Octaves(TextureSpec spec) => spec.Octaves > 0 ? spec.Octaves : Math.Max(1, DefaultOctaves(spec.Name));

        // Cache key for the grey tile: only what the pattern really depends on.
        public static string Key(TextureSpec spec)
        {
            Param uses = Uses(spec.Name);
            return spec.Name + ":" +
                (uses.HasFlag(Param.Seed) ? spec.Seed : 0) + ":" +
                (uses.HasFlag(Param.Octaves) ? Octaves(spec) : 0) + ":" +
                (uses.HasFlag(Param.Frequency) ? Frequency(spec) : 0) + ":" +
                (uses.HasFlag(Param.Strength) ? spec.Strength.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "");
        }

        public static byte[] Pattern(TextureSpec spec)
        {
            string name = spec.Name;
            Param uses = Uses(name);
            uint seed = uses.HasFlag(Param.Seed) ? (uint)spec.Seed : 0;
            double strength = uses.HasFlag(Param.Strength) ? spec.Strength : 1;
            int frequency = Frequency(spec), octaves = Octaves(spec);

            var pixels = new byte[TileSize * TileSize * 4];
            double wave(double n, double period) => Math.Cos(2 * Math.PI * n / period);
            double hash(int x, int y, uint salt)
            {
                uint n = unchecked((uint)((x & 63) + (y & 63) * 64) + 0x9e3779b9u + (seed + salt) * 0x85ebca6bu);
                n = unchecked((n ^ (n >> 16)) * 0x7feb352du);
                n = unchecked((n ^ (n >> 15)) * 0x846ca68bu);
                return ((n ^ (n >> 16)) & 65535) / 65535.0;
            }
            double noise(int x, int y) => hash(x, y, 0) - 0.5;
            // Per-cell shade for weave and bricks: the same value for every pixel of a cell.
            double cellShade(int cx, int cy) => (hash(cx * 7 + 1, cy * 13 + 5, 311u) - 0.5) * 2;

            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                {
                    double level;
                    switch (name)
                    {
                        case "brushed":
                            level = 0.50 + 0.16 * wave(y, TileSize / (double)frequency) + 0.23 * strength * noise(0, y) +
                                0.04 * wave(x + y, TileSize) + 0.035 * strength * noise(x, y);
                            break;
                        case "carbon":
                        {
                            int block = x * frequency / TileSize, rowBlock = y * frequency / TileSize;
                            double period = TileSize / (double)frequency;
                            bool across = ((block + rowBlock) & 1) == 0;
                            level = 0.25 + 0.34 * (1 + wave(across ? y : x, period)) / 2 + 0.07 * wave(across ? x : y, period / 2);
                            break;
                        }
                        case "noise":
                            level = 0.5 + 0.8 * strength * noise(x * frequency / TileSize, y * frequency / TileSize);
                            break;
                        case "scanlines": level = (y * frequency) % TileSize < TileSize / 2 ? 0.2 : 0.8; break;
                        case "checker": level = ((x * frequency / TileSize + y * frequency / TileSize) & 1) == 0 ? 0.25 : 0.75; break;
                        case "diamond":
                        {
                            int cx = x * frequency / TileSize, cy = y * frequency / TileSize;
                            double u = 16.0 * (x * frequency / (double)TileSize - cx) - 7.5;
                            double v = 16.0 * (y * frequency / (double)TileSize - cy) - 7.5;
                            if (((cx + cy) & 1) != 0) u = -u;
                            double along = (u + v) / Math.Sqrt(2), cross = (u - v) / Math.Sqrt(2);
                            level = 0.3 + 0.6 * Math.Max(0, 1 - along * along / 36 - cross * cross / 3);
                            break;
                        }
                        // UXUI-Extreme (UX-013): coherent noise, periodic on the tile so it repeats seamlessly.
                        case "clouds": level = 0.5 + 1.3 * strength * CoherentNoise.Fractal(x, y, TileSize, frequency, octaves, seed * 31u); break;
                        case "cells":
                            double pebble = CoherentNoise.Cells(x, y, TileSize, frequency, seed * 31u);
                            level = strength == 1 ? pebble : 0.5 + (pebble - 0.5) * strength;
                            break;
                        case "marble":
                        {
                            double turbulence = CoherentNoise.Turbulence(x, y, TileSize, 4, octaves, seed * 31u);
                            double along = frequency * (double)x / TileSize + Math.Max(1, frequency / 2) * (double)y / TileSize;
                            level = 0.5 + 0.5 * Math.Sin(2 * Math.PI * (along + 1.6 * strength * turbulence));
                            break;
                        }
                        case "weave":
                        {
                            double cell = TileSize / (double)frequency;
                            int cx = (int)(x / cell), cy = (int)(y / cell);
                            double fx = x / cell - cx, fy = y / cell - cy;
                            bool horizontal = ((cx + cy) & 1) == 0;
                            double across = horizontal ? fy : fx, along = horizontal ? fx : fy;
                            double thread = Math.Sin(Math.PI * across);
                            double tuck = 0.75 + 0.25 * Math.Sin(Math.PI * along);
                            level = 0.18 + 0.52 * thread * tuck + 0.07 * strength * cellShade(cx, cy);
                            break;
                        }
                        case "bricks":
                        {
                            double course = TileSize / (double)frequency;           // row height
                            double brick = course * 2;                              // brick length
                            int row = (int)(y / course);
                            double offset = (row & 1) == 0 ? 0 : brick / 2;
                            double bx = (x + offset) % brick;
                            double by = y - row * course;
                            double mortar = Math.Max(1, course / 8);
                            int column = (int)Math.Floor((x + offset) / brick);
                            if (bx < mortar || by < mortar)
                            {
                                level = 0.2;
                            }
                            else
                            {
                                double edge = Math.Min(Math.Min(bx - mortar, brick - bx), Math.Min(by - mortar, course - by));
                                level = 0.55 + 0.1 * Math.Min(1, edge / 2) + 0.12 * strength * cellShade(column & 63, row);
                            }
                            break;
                        }
                        case "dots":
                        {
                            double cell = TileSize / (double)frequency;
                            double dx = (x + 0.5) % cell - cell / 2, dy = (y + 0.5) % cell - cell / 2;
                            double distance = Math.Sqrt(dx * dx + dy * dy) / (cell / 2);
                            double radius = Math.Min(1.1, 0.3 * strength + 0.15);
                            level = 0.9 - 0.7 * Math.Clamp((distance - radius) / 0.2, 0, 1); // light dots on a dark ground
                            break;
                        }
                        case "waves":
                        {
                            double sum = 0, amplitude = 1, total = 0;
                            for (int o = 0; o < octaves; o++)
                            {
                                int k = frequency << o;
                                if (o > 0 && k > TileSize / 2) break;
                                double p1 = hash(o, 1, 17u) * 2 * Math.PI, p2 = hash(o, 2, 17u) * 2 * Math.PI, p3 = hash(o, 3, 17u) * 2 * Math.PI;
                                sum += amplitude * (Math.Cos(2 * Math.PI * k * x / TileSize + p1) + Math.Cos(2 * Math.PI * k * y / TileSize + p2) +
                                                    Math.Cos(2 * Math.PI * k * (x + y) / TileSize + p3)) / 3;
                                total += amplitude;
                                amplitude *= 0.5;
                            }
                            level = 0.5 + 0.5 * strength * sum / Math.Max(total, 1e-9);
                            break;
                        }
                        default: throw new ArgumentException("unknown procedural texture", nameof(spec));
                    }
                    int i = (y * TileSize + x) * 4;
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = (byte)Math.Round(Math.Clamp(level, 0, 1) * 255);
                    pixels[i + 3] = 255;
                }
            return pixels;
        }
    }
}
