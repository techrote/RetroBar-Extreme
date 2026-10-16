using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-009): the floating palette. A small tool window beside Properties with 12
    // colour slots (swatch, hex, copy, paste) that stays put while you switch parts or tabs, so
    // a set of colours can be carried between parts. Saved with the style.
    public class PaletteWindow : Window
    {
        private readonly ColourField[] _fields = new ColourField[StyleDocument.PALETTE_SIZE];
        private bool _updating;

        // Slot index and new colour (null = cleared).
        public event Action<int, Color?> SlotChanged;

        public PaletteWindow(Window owner, Color?[] palette)
        {
            Owner = owner;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            ShowActivated = false;
            SetResourceReference(TitleProperty, "uxui_palette_title");
            SetResourceReference(StyleProperty, "PropertiesWindow");
            FlowDirection = owner.FlowDirection;

            var grid = new Grid { Margin = new Thickness(8) };
            const int columns = 2, rows = StyleDocument.PALETTE_SIZE / columns;
            for (int c = 0; c < columns; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            for (int r = 0; r < rows; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < StyleDocument.PALETTE_SIZE; i++)
            {
                int slot = i;
                int column = (i / rows) * 2, row = i % rows;

                var number = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Width = 18,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(column == 0 ? 0 : 10, 0, 4, 0),
                };
                Grid.SetColumn(number, column);
                Grid.SetRow(number, row);
                grid.Children.Add(number);

                var field = new ColourField(palette[i], allowEmpty: true);
                field.Changed += (s, e) =>
                {
                    if (!_updating)
                    {
                        SlotChanged?.Invoke(slot, field.Value);
                    }
                };
                Grid.SetColumn(field, column + 1);
                Grid.SetRow(field, row);
                grid.Children.Add(field);
                _fields[i] = field;
            }

            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(grid);
            var note = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(8, 0, 8, 8) };
            note.SetResourceReference(TextBlock.TextProperty, "uxui_palette_note");
            panel.Children.Add(note);
            Content = panel;

            Loaded += (s, e) =>
            {
                // Beside Properties, on whichever side has room.
                double right = Owner.Left + Owner.ActualWidth + 4;
                Left = right + ActualWidth <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ? right : Math.Max(SystemParameters.VirtualScreenLeft, Owner.Left - ActualWidth - 4);
                Top = Owner.Top;
            };
        }

        // When the style changes underneath (load, import, reset).
        public void ShowPalette(Color?[] palette)
        {
            _updating = true;
            for (int i = 0; i < _fields.Length; i++)
            {
                _fields[i].Value = palette[i];
            }
            _updating = false;
        }
    }
}
