using System;
using NAudio.Wave;

namespace AudioVisualPlayer.Core;

public class SpectrumProvider : ISampleProvider
{
    private const int FftSize = 1024;

    private readonly ISampleProvider _source;
    private readonly int _barCount;
    private readonly float[] _re = new float[FftSize];
    private readonly float[] _im = new float[FftSize];
    private readonly float[] _window = new float[FftSize];
    private readonly float[] _bands;
    private readonly object _lock = new();
    private int _pos;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public SpectrumProvider(ISampleProvider source, int barCount)
    {
        _source = source;
        _barCount = barCount;
        _bands = new float[barCount];

        // Окно Ханна — сглаживает края кадра, чтобы спектр не "размазывался"
        for (int i = 0; i < FftSize; i++)
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1))));
    }

    public float[] GetBands()
    {
        lock (_lock) return (float[])_bands.Clone();
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int i = 0; i < read; i++)
        {
            _re[_pos] = buffer[offset + i] * _window[_pos];
            _im[_pos] = 0;
            if (++_pos == FftSize)
            {
                _pos = 0;
                Compute();
            }
        }
        return read;
    }

    private void Compute()
    {
        Fft(_re, _im);

        int usable = FftSize / 2;
        var raw = new float[_barCount];

        for (int i = 0; i < _barCount; i++)
        {
            // Логарифмическое распределение: басам — узкие полосы, верхам — широкие
            int from = Math.Max(1, (int)Math.Pow(usable, (double)i / _barCount));
            int to = Math.Max(from + 1, (int)Math.Pow(usable, (double)(i + 1) / _barCount));

            double sum = 0;
            for (int j = from; j < to; j++)
                sum += Math.Sqrt(_re[j] * _re[j] + _im[j] * _im[j]);

            double mag = sum / (to - from) * 4.0 / FftSize;
            raw[i] = (float)Math.Min(1.0, Math.Log10(1 + mag * 9));
        }

        lock (_lock)
        {
            for (int i = 0; i < _barCount; i++)
                _bands[i] = Math.Max(raw[i], _bands[i] * 0.85f); // быстрый взлёт, плавное падение
        }
    }

    // Классический итеративный FFT (Cooley–Tukey), без внешних зависимостей
    private static void Fft(float[] re, float[] im)
    {
        int n = re.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len;
            double wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double curR = 1, curI = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = i + k + len / 2;
                    double vr = re[b] * curR - im[b] * curI;
                    double vi = re[b] * curI + im[b] * curR;
                    re[b] = (float)(re[a] - vr);
                    im[b] = (float)(im[a] - vi);
                    re[a] = (float)(re[a] + vr);
                    im[a] = (float)(im[a] + vi);
                    double nextR = curR * wr - curI * wi;
                    curI = curR * wi + curI * wr;
                    curR = nextR;
                }
            }
        }
    }
}