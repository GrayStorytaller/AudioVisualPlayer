using System;
using System.IO;

namespace AudioVisualPlayer.Core;

public class TrackInfo
{
    public string FilePath { get; set; } = "";
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public int Bitrate { get; set; }
    public double DurationSeconds { get; set; }
    public long SizeBytes { get; set; }

    public bool HasTags => !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title);

    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

    public bool IsLossless => Extension is ".flac" or ".wav" or ".ape" or ".aiff";

    public string DisplayTitle => HasTags ? $"{Artist} — {Title}" : Path.GetFileNameWithoutExtension(FilePath);

    public string QualityBadge => IsLossless ? "LOSSLESS" : $"{Bitrate} kbps";

    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
}