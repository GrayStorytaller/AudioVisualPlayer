using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AudioVisualPlayer.App.Themes;
using AudioVisualPlayer.Core;
using Avalonia;
using Avalonia.Input;

namespace AudioVisualPlayer.App;

public class AppSettings
{
    public string Theme { get; set; } = "Noir Pulse";
    public string? Accent { get; set; }
    public string Folder { get; set; } = @"F:\Музыка";
    public List<string> RecentFolders { get; set; } = new();
    public int Bars { get; set; } = 48;
    public double Gain { get; set; } = 1.2;
    public double Decay { get; set; } = 0.88;
    public double Glow { get; set; } = 0.6;
    public bool Mirror { get; set; }
    public bool Peaks { get; set; } = true;
    public bool Rounded { get; set; } = true;
    public bool Grid { get; set; } = true;
    public int Palette { get; set; }
    public double Volume { get; set; } = 0.8;
    public bool Shuffle { get; set; }
    public int Repeat { get; set; }
}

public partial class MainWindow : Window
{
    private enum RepeatMode { Off, All, One }

    private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    private readonly AudioPlayerService _service = new();
    private readonly ObservableCollection<TrackInfo> _view = new();
    private readonly DispatcherTimer _timer;
    private readonly Random _rnd = new();
    private readonly List<TrackInfo> _history = new();
    private List<TrackInfo> _all = new();
    private List<string> _recent = new();
    private TrackInfo? _current;
    private AppSettings _settings = new();
    private string _folder = @"F:\Музыка";
    private bool _dragSeek, _updatingUi, _shuffle;
    private RepeatMode _repeatMode;

    public MainWindow()
    {
        InitializeComponent();
        var icon = Branding.LoadIcon();
        if (icon is not null) Icon = icon;

        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        TitleBar.DoubleTapped += (_, _) => ToggleMaximize();
        MinBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxBtn.Click += (_, _) => ToggleMaximize();
        CloseBtn.Click += (_, _) => Close();
        TrackList.ItemsSource = _view;

        SearchBox.TextChanged += (_, _) => ApplyFilter();
        SortBox.SelectionChanged += (_, _) => ApplyFilter();
        TrackList.DoubleTapped += (_, _) => PlaySelected();
        FolderBtn.Click += async (_, _) => await OpenFolderDialogAsync();
        ThemeBox.SelectionChanged += (_, _) => { if (!_updatingUi && ThemeBox.SelectedItem is string n) ThemeManager.Apply(n); };
        SaveThemeBtn.Click += (_, _) =>
        {
            var name = ThemeManager.SaveCurrent();
            RefreshThemeBox(name);
            StatusLabel.Text = $"Скин «{name}» сохранён в themes/.";
        };

        PrevBtn.Click += (_, _) => Step(-1);
        NextBtn.Click += (_, _) => Step(1);
        StopBtn.Click += (_, _) => { _service.Stop(); Viz.IsLive = false; };
        PlayBtn.Click += (_, _) =>
        {
            if (_current == null) PlaySelected();
            else _service.TogglePlayPause();
        };
        ShuffleBtn.Click += (_, _) =>
        {
            _shuffle = !_shuffle;
            if (_shuffle && _current != null) { _history.Clear(); _history.Add(_current); }
            UpdateModeButtons();
            StatusLabel.Text = _shuffle ? "Случайный порядок включён." : "Случайный порядок выключен.";
        };
        RepeatBtn.Click += (_, _) =>
        {
            _repeatMode = (RepeatMode)(((int)_repeatMode + 1) % 3);
            UpdateModeButtons();
            StatusLabel.Text = _repeatMode switch
            {
                RepeatMode.All => "Повтор всего плейлиста включён.",
                RepeatMode.One => "Повтор одного трека включён.",
                _ => "Повтор выключен."
            };
        };

        SeekSlider.AddHandler(PointerPressedEvent, (_, _) => _dragSeek = true, RoutingStrategies.Tunnel, true);
        SeekSlider.AddHandler(PointerReleasedEvent, (_, _) => _dragSeek = false, RoutingStrategies.Tunnel, true);
        SeekSlider.ValueChanged += (_, e) =>
        {
            if (_dragSeek && _service.Duration > TimeSpan.Zero)
                _service.Seek(TimeSpan.FromSeconds(_service.Duration.TotalSeconds * e.NewValue / 1000));
        };
        VolumeSlider.ValueChanged += (_, e) => _service.Volume = (float)e.NewValue;

        PaletteBox.SelectionChanged += (_, _) => Viz.PaletteMode = PaletteBox.SelectedIndex;
        BarsSlider.ValueChanged += (_, e) => { Viz.BarCount = (int)e.NewValue; BarsVal.Text = $"{(int)e.NewValue}"; };
        GainSlider.ValueChanged += (_, e) => { Viz.Gain = e.NewValue; GainVal.Text = $"{e.NewValue:0.0}×"; };
        DecaySlider.ValueChanged += (_, e) => { Viz.Decay = e.NewValue; DecayVal.Text = $"{e.NewValue:0.00}"; };
        GlowSlider.ValueChanged += (_, e) => { Viz.Glow = e.NewValue; GlowVal.Text = $"{e.NewValue * 100:0}%"; };
        ResetLabBtn.Click += (_, _) => ApplyLab(new AppSettings());
        RandomLabBtn.Click += (_, _) => ApplyLab(new AppSettings
        {
            Bars = 16 + 4 * _rnd.Next(0, 21),
            Gain = 0.7 + _rnd.NextDouble() * 1.8,
            Decay = 0.75 + _rnd.NextDouble() * 0.2,
            Glow = _rnd.NextDouble(),
            Mirror = _rnd.Next(2) == 1,
            Peaks = _rnd.Next(2) == 1,
            Rounded = _rnd.Next(2) == 1,
            Grid = _rnd.Next(2) == 1,
            Palette = _rnd.Next(3)
        });

        _service.PlaybackStopped += () => Dispatcher.UIThread.Post(() =>
        {
            if (_service.Duration > TimeSpan.Zero && _service.Position >= _service.Duration - TimeSpan.FromMilliseconds(300))
                Advance(auto: true);
        });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            try
            {
                Viz.PushBands(_service.GetSpectrum());
                Viz.IsLive = _service.IsPlaying;
                Viz.Mirror = MirrorChk.IsChecked == true;
                Viz.ShowPeaks = PeaksChk.IsChecked == true;
                Viz.Rounded = RoundedChk.IsChecked == true;
                Viz.ShowGrid = GridChk.IsChecked == true;
                Viz.Tick(0.033);
                PlayIcon.IsVisible = !_service.IsPlaying;
                PauseIcon.IsVisible = _service.IsPlaying;
                if (!_dragSeek)
                {
                    var d = _service.Duration;
                    SeekSlider.Value = d > TimeSpan.Zero ? _service.Position.TotalSeconds / d.TotalSeconds * 1000 : 0;
                    TimeNow.Text = _service.Position.ToString(@"mm\:ss");
                    TimeTotal.Text = d.ToString(@"mm\:ss");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Сбой визуализации: {ex.Message}";
            }
        };

        UpdateModeButtons();
        Opened += async (_, _) => await InitAsync();
        Closing += (_, _) => SaveSettings();
        Closed += (_, _) => { _timer.Stop(); _service.Dispose(); };
    }

    private async Task InitAsync()
    {
        try
        {
            if (File.Exists(SettingsPath))
                _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { _settings = new AppSettings(); }

        _folder = _settings.Folder;
        _recent = (_settings.RecentFolders ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        _shuffle = _settings.Shuffle;
        _repeatMode = (RepeatMode)Math.Clamp(_settings.Repeat, 0, 2);
        ThemeManager.Apply(_settings.Theme);
        if (!string.IsNullOrEmpty(_settings.Accent)) ThemeManager.SetAccent(_settings.Accent);
        RefreshThemeBox(ThemeManager.Current.Name);
        ApplyLab(_settings);
        VolumeSlider.Value = _settings.Volume;
        UpdateModeButtons();
        UpdateFolderLabel();

        _timer.Start();
        if (Directory.Exists(_folder)) await ScanAsync();
        else await OpenFolderDialogAsync(initial: true);
    }

    private async Task OpenFolderDialogAsync(bool initial = false)
    {
        var dlg = new FolderDialog(_folder, _recent);
        var ok = await dlg.ShowDialog<bool>(this);
        if (ok && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            _folder = dlg.SelectedPath!.Trim();
            RememberFolder(_folder);
            UpdateFolderLabel();
            await ScanAsync();
        }
        else if (initial)
        {
            ScanStatus.Text = "Папка не выбрана. Нажми «Сменить папку», чтобы указать библиотеку.";
        }
    }

    private void RememberFolder(string path)
    {
        _recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _recent.Insert(0, path);
        if (_recent.Count > 8) _recent.RemoveRange(8, _recent.Count - 8);
    }

    private void UpdateFolderLabel()
    {
        FolderPathLabel.Text = _folder;
        ToolTip.SetTip(FolderPathLabel, _folder);
    }

    private void UpdateModeButtons()
    {
        ShuffleBtn.Classes.Set("active", _shuffle);
        RepeatBtn.Classes.Set("active", _repeatMode != RepeatMode.Off);
        RepeatIcon.IsVisible = _repeatMode != RepeatMode.One;
        RepeatOneIcon.IsVisible = _repeatMode == RepeatMode.One;
        ToolTip.SetTip(ShuffleBtn, _shuffle ? "Случайный порядок: вкл" : "Случайный порядок: выкл");
        ToolTip.SetTip(RepeatBtn, _repeatMode switch
        {
            RepeatMode.All => "Повтор: весь плейлист",
            RepeatMode.One => "Повтор: один трек",
            _ => "Повтор: выкл"
        });
    }

    private void RefreshThemeBox(string select)
    {
        _updatingUi = true;
        ThemeBox.ItemsSource = ThemeManager.Themes.Select(t => t.Name).ToList();
        ThemeBox.SelectedItem = select;
        _updatingUi = false;
    }

    private void ApplyLab(AppSettings s)
    {
        BarsSlider.Value = s.Bars;
        GainSlider.Value = s.Gain;
        DecaySlider.Value = s.Decay;
        GlowSlider.Value = s.Glow;
        MirrorChk.IsChecked = s.Mirror;
        PeaksChk.IsChecked = s.Peaks;
        RoundedChk.IsChecked = s.Rounded;
        GridChk.IsChecked = s.Grid;
        PaletteBox.SelectedIndex = s.Palette;
    }

    private void SaveSettings()
    {
        _settings = new AppSettings
        {
            Theme = ThemeManager.Current.Name,
            Accent = ThemeManager.Current.Accent,
            Folder = _folder,
            RecentFolders = _recent.ToList(),
            Bars = (int)BarsSlider.Value,
            Gain = GainSlider.Value,
            Decay = DecaySlider.Value,
            Glow = GlowSlider.Value,
            Mirror = MirrorChk.IsChecked == true,
            Peaks = PeaksChk.IsChecked == true,
            Rounded = RoundedChk.IsChecked == true,
            Grid = GridChk.IsChecked == true,
            Palette = PaletteBox.SelectedIndex,
            Volume = VolumeSlider.Value,
            Shuffle = _shuffle,
            Repeat = (int)_repeatMode
        };
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* нет прав на запись — не критично */ }
    }

    private async Task ScanAsync()
    {
        ScanStatus.Text = "Сканирование…";
        try
        {
            _all = await LibraryScanner.ScanAsync(_folder);
            RememberFolder(_folder);
            UpdateFolderLabel();
            ApplyFilter();
            ScanStatus.Text = $"{_all.Count} треков · {_folder}";
        }
        catch (Exception ex)
        {
            ScanStatus.Text = $"Ошибка сканирования: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var q = (SearchBox.Text ?? "").Trim();
        IEnumerable<TrackInfo> rows = _all;
        if (q.Length > 0)
            rows = rows.Where(t => (t.DisplayTitle + " " + (t.Album ?? "")).Contains(q, StringComparison.OrdinalIgnoreCase));

        rows = SortBox.SelectedIndex switch
        {
            1 => rows.OrderBy(t => t.DisplayTitle),
            2 => rows.OrderBy(t => t.DurationSeconds),
            3 => rows.OrderByDescending(t => t.Bitrate),
            _ => rows.OrderBy(t => t.Artist ?? "").ThenBy(t => t.DisplayTitle)
        };

        _view.Clear();
        foreach (var t in rows) _view.Add(t);
        CountLabel.Text = $"{_view.Count} / {_all.Count}";
    }

    private void PlaySelected()
    {
        if (TrackList.SelectedItem is TrackInfo t) PlayTrack(t);
        else if (_view.Count > 0) PlayTrack(_view[0]);
    }

    private void Step(int dir)
    {
        if (_view.Count == 0) return;
        if (dir > 0) { Advance(auto: false); return; }

        if (_shuffle && _history.Count > 0)
        {
            var t = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            PlayTrack(t, recordHistory: false);
            return;
        }

        var idx = _current == null ? 0 : _view.IndexOf(_current);
        idx = idx <= 0 ? (_repeatMode == RepeatMode.Off ? 0 : _view.Count - 1) : idx - 1;
        PlayTrack(_view[idx]);
    }

    private void Advance(bool auto)
    {
        if (_view.Count == 0) return;

        if (auto && _repeatMode == RepeatMode.One && _current != null)
        {
            PlayTrack(_current, recordHistory: false);
            return;
        }

        if (_shuffle && _view.Count > 1)
        {
            int cur = _current == null ? -1 : _view.IndexOf(_current);
            int idx;
            do { idx = _rnd.Next(_view.Count); } while (idx == cur);
            PlayTrack(_view[idx]);
            return;
        }

        var i = _current == null ? -1 : _view.IndexOf(_current);
        if (i < 0) { PlayTrack(_view[0]); return; }
        if (i < _view.Count - 1) { PlayTrack(_view[i + 1]); return; }

        if (!auto || _repeatMode == RepeatMode.All) PlayTrack(_view[0]);
        else { _service.Stop(); StatusLabel.Text = "Плейлист завершён."; }
    }

    private void PlayTrack(TrackInfo t, bool recordHistory = true)
    {
        try
        {
            _service.Load(t.FilePath);
            _service.Volume = (float)VolumeSlider.Value;
            _service.Play();

            if (recordHistory && _current != null && !ReferenceEquals(_current, t))
            {
                _history.Add(_current);
                if (_history.Count > 300) _history.RemoveAt(0);
            }

            _current = t;
            NowTitle.Text = t.DisplayTitle;
            TitleTrack.Text = t.DisplayTitle;
            NowArtist.Text = $"{t.Artist ?? "Неизвестный артист"} · {t.Album ?? "без альбома"}";
            NowFormatText.Text = t.Extension.TrimStart('.').ToUpperInvariant();
            NowBitrateText.Text = t.IsLossless ? "LOSSLESS" : $"{t.Bitrate} kbps";
            TrackIndex.Text = $"{_view.IndexOf(t) + 1}/{_view.Count}";
            StatusLabel.Text = $"Играет: {Path.GetFileName(t.FilePath)}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Не удалось воспроизвести {Path.GetFileName(t.FilePath)}: {ex.Message}";
        }
    }

        private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && MaxIcon is not null && RestoreIcon is not null)
        {
            var max = WindowState == WindowState.Maximized;
            MaxIcon.IsVisible = !max;
            RestoreIcon.IsVisible = max;
        }
    }

    private void Swatch_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex }) ThemeManager.SetAccent(hex);
    }
}