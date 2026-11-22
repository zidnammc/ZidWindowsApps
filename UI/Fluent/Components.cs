using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ZidWindowsApps.Fluent;
internal abstract partial class FluentWindow
{
    protected WrapPanel Segments(IEnumerable<string> values, string selected, Action<string> select)
    {
        var row = new WrapPanel();
        foreach (var value in values)
        {
            var b = ActionButton(value, () =>
            {
                select(value);
                foreach (Button item in row.Children)
                    item.SetResourceReference(BackgroundProperty, Equals(item.Content, value) ? "Selected" : "Surface");
            });
            b.SetResourceReference(BackgroundProperty, value == selected ? "Selected" : "Surface");
            row.Children.Add(b);
        }

        return row;
    }

    protected TextBlock Label(string text, double size = 13, bool bold = false, string color = "Ink")
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        t.SetResourceReference(TextBlock.ForegroundProperty, color);
        return t;
    }

    protected Button ActionButton(string title, Action action, string kind = "")
    {
        var b = new Button
        {
            Content = title,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(b, title);
        b.Click += (_, _) => action();
        if (kind is "primary" or "danger")
        {
            b.Background = Paint(kind == "danger" ? "#be123c" : "#2563eb");
            b.Foreground = Brushes.White;
        }

        return b;
    }

    protected Border Card(UIElement child)
    {
        if (child is DataGrid table)
        {
            var empty = Label("Tidak ada item yang cocok.", 14, false, "Muted");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.IsHitTestVisible = false;
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
            var trigger = new DataTrigger
            {
                Binding = new Binding("HasItems")
                {
                    Source = table
                },
                Value = false
            };
            trigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible));
            style.Triggers.Add(trigger);
            empty.Style = style;
            var panel = new Grid();
            panel.Children.Add(table);
            panel.Children.Add(empty);
            child = panel;
        }

        var c = new Border
        {
            Child = child
        };
        c.SetResourceReference(StyleProperty, "Card");
        return c;
    }

    protected static StackPanel Stack(params UIElement[] items)
    {
        var s = new StackPanel();
        foreach (var item in items)
        {
            if (s.Children.Count > 0 && item is FrameworkElement e && e.Margin == new Thickness(0))
                e.Margin = new Thickness(0, 8, 0, 0);
            s.Children.Add(item);
        }

        return s;
    }

    protected static WrapPanel Row(params UIElement[] items)
    {
        var p = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var i in items)
        {
            // Label tanpa margin menempel pada tombol/segmen di sebelahnya.
            if (i is TextBlock t && t.Margin == new Thickness(0))
                t.Margin = new Thickness(0, 0, 10, 0);
            p.Children.Add(i);
        }
        return p;
    }

    protected static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
    };
    protected static UniformGrid Columns(int columns, params UIElement[] items)
    {
        var p = new UniformGrid
        {
            Columns = columns
        };
        foreach (var i in items)
            p.Children.Add(i);
        return p;
    }

    protected static DockPanel Framed(UIElement top, UIElement middle, UIElement? bottom = null)
    {
        var p = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        p.Children.Add(top);
        if (bottom != null)
        {
            DockPanel.SetDock(bottom, Dock.Bottom);
            p.Children.Add(bottom);
        }

        p.Children.Add(middle);
        return p;
    }

    protected static Grid Split(UIElement left, UIElement right, double first = 7, double second = 5)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(first, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(second, GridUnitType.Star) });
        g.Children.Add(left);
        Grid.SetColumn(right, 1);
        g.Children.Add(right);
        return g;
    }

    protected static TextBox SearchBox(string title, string value = "")
    {
        var t = new TextBox
        {
            Text = value,
            MinWidth = 160,
            ToolTip = title,
            Margin = new Thickness(2)
        };
        AutomationProperties.SetName(t, title);
        return t;
    }

    protected static CheckBox Check(string title, bool value, Action<bool> change)
    {
        var c = new CheckBox
        {
            Content = title,
            IsChecked = value
        };
        if (title.Length > 0)
            c.SetResourceReference(StyleProperty, "Switch");
        c.Click += (_, _) => change(c.IsChecked == true);
        return c;
    }

    protected static DataGrid Table(string name, params (string Caption, string Field, double Width)[] fields)
    {
        var grid = new DataGrid();
        AutomationProperties.SetName(grid, name);
        foreach (var f in fields)
            grid.Columns.Add(new DataGridTextColumn { Header = f.Caption, Binding = new Binding(f.Field), Width = f.Width < 0 ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(f.Width), MinWidth = 50 });
        return grid;
    }

    protected static UIElement SearchField(string title, TextBox box) => Row(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3) }, box);
    protected Button Unavailable(string title)
    {
        var b = new Button
        {
            Content = title + " · Belum tersedia",
            IsEnabled = false,
            ToolTip = "Belum tersedia dalam pratinjau"
        };
        ToolTipService.SetShowOnDisabled(b, true);
        return b;
    }

    protected static bool Matches(string text, string query) => text.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
