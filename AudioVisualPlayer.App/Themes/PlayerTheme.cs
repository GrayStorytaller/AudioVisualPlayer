using System.Text.Json.Serialization;
using Avalonia.Media;

namespace AudioVisualPlayer.App.Themes;

public class PlayerTheme
{
    public string Name { get; set; } = "Noir Pulse";
    public bool Light { get; set; }
    public string Bg { get; set; } = "#0B0E14";
    public string Surface { get; set; } = "#121826";
    public string SurfaceAlt { get; set; } = "#1B2436";
    public string Accent { get; set; } = "#FF3D81";
    public string Accent2 { get; set; } = "#22D3EE";
    public string Text { get; set; } = "#EAF2F7";
    public string TextDim { get; set; } = "#8FA3B0";
    public string Border { get; set; } = "#243247";
    public string Success { get; set; } = "#4ADE80";
    public string Danger { get; set; } = "#F87171";

    [JsonIgnore] public Color BgColor => Color.Parse(Bg);
    [JsonIgnore] public Color SurfaceColor => Color.Parse(Surface);
    [JsonIgnore] public Color SurfaceAltColor => Color.Parse(SurfaceAlt);
    [JsonIgnore] public Color AccentColor => Color.Parse(Accent);
    [JsonIgnore] public Color Accent2Color => Color.Parse(Accent2);
    [JsonIgnore] public Color TextColor => Color.Parse(Text);
    [JsonIgnore] public Color TextDimColor => Color.Parse(TextDim);
    [JsonIgnore] public Color BorderColor => Color.Parse(Border);
    [JsonIgnore] public Color SuccessColor => Color.Parse(Success);
    [JsonIgnore] public Color DangerColor => Color.Parse(Danger);
}