using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace AudioVisualPlayer.App.Themes;

public static class ThemeManager
{
    public static List<PlayerTheme> Themes { get; } = new();
    public static PlayerTheme Current { get; private set; } = new();
    public static string ThemesDir => Path.Combine(AppContext.BaseDirectory, "themes");

    public static void Initialize()
    {
        Themes.AddRange(new[]
        {
            new PlayerTheme(),
            new PlayerTheme { Name = "Amber Deck", Bg = "#14100B", Surface = "#1E1710", SurfaceAlt = "#2A2015",
                Accent = "#FFB020", Accent2 = "#FF5C39", Text = "#F5EBDD", TextDim = "#A8977F", Border = "#3A2C1B",
                Success = "#A3E635", Danger = "#FB7185" },
            new PlayerTheme { Name = "Acid Tape", Bg = "#0C120B", Surface = "#141C12", SurfaceAlt = "#1D2A19",
                Accent = "#A3E635", Accent2 = "#34D399", Text = "#EAF5E4", TextDim = "#93A68C", Border = "#28391F",
                Success = "#4ADE80", Danger = "#F87171" },
            new PlayerTheme { Name = "Blue Hour", Bg = "#0A1020", Surface = "#121A30", SurfaceAlt = "#1B2745",
                Accent = "#60A5FA", Accent2 = "#F472B6", Text = "#E8EEF9", TextDim = "#8C9BC0", Border = "#263457",
                Success = "#34D399", Danger = "#FB7185" },
            new PlayerTheme { Name = "Paper Studio", Light = true, Bg = "#F4F1EA", Surface = "#FFFFFF", SurfaceAlt = "#EDE8DC",
                Accent = "#D9482B", Accent2 = "#1F6FEB", Text = "#171A1F", TextDim = "#6B7280", Border = "#DDD6C8",
                Success = "#16A34A", Danger = "#DC2626" },
        });

        if (Directory.Exists(ThemesDir))
            foreach (var f in Directory.EnumerateFiles(ThemesDir, "*.json"))
                try
                {
                    var t = JsonSerializer.Deserialize<PlayerTheme>(File.ReadAllText(f));
                    if (t != null && !Themes.Exists(x => x.Name == t.Name)) Themes.Add(t);
                }
                catch { /* повреждённый скин пропускаем */ }

        Apply(Themes[0].Name);
    }

    public static void Apply(string name)
    {
        var t = Themes.Find(x => x.Name == name) ?? Themes[0];
        Current = t;
        var res = Application.Current!.Resources;
        void Set(string k, Color c) { res[k + "Color"] = c; res[k + "Brush"] = new SolidColorBrush(c); }
        Set("Bg", t.BgColor); Set("Surface", t.SurfaceColor); Set("SurfaceAlt", t.SurfaceAltColor);
        Set("Accent", t.AccentColor); Set("Accent2", t.Accent2Color); Set("Text", t.TextColor);
        Set("TextDim", t.TextDimColor); Set("Border", t.BorderColor); Set("Success", t.SuccessColor);
        Set("Danger", t.DangerColor);
        Application.Current.RequestedThemeVariant = t.Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    public static void SetAccent(string hex)
    {
        Current.Accent = hex;
        var c = Color.Parse(hex);
        var res = Application.Current!.Resources;
        res["AccentColor"] = c;
        res["AccentBrush"] = new SolidColorBrush(c);
    }

    public static string SaveCurrent()
    {
        Directory.CreateDirectory(ThemesDir);
        var copy = JsonSerializer.Deserialize<PlayerTheme>(JsonSerializer.Serialize(Current))!;
        copy.Name = "Custom " + DateTime.Now.ToString("HHmmss");
        File.WriteAllText(Path.Combine(ThemesDir, copy.Name + ".json"),
            JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
        Themes.Add(copy);
        Apply(copy.Name);
        return copy.Name;
    }
}