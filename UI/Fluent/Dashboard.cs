using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ZidWindowsApps.Fluent;
internal sealed partial class PreviewWindow
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
        int cachePenalty = Math.Min(10, (int)Math.Round(data.Cleanup.Sum(x => x.SizeMb) / 512));
        int startupPenalty = Math.Min(10, data.Startup.Count(x => x.Enabled));
        int health = 100 - 10 - cachePenalty - startupPenalty;
        string condition = health >= 90 ? "Sangat Baik" : "Baik";
        string color = health >= 90 ? "#059669" : "#2563eb";
        double angle = health * 2 * Math.PI / 100;
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
        ring.ToolTip = $"Skor contoh mulai dari 100. Tekanan RAM 88% mengurangi 10 poin, cache {cachePenalty} poin dan auto-start {startupPenalty} poin.";
        var score = Stack(Label(health.ToString(), 28, true), Label("/ 100", 11, false, "Muted"));
        score.VerticalAlignment = VerticalAlignment.Center;
        score.HorizontalAlignment = HorizontalAlignment.Center;
        ring.Children.Add(score);
        var hero = new DockPanel();
        DockPanel.SetDock(ring, Dock.Left);
        hero.Children.Add(ring);
        var scan = ActionButton("Quick Health Scan", () =>
        {
            Navigate("dashboard");
            Notify("Skor contoh " + health + "/100 · RAM −10, cache −" + cachePenalty + ", auto-start −" + startupPenalty + ".");
        }, "primary");
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
        var summary = Stack(Row(pill, Label("Windows 11 25H2 · Perangkat contoh", 12, false, "Muted")), Label("Ringkasan Kesehatan Sistem", 20, true), Label("Memori terpakai 88%. Tersedia " + PreviewData.Size(data.Cleanup.Sum(x => x.SizeMb)) + " file sementara pada data contoh.", 13, false, "Muted"));
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
                content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = percent, Height = 5, Foreground = Paint(tone), Margin = new Thickness(0, 8, 0, 5) });
            }

            content.Children.Add(Label(desc, 12, false, "Muted"));
            var b = ActionButton(title, () => Navigate(route));
            b.Content = content;
            b.Padding = new Thickness(13);
            b.Margin = new Thickness(0, 0, 8, 12);
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalAlignment = VerticalAlignment.Stretch;
            return b;
        }

        var metrics = Columns(4, Tile("Proses Aktif", data.Processes.Count.ToString(), "Pemakaian CPU 24%", "\uE9D9", "#d97706", "processes", true, 24), Tile("Auto-start Aktif", data.Startup.Count(x => x.Enabled).ToString(), "Saat Windows dimulai", "\uE945", "#2563eb", "autostart", true, 100d * data.Startup.Count(x => x.Enabled) / data.Startup.Count), Tile("Penyimpanan", "99,67 GB", "Bebas dari 237,23 GB", "\uEDA2", "#059669", "sysinfo", true, 58), Tile("Sampah & Cache", PreviewData.Size(data.Cleanup.Sum(x => x.SizeMb)), data.Cleanup.Sum(x => x.Files).ToString("N0", PreviewData.Locale) + " file", "\uE74D", "#9333ea", "cleanup", true, 100 * data.Cleanup.Sum(x => x.SizeMb) / 3000));
        var links = Columns(4, Tile("Cleanup", "Periksa Sampah", "Pindai cache dan file sementara.", "\uE74D", "#9333ea", "cleanup", false), Tile("System Info", "Periksa Spesifikasi", "Lihat CPU, RAM dan firmware.", "\uE950", "#2563eb", "sysinfo", false), Tile("Diagnostik", "Troubleshooting", "Periksa jaringan, sistem dan baterai.", "\uE9D9", "#059669", "troubleshooting", false), Tile("Uninstaller", "Aplikasi Terpasang", "Tinjau aplikasi dan sisa instalasi.", "\uE8B7", "#be123c", "uninstaller", false));
        return Scroll(Stack(Card(hero), metrics, Label("Aksi Cepat", 16, true), links, Card(Stack(Label("Mode Aman Aktif", 13, true, "Accent"), Label("Tindakan di pratinjau hanya mengubah data contoh. Anda dapat meninjau konfirmasi sebelum melanjutkan.", 13, false, "Muted")))));
    }
}
