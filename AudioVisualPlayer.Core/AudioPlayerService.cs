using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioVisualPlayer.Core;

public class AudioPlayerService : IDisposable
{
    public const int BarCount = 48;

    private WasapiOut? _output;
    private AudioFileReader? _reader;
    private SpectrumProvider? _spectrum;

    public event Action? PlaybackStopped;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public float Volume
    {
        get => _reader?.Volume ?? 0.8f;
        set { if (_reader != null) _reader.Volume = value; }
    }

    public void Load(string path)
    {
        Stop();

        _reader = new AudioFileReader(path);
        _spectrum = new SpectrumProvider(_reader, BarCount);

        _output = new WasapiOut(AudioClientShareMode.Shared, 100);
        _output.PlaybackStopped += (_, _) => PlaybackStopped?.Invoke();
        _output.Init(_spectrum);
    }

    public void Play() => _output?.Play();
    public void Pause() => _output?.Pause();

    public void TogglePlayPause()
    {
        if (_output == null) return;
        if (_output.PlaybackState == PlaybackState.Playing) Pause();
        else Play();
    }

    public void Seek(TimeSpan position)
    {
        if (_reader != null) _reader.CurrentTime = position;
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _reader?.Dispose();
        _reader = null;
        _spectrum = null;
    }

    public float[] GetSpectrum() => _spectrum?.GetBands() ?? new float[BarCount];

    public void Dispose() => Stop();
}