# PulseDeck

Аудиоплеер на C# / Avalonia / NAudio.

## Возможности
- Библиотека: сканирование папки, теги (TagLib#), поиск, сортировка, бейджи LOSSLESS/kbps
- FFT-визуализатор: палитры, зеркало, пики, свечение, сетка, живая настройка
- Скины: 5 тем + акценты, экспорт в JSON (папка themes/)
- Повтор (выкл / плейлист / трек), шафл с историей, перемотка, громкость
- Кастомный тайтлбар и фирменная иконка

## Сборка и запуск
```powershell
powershell -ExecutionPolicy Bypass -File .\make-icon.ps1
dotnet build
dotnet run --project AudioVisualPlayer.App
```