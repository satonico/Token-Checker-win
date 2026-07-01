using System;
using System.Windows;
using System.Windows.Media;
using WpfBrush  = System.Windows.Media.Brush;
using WpfColor  = System.Windows.Media.Color;
using WpfPen    = System.Windows.Media.Pen;
using WpfPoint  = System.Windows.Point;
using WpfSize   = System.Windows.Size;

namespace TokenChecker.Controls;

/// 円形プログレスバー（ドーナツ型）。
/// 中央のマーク（✦ / ▶）は XAML 側でオーバーレイする。
public class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(DonutChart),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static readonly SolidColorBrush GreenBrush  = FreezeBrush(0x4C, 0xAF, 0x50);
    private static readonly SolidColorBrush YellowBrush = FreezeBrush(0xFF, 0xC1, 0x07);
    private static readonly SolidColorBrush RedBrush    = FreezeBrush(0xF4, 0x43, 0x36);
    private static readonly SolidColorBrush TrackBrush  = FreezeBrushArgb(0x40, 0xFF, 0xFF, 0xFF);

    // Pen を静的フィールドに固定し OnRender ごとの GC 負荷を排除する。
    private const double LineWidth = 2.5;
    private static readonly WpfPen TrackPen  = FreezePen(TrackBrush,  LineWidth);
    private static readonly WpfPen GreenPen  = FreezePen(GreenBrush,  LineWidth);
    private static readonly WpfPen YellowPen = FreezePen(YellowBrush, LineWidth);
    private static readonly WpfPen RedPen    = FreezePen(RedBrush,    LineWidth);

    private static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(WpfColor.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }

    private static SolidColorBrush FreezeBrushArgb(byte a, byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        b2.Freeze();
        return b2;
    }

    private static WpfPen FreezePen(WpfBrush brush, double thickness)
    {
        var p = new WpfPen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
        };
        p.Freeze();
        return p;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w  = ActualWidth;
        var h  = ActualHeight;
        var cx = w / 2;
        var cy = h / 2;
        var r  = (Math.Min(w, h) - LineWidth) / 2;
        if (r <= 0) return;

        dc.DrawEllipse(null, TrackPen, new WpfPoint(cx, cy), r, r);

        var v = Math.Clamp(Value, 0.0, 1.0);
        if (v <= 0) return;

        var fillPen = v < 0.75 ? GreenPen : v < 0.90 ? YellowPen : RedPen;

        if (v >= 1.0)
        {
            dc.DrawEllipse(null, fillPen, new WpfPoint(cx, cy), r, r);
            return;
        }

        const double startAngle = -Math.PI / 2;
        var endAngle = startAngle + v * 2 * Math.PI;
        var sx = cx + r * Math.Cos(startAngle);
        var sy = cy + r * Math.Sin(startAngle);
        var ex = cx + r * Math.Cos(endAngle);
        var ey = cy + r * Math.Sin(endAngle);

        var arc = new ArcSegment(
            new WpfPoint(ex, ey),
            new WpfSize(r, r),
            rotationAngle: 0,
            isLargeArc: v > 0.5,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true);
        var fig = new PathFigure(new WpfPoint(sx, sy), new[] { arc }, closed: false);
        var geo = new PathGeometry(new[] { fig });
        geo.Freeze();
        dc.DrawGeometry(null, fillPen, geo);
    }
}
