using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: a tiled texture is laid out from the top-left of the element it paints. Themes build
    // a border from nested Borders (the tray's top/bottom edge is the outer one, its left edges are
    // inner ones), so every piece started its tiles at its own corner and the pattern jumped where
    // they meet. Each nested border's brush is moved back by the border's offset inside the outermost
    // one, so the whole frame reads as one continuous piece of texture.
    public static class TextureAlign
    {
        public static void Apply(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is Border nested && VisualTreeHelper.GetParent(nested) is Border outer)
                {
                    Align(nested, Outermost(outer));
                }

                Apply(child);
            }
        }

        private static Border Outermost(Border border)
        {
            while (VisualTreeHelper.GetParent(border) is Border parent)
            {
                border = parent;
            }
            return border;
        }

        private static void Align(Border border, Border outermost)
        {
            // Back to the theme's own brush (it may follow a resource that has changed since).
            if (Shifted.TryGetValue(border, out _))
            {
                border.ClearValue(Border.BorderBrushProperty);
                Shifted.Remove(border);
            }
            if (!(border.BorderBrush is ImageBrush brush) || brush.TileMode != TileMode.Tile || !border.IsVisible || !outermost.IsVisible)
            {
                return;
            }

            Point offset;
            try { offset = border.TranslatePoint(new Point(0, 0), outermost); }
            catch (System.InvalidOperationException) { return; }

            double dx = System.Math.Round(offset.X), dy = System.Math.Round(offset.Y);
            if (dx == 0 && dy == 0)
            {
                return;
            }

            ImageBrush shifted = brush.Clone();
            var group = new TransformGroup();
            if (brush.Transform != null && !brush.Transform.Value.IsIdentity)
            {
                group.Children.Add(brush.Transform.Clone());
            }
            group.Children.Add(new TranslateTransform(-dx, -dy));
            shifted.Transform = group;
            border.BorderBrush = shifted;
            Shifted.Add(border, null);
        }

        // Borders whose brush is ours (so a brush set by anything else is never cleared).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Border, object> Shifted =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Border, object>();
    }
}
