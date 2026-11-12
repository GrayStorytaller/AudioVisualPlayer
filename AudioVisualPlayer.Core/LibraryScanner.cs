using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioVisualPlayer.Core;

public static class LibraryScanner
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".aiff", ".ape", ".m4a", ".ogg", ".wma"
    };

    public static Task<List<TrackInfo>> ScanAsync(string folder) => Task.Run(() => Scan(folder));

    public static Task<int> CountAudioFilesAsync(string folder, CancellationToken ct = default) => Task.Run(() =>
    {
        int count = 0;
        void Walk(string dir)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    ct.ThrowIfCancellationRequested();
                    if (AudioExtensions.Contains(Path.GetExtension(f))) count++;
                }
                foreach (var sub in Directory.EnumerateDirectories(dir)) Walk(sub);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* нет доступа — пропускаем */ }
        }
        if (Directory.Exists(folder)) Walk(folder);
        return count;
    }, ct);

    public static List<TrackInfo> Scan(string folder)
    {
        var tracks = new List<TrackInfo>();
        if (!Directory.Exists(folder)) return tracks;
        CollectFiles(folder, tracks);
        return tracks.OrderBy(t => t.Artist ?? "").ThenBy(t => t.DisplayTitle).ToList();
    }

    private static void CollectFiles(string folder, List<TrackInfo> tracks)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (!AudioExtensions.Contains(Path.GetExtension(file))) continue;
                try { tracks.Add(ReadTrack(file)); }
                catch { /* битый файл пропускаем */ }
            }
            foreach (var sub in Directory.EnumerateDirectories(folder)) CollectFiles(sub, tracks);
        }
        catch { /* нет доступа к папке — пропускаем */ }
    }

    private static TrackInfo ReadTrack(string file)
    {
        var info = new TrackInfo
        {
            FilePath = file,
            SizeBytes = new FileInfo(file).Length
        };
        using var tagFile = TagLib.File.Create(file);
        info.Artist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist;
        info.Title = tagFile.Tag.Title;
        info.Album = tagFile.Tag.Album;
        info.Bitrate = tagFile.Properties.AudioBitrate;
        info.DurationSeconds = tagFile.Properties.Duration.TotalSeconds;
        return info;
    }
}