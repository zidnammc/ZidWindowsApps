using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZidWindowsApps.Fluent;
internal abstract partial class FluentWindow : Window
{
    private sealed record Route(string Key, string Group, string Label, string Icon, string Title, string Subtitle);
    private static readonly Route[] Routes =
    {
        new("dashboard", "OVERVIEW", "Dashboard", "\uE80F", "Dashboard", "Ringkasan kesehatan perangkat dan kontrol cepat pemeliharaan Windows."),
        new("processes", "OPTIMIZATION", "Processes", "\uE9D9", "Processes", "Pantau konsumsi CPU & memori aplikasi berjalan serta kelola proses responsif."),
        new("autostart", "OPTIMIZATION", "Auto-start", "\uE945", "Auto-start Manager", "Kendalikan aplikasi dan layanan yang berjalan otomatis saat Windows dimulai."),
        new("cleanup", "OPTIMIZATION", "Cleanup", "\uE74D", "Disk & Cache Cleanup", "Bersihkan file sementara sistem dan cache browser secara aman ke Recycle Bin."),
        new("uninstaller", "MANAGEMENT", "Uninstaller", "\uE8B7", "Application Uninstaller", "Hapus aplikasi Desktop maupun Store beserta sisa file dan key registry."),
        new("tools", "MANAGEMENT", "Tools", "\uE90F", "System Tools & Utilities", "Pengaturan rencana daya, pencari file duplikat, dan pintasan sistem Windows."),
        new("sysinfo", "DIAGNOSTICS", "System Info", "\uE950", "System Information", "Inventaris perangkat keras, jaringan, firmware BIOS, dan postur keamanan."),
        new("troubleshooting", "DIAGNOSTICS", "Troubleshooting", "\uE9D9", "Troubleshooting & Diagnostics", "Jalankan utilitas perbaikan jaringan, file sistem (SFC/DISM), dan kesehatan daya."),
        new("settings", "", "Settings", "\uE713", "ZidWindowsApps Settings", "Preferensi bahasa, tema antarmuka, keamanan konfirmasi, dan lokasi cadangan.")
    };
    protected Grid root = null!;
    protected abstract bool Dark { get; set; }
    protected abstract bool Collapsed { get; set; }
    protected abstract bool AdminMode { get; }
    protected abstract void SavePreferences();
    protected abstract void RefreshPage();
    protected abstract void RequestAdmin();
    protected abstract UIElement Page(string key);
    protected abstract string? Badge(string key);
    protected virtual void LeavingPage() { }
    protected virtual void ShowGallery() { }
    protected virtual bool IsPreview => false;
    protected bool IsRoute(string key) => Routes.Any(r => r.Key == key);
    private readonly DispatcherTimer toastTimer = new()
    {
        Interval = TimeSpan.FromSeconds(4)
    };
    protected string current = "dashboard";
    private IInputElement? previousFocus;
    private static T Xaml<T>(string name)
        where T : class
    {
        using var stream = typeof(FluentWindow).Assembly.GetManifestResourceStream("ZidWindowsApps.Fluent." + name) ?? throw new InvalidDataException(name);
        return (T)XamlReader.Load(stream);
    }

    protected void InitializeShell()
    {
        Title = "ZidWindowsApps";
        Width = 1280;
        Height = 760;
        MinWidth = 980;
        MinHeight = 610;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        UseLayoutRounding = true;
        Language = XmlLanguage.GetLanguage("id-ID");
        Resources.MergedDictionaries.Add(Xaml<ResourceDictionary>("Styles.xaml"));
        SetColors();
        root = Xaml<Grid>("Shell.xaml");
        Content = root;
        Get<TextBlock>("VersionLabel").Text = "v" + typeof(FluentWindow).Assembly.GetName().Version!.ToString(3);
        Get<Button>("Collapse").Click += (_, _) =>
        {
            Collapsed = !Collapsed;
            SavePreferences();
            Sidebar();
        };
        Get<Button>("Theme").Click += (_, _) => ToggleTheme();
        Get<Button>("Search").Click += (_, _) => ShowPalette();
        Get<Button>("Refresh").Click += (_, _) => RefreshPage();
        Get<Button>("Elevation").Click += (_, _) => RequestAdmin();
        Get<TextBlock>("Status").Text = "Siap";
        PreviewKeyDown += OnKeys;
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            Get<Border>("Toast").Visibility = Visibility.Collapsed;
        };
        Closed += (_, _) => toastTimer.Stop();
    }

    protected T Get<T>(string name)
        where T : class => (T)root.FindName(name);
    protected void ToggleTheme()
    {
        Dark = !Dark;
        SetColors();
        Navigate(current);
        SavePreferences();
    }

    protected void SetColors()
    {
        void Set(string key, string light, string dark) => Resources[key] = Paint(Dark ? dark : light);
        Set("Chrome", "#eef1f6", "#121418");
        Set("Sidebar", "#f7f9fc", "#14161b");
        Set("Page", "#f3f5f9", "#1c1f26");
        Set("Surface", "#ffffff", "#242832");
        Set("Ink", "#0f172a", "#f1f5f9");
        Set("Muted", "#475569", "#b2bed1");
        Set("Line", "#dce3ed", "#3c4351");
        Set("Subtle", "#f7f9fc", "#1b1e26");
        Set("Selected", "#e5efff", "#243b5e");
        Set("Accent", "#1d4ed8", "#8eb7ff");
        Background = (Brush)Resources["Page"];
    }

    protected static Brush Paint(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    protected void Navigate(string key)
    {
        LeavingPage();
        var r = Routes.First(x => x.Key == key);
        current = key;
        Get<TextBlock>("PageTitle").Text = r.Title;
        Get<TextBlock>("Subtitle").Text = r.Subtitle;
        Get<TextBlock>("Access").Text = AdminMode ? "Administrator" : "Standard User";
        Get<Button>("Elevation").Content = AdminMode ? "Mode standar" : "Jalankan sebagai Admin";
        Get<Button>("Elevation").Visibility = AdminMode && !IsPreview ? Visibility.Collapsed : Visibility.Visible;
        Sidebar();
        Get<ContentControl>("Viewport").Content = Page(key);
    }

    protected virtual string NavLabel(string label) => label;
    protected void Sidebar()
    {
        Get<ColumnDefinition>("Rail").Width = new GridLength(Collapsed ? 68 : 230);
        var nav = Get<StackPanel>("Navigation");
        var pinned = Get<StackPanel>("Pinned");
        nav.Children.Clear();
        pinned.Children.Clear();
        foreach (var group in Routes.GroupBy(r => r.Group))
        {
            var host = group.Key == "" ? pinned : nav;
            if (!Collapsed && group.Key != "")
            {
                var title = Label(group.Key, 10, true, "Muted");
                title.Margin = new Thickness(10, 11, 0, 5);
                host.Children.Add(title);
            }

            foreach (var r in group)
            {
                var row = new DockPanel
                {
                    LastChildFill = false
                };
                var icon = Label(r.Icon, 16);
                icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                row.Children.Add(icon);
                if (!Collapsed)
                {
                    var name = Label(NavLabel(r.Label), 13, current == r.Key);
                    name.Margin = new Thickness(10, 0, 0, 0);
                    row.Children.Add(name);
                    string? count = Badge(r.Key);
                    if (count != null)
                    {
                        var badge = Label(count, 11, true, "Muted");
                        DockPanel.SetDock(badge, Dock.Right);
                        row.Children.Add(badge);
                    }
                }

                var b = ActionButton(r.Label, () => Navigate(r.Key));
                b.Content = row;
                b.ToolTip = NavLabel(r.Label);
                b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                b.Padding = new Thickness(9, 6, 7, 6);
                b.SetResourceReference(BackgroundProperty, current == r.Key ? "Selected" : "Sidebar");
                b.BorderThickness = new Thickness(current == r.Key ? 3 : 0, 0, 0, 0);
                b.SetResourceReference(BorderBrushProperty, "Accent");
                host.Children.Add(b);
            }
        }
    }

    protected void Notify(string message)
    {
        Get<TextBlock>("Status").Text = (IsPreview ? "Pratinjau · " : "") + message;
        Get<TextBlock>("ToastText").Text = message;
        Get<Border>("Toast").Visibility = Visibility.Visible;
        toastTimer.Stop();
        toastTimer.Start();
    }

    protected virtual bool CanDismissOverlay => true;
    private void OnKeys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && CanDismissOverlay && Get<Grid>("Overlay").IsVisible)
        {
            CloseOverlay();
            e.Handled = true;
        }
        else if (IsPreview && e.Key == Key.G && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && !Get<Grid>("Overlay").IsVisible)
        {
            ShowGallery();
            e.Handled = true;
        }
        else if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control && !Get<Grid>("Overlay").IsVisible)
        {
            ShowPalette();
            e.Handled = true;
        }
    }

    protected void OpenOverlay(UIElement child, Control focus)
    {
        previousFocus = Keyboard.FocusedElement;
        var overlay = Get<Grid>("Overlay");
        foreach (UIElement sibling in root.Children)
            if (sibling != overlay)
                sibling.IsEnabled = false;
        var box = Card(child);
        box.Width = 480;
        box.VerticalAlignment = VerticalAlignment.Center;
        box.HorizontalAlignment = HorizontalAlignment.Center;
        KeyboardNavigation.SetTabNavigation(box, KeyboardNavigationMode.Cycle);
        overlay.Children.Add(box);
        overlay.Visibility = Visibility.Visible;
        focus.Focus();
    }

    protected void CloseOverlay()
    {
        var overlay = Get<Grid>("Overlay");
        overlay.Children.Clear();
        overlay.Visibility = Visibility.Collapsed;
        foreach (UIElement sibling in root.Children)
            sibling.IsEnabled = true;
        if (previousFocus != null)
            Keyboard.Focus(previousFocus);
    }

    protected void Ask(string title, string detail, string action, Action confirmed)
    {
        var cancel = ActionButton("Batal", CloseOverlay);
        OpenOverlay(Stack(Label(title, 18, true), Label(detail, 13, false, "Muted"), Row(cancel, ActionButton(action, () =>
        {
            CloseOverlay();
            confirmed();
        }, "primary"))), cancel);
    }

    protected void ShowPalette()
    {
        var query = SearchBox("Cari modul");
        var list = new ListBox
        {
            MaxHeight = 330,
            DisplayMemberPath = "Title"
        };
        void Filter()
        {
            list.ItemsSource = Routes.Where(r => (r.Title + " " + r.Label + " " + r.Subtitle).Contains(query.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
            list.SelectedIndex = 0;
        }

        void Select()
        {
            if (list.SelectedItem is Route r)
            {
                CloseOverlay();
                Navigate(r.Key);
                Get<Button>("Search").Focus();
            }
        }

        query.TextChanged += (_, _) => Filter();
        query.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), -1, list.Items.Count - 1);
                e.Handled = true;
            }

            if (e.Key == Key.Enter)
            {
                Select();
                e.Handled = true;
            }
        };
        list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Select();
                e.Handled = true;
            }
        };
        list.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is ListBoxItem item)
            {
                list.SelectedItem = item.Content;
                Select();
            }
        };
        Filter();
        OpenOverlay(Stack(Label("Pencarian cepat", 16, true), query, list, ActionButton("Tutup", CloseOverlay)), query);
    }
}
