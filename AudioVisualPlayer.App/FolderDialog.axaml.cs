using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AudioVisualPlayer.App.Themes;
using AudioVisualPlayer.Core;

namespace AudioVisualPlayer.App;

public partial class FolderDialog : Window
{
    private CancellationTokenSource? _cts;
    public string? SelectedPath { get; private set; }

    public FolderDialog() : this("", Array.Empty<string>()) { }

    public FolderDialog(string current, IEnumerable<string> recent)
    {
        InitializeComponent();
        PathBox.Text = current;
        RecentList.ItemsSource = recent.Where(p => Directory.Exists(p)).Distinct().ToList();

        PathBox.TextChanged += (_, _) => _ = ValidateAsync();
        BrowseBtn.Click += async (_, _) => await BrowseAsync();
        RecentList.DoubleTapped += (_, _) => { if (RecentList.SelectedItem is string p) PathBox.Text = p; };
        CancelBtn.Click += (_, _) => Close(false);
        OkBtn.Click += (_, _) => { SelectedPath = (PathBox.Text ?? "").Trim(); Close(true); };
        Opened += (_, _) => _ = ValidateAsync();
    }

    private async Task BrowseAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } sp) return;
        IStorageFolder? start = null;
        var path = (PathBox.Text ?? "").Trim();
        if (Directory.Exists(path))
            start = await sp.TryGetFolderFromPathAsync(new Uri(path));
        var folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Папка с музыкой",
            SuggestedStartLocation = start
        });
        if (folders.Count > 0) PathBox.Text = folders[0].Path.LocalPath;
    }

    private async Task ValidateAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var path = (PathBox.Text ?? "").Trim();
        OkBtn.IsEnabled = false;

        void Say(string text, bool ok)
        {
            ValidateLabel.Text = text;
            ValidateLabel.Foreground = new SolidColorBrush(ok ? ThemeManager.Current.SuccessColor : ThemeManager.Current.DangerColor);
        }

        if (path.Length == 0) { Say("Введи путь или нажми «Обзор…».", false); return; }
        if (!Directory.Exists(path)) { Say("Папка не найдена: проверь путь.", false); return; }

        Say("Считаю аудиофайлы…", true);
        try
        {
            await Task.Delay(250, cts.Token);
            var count = await LibraryScanner.CountAudioFilesAsync(path, cts.Token);
            if (!ReferenceEquals(_cts, cts)) return;
            OkBtn.IsEnabled = count > 0;
            Say(count > 0 ? $"Найдено аудиофайлов: {count}. Можно сканировать." : "В папке нет поддерживаемых аудиофайлов.", count > 0);
        }
        catch (OperationCanceledException) { /* ввод изменился — проверка отменена */ }
        catch { Say("Нет доступа к папке.", false); }
    }
}