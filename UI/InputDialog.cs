using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZidWindowsApps;

/// <summary>Dialog sederhana satu kolom teks. Enter = OK, Esc = Batal, fokus langsung ke kolom teks.</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _box = new() { Margin = new Thickness(0, 8, 0, 12), MinWidth = 380 };

    public string Value => _box.Text.Trim();

    public InputDialog(string prompt, Window? owner, string title = "ZidWindowsApps")
    {
        Title = title;
        Owner = owner;
        WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Batal", IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) => DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        panel.Children.Add(_box);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => { _box.Focus(); Keyboard.Focus(_box); };
    }
}
