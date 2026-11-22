using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ZidWindowsApps.Fluent;
internal sealed partial class LiveWindow
{
    private UIElement Dashboard()
    {
        var ring = new Grid
        {
            Width = 102,
            Height = 102,
            Margin = new Thickness(0, 0, 15, 0)
        };
        var track = new Ellipse
        {
            StrokeThickness = 8
        };
        track.SetResourceReference(Shape.StrokeProperty, "Line");
        ring.Children.Add(track);
        double diskFree = capacity > 0 ? 100d * free / capacity : 100;
        int health = 100 - (memory > 85 ? 15 : memory > 70 ? 8 : 0) - (diskFree < 10 ? 20 : diskFree < 20 ? 8 : 0) - (startupCount > 25 ? 10 : startupCount > 15 ? 5 : 0) - (processCount > 250 ? 5 : 0);
        string condition = health >= 90 ? "Sangat Baik" : health >= 70 ? "Baik" : health >= 50 ? "Perlu Perhatian" : "Kritis";
        string color = health >= 90 ? "#059669" : health >= 70 ? "#2563eb" : health >= 50 ? "#b45309" : "#be123c";
        double angle = Math.Min(health, 99.99) * 2 * Math.PI / 100;
        var shape = new StreamGeometry();
        using (var ctx = shape.Open())
        {
            ctx.BeginFigure(new Point(51, 4), false, false);
            ctx.ArcTo(new Point(51 + 47 * Math.Sin(angle), 51 - 47 * Math.Cos(angle)), new Size(47, 47), 0, health > 50, SweepDirection.Clockwise, true, false);
        }

        var arc = new System.Windows.Shapes.Path
        {
            Stroke = Paint(color),
            StrokeThickness = 8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = shape
        };
        ring.Children.Add(arc);
        ring.ToolTip = $"Perkiraan dari RAM {memory}%, ruang bebas {diskFree:0}%, {startupCount} auto-start aktif dan {processCount} proses.";
        var score = Stack(Label(health.ToString(), 28, true), Label("/ 100", 11, false, "Muted"));
        score.VerticalAlignment = VerticalAlignment.Center;
        score.HorizontalAlignment = HorizontalAlignment.Center;
        ring.Children.Add(score);
        var hero = new DockPanel();
        DockPanel.SetDock(ring, Dock.Left);
        hero.Children.Add(ring);
        var scan = ActionButton("Quick Health Scan", RefreshPage, "primary");
        DockPanel.SetDock(scan, Dock.Right);
        hero.Children.Add(scan);
        var pill = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = Paint(color),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var caption = Label("Kondisi Perangkat " + condition, 11, true);
        caption.Foreground = Brushes.White;
        pill.Child = caption;
        var summary = Stack(Row(pill, Label(SystemInfoService.WindowsVersionText() + " · " + Environment.MachineName, 12, false, "Muted")), Label("Ringkasan Kesehatan Sistem", 20, true), Label(ring.ToolTip.ToString()!, 13, false, "Muted"));
        summary.VerticalAlignment = VerticalAlignment.Center;
        hero.Children.Add(summary);
        UIElement Tile(string title, string value, string desc, string glyph, string tone, string route, bool meter, double percent = 0)
        {
            var heading = new DockPanel();
            var icon = Label(glyph, 19);
            icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            icon.Foreground = Paint(tone);
            DockPanel.SetDock(icon, Dock.Right);
            heading.Children.Add(icon);
            heading.Children.Add(Label(title, 12, true, "Muted"));
            var content = Stack(heading, Label(value, meter ? 23 : 15, true));
            if (meter)
            {
                content.Children.Add(new ProgressBar { Style = (Style)FindResource("MetricBar"), Minimum = 0, Maximum = 100, Value = percent, Height = 5, Foreground = Paint(tone), Margin = new Thickness(0, 8, 0, 5) });
            }

            var description = Label(desc, 12, false, "Muted");
            content.Children.Add(description);
            if (route == "processes" && meter)
            {
                var bar = (ProgressBar)content.Children[2];
                updateCpuCard = () => { bar.Value = cpu ?? 0; description.Text = "Pemakaian CPU " + (cpu?.ToString("0") ?? "–") + "%"; };
            }
            var b = ActionButton(title, () => Navigate(route));
            b.Content = content;
            b.Padding = new Thickness(13);
            b.Margin = new Thickness(0, 0, 8, 12);
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalAlignment = VerticalAlignment.Stretch;
            return b;
        }

        UIElement CleanupTile()
        {
            if (scanned)
                return Tile("Potensi Pembersihan", Format.Bytes(cache.Sum(c => c.Size)), cache.Sum(c => c.Files.Count) + " file siap diperiksa", "\uE74D", "#9333ea", "cleanup", false);
            if (lastCleanup is { } last)
                return Tile("Pembersihan Terakhir", Format.Bytes(last.Bytes) + " dibersihkan", last.Files + " file · " + last.When.ToString("d MMM HH:mm") + (last.Scheduled ? " (terjadwal)" : ""), "\uE74D", "#9333ea", "cleanup", false);
            return Tile("Potensi Pembersihan", "Belum dipindai", "Buka Cleanup untuk memindai", "\uE74D", "#9333ea", "cleanup", false);
        }

        var metrics = Columns(4, Tile("Proses Aktif", processCount.ToString(), "Pemakaian CPU " + (cpu?.ToString("0") ?? "–") + "%", "\uE9D9", "#d97706", "processes", true, cpu ?? 0), Tile("Auto-start Aktif", startupCount.ToString(), ready.Contains("autostart") ? "Termasuk layanan Windows" : "Aplikasi saat masuk Windows", "\uE945", "#2563eb", "autostart", true, startupTotal > 0 ? 100d * startupCount / startupTotal : 0), Tile("Ruang Kosong (" + drive.TrimEnd('\\') + ")", Format.Bytes(free), "Bebas dari " + Format.Bytes(capacity), "\uEDA2", "#059669", "sysinfo", true, 100 - diskFree), CleanupTile());
        var links = Columns(4, Tile("Cleanup", "Periksa Sampah", "Pindai cache dan file sementara.", "\uE74D", "#9333ea", "cleanup", false), Tile("System Info", "Periksa Spesifikasi", "Lihat CPU, RAM dan firmware.", "\uE950", "#2563eb", "sysinfo", false), Tile("Diagnostik", "Troubleshooting", "Periksa jaringan, sistem dan baterai.", "\uE9D9", "#059669", "troubleshooting", false), Tile("Uninstaller", "Aplikasi Terpasang", "Tinjau aplikasi dan sisa instalasi.", "\uE8B7", "#be123c", "uninstaller", false));
        return Scroll(Stack(Card(hero), metrics, Label("Aksi Cepat", 16, true), links, Card(Stack(Label("Mode Aman Aktif", 13, true, "Accent"), Label("File pembersihan dipindahkan ke Recycle Bin. Periksa pilihan sebelum mengonfirmasi tindakan.", 13, false, "Muted")))));
    }
}
