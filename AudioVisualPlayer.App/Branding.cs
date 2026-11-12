using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AudioVisualPlayer.App;

public static class Branding
{
    public static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://AudioVisualPlayer.App/Assets/app.ico");
            if (AssetLoader.Exists(uri))
                return new WindowIcon(AssetLoader.Open(uri));
        }
        catch { /* иконка не обязательна для работы плеера */ }
        return null;
    }
}