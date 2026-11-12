using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AudioVisualPlayer.App.Themes;

namespace AudioVisualPlayer.App.Controls;

public class SpectrumVisualizer : Control
{
    private float[] _source = new float[48];
    private float[] _levels = Array.Empty<float>();
    private float[] _peaks = Array.Empty<float>();
    private float[] _peakVel = Array.Empty<float>();
    private double _t;

    public int BarCount { get; set; } = 48;
    public double Gain { get; set; } = 1.2;
    public double Decay { get; set; } = 0.88;
    public double Glow { get; set; } = 0.6;
    public bool Mirror { get; set; }
    public bool ShowPeaks { get; set; } = true;
    public bool Rounded { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public int PaletteMode { get; set; }
    public bool IsLive { get; set; }

    public void PushBands(float[] bands) => _source = bands;

    public void Tick(double dt)
    {
        _t += dt;
        if (_levels.Length != BarCount)
        {
            _levels = new float[BarCount];
            _peaks = new float[BarCount];
            _peakVel = new float[BarCount];
        }

        for (int i = 0; i < BarCount; i++)
        {
            double target;
            if (IsLive)
            {
                double pos = (double)i * (_source.Length - 1) / Math.Max(1, BarCount - 1);
                int a = (int)pos;
                int b = Math.Min(_source.Length - 1, a + 1);
                target = Math.Clamp((_source[a] + (_source[b] - _source[a]) * (pos - a)) * Gain, 0, 1);
            }
            else
            {
                target = Math.Max(0.02, 0.10 + 0.06 * Math.Sin(_t * 1.7 + i * 0.32) + 0.04 * Math.Sin(_t * 0.9 + i * 0.11));
            }

            _levels[i] = (float)Math.Max(target, _levels[i] * Decay);

            if (_levels[i] >= _peaks[i]) { _peaks[i] = _levels[i]; _peakVel[i] = 0; }
            else { _peakVel[i] += (float)(2.2 * dt); _peaks[i] = Math.Max(_levels[i], _peaks[i] - _peakVel[i] * (float)dt); }
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 10 || h < 10 || _levels.Length == 0) return;
        var th = ThemeManager.Current;

        var bgGlow = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, th.AccentColor.R, th.AccentColor.G, th.AccentColor.B), 0),
                new GradientStop(Color.FromArgb(30, th.AccentColor.R, th.AccentColor.G, th.AccentColor.B), 1),
            }
        };
        context.DrawRectangle(bgGlow, null, new Rect(0, 0, w, h));

        if (ShowGrid)
        {
            var gp = new Pen(new SolidColorBrush(Color.FromArgb(60, th.BorderColor.R, th.BorderColor.G, th.BorderColor.B)), 1);
            foreach (var f in new[] { 0.25, 0.5, 0.75 })
                context.DrawLine(gp, new Point(0, h * f), new Point(w, h * f));
        }

        double step = w / BarCount, bw = step * 0.62;
        float radius = Rounded ? (float)Math.Min(4, bw / 2) : 0;
        double mid = Mirror ? h / 2 : h;

        var grad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(th.AccentColor, 0), new GradientStop(th.Accent2Color, 1) }
        };

        for (int i = 0; i < BarCount; i++)
        {
            double lvl = _levels[i];
            double bh = Math.Max(2, lvl * (Mirror ? h / 2 - 6 : h - 8));
            double x = i * step + (step - bw) / 2;
            double y = Mirror ? mid - bh : h - bh;
            var spec = Hsv((double)i / BarCount * 0.85, 0.85, 1);

            IBrush fill = PaletteMode switch
            {
                1 => new SolidColorBrush(spec),
                2 => new SolidColorBrush(th.AccentColor),
                _ => grad
            };

            if (Glow > 0.02)
            {
                var gc = PaletteMode == 1 ? spec : th.AccentColor;
                var gb = new SolidColorBrush(Color.FromArgb((byte)(Glow * 80 * lvl), gc.R, gc.G, gc.B));
                double gh = Mirror ? bh * 2 + 8 : bh + 8;
                double gy = Mirror ? mid - bh - 4 : h - bh - 4;
                context.DrawRectangle(gb, null, new Rect(x - 3, gy, bw + 6, gh), radius + 3, radius + 3);
            }

            context.DrawRectangle(fill, null, new Rect(x, y, bw, bh), radius, radius);
            if (Mirror) context.DrawRectangle(fill, null, new Rect(x, mid + 1, bw, bh), radius, radius);

            if (ShowPeaks && _peaks[i] > 0.02)
            {
                double ph = _peaks[i] * (Mirror ? h / 2 - 6 : h - 8);
                var pb = new SolidColorBrush(th.Accent2Color);
                context.DrawRectangle(pb, null, new Rect(x, (Mirror ? mid - ph : h - ph) - 4, bw, 2), 1, 1);
                if (Mirror) context.DrawRectangle(pb, null, new Rect(x, mid + ph + 2, bw, 2), 1, 1);
            }
        }

        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(140, th.AccentColor.R, th.AccentColor.G, th.AccentColor.B)), 1),
            new Point(0, mid - 0.5), new Point(w, mid - 0.5));
    }

    private static Color Hsv(double h, double s, double v)
    {
        int i = (int)(h * 6);
        double f = h * 6 - i, p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        (double r, double g, double b) = (i % 6) switch
        {
            0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t),
            3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q)
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}