using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace NvidiaCi
{
    public sealed class UserSettings
    {
        public bool DoNotDisturb { get; set; }
        public string? FfmpegPath { get; set; }
        public string OverlayHotkey { get; set; } = "Z";
        public string ScreenshotHotkey { get; set; } = "F10";
    }

    public sealed class UserSettingsStore
    {
        private readonly string _filePath;

        public UserSettingsStore()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NvidiaCi");
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "settings.json");
        }

        public UserSettings Load()
        {
            if (!File.Exists(_filePath))
                return new UserSettings();

            try
            {
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_filePath))
                    ?? new UserSettings();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Settings file is invalid ({_filePath}): {ex.Message}");
                return new UserSettings();
            }
        }

        public void Save(UserSettings settings)
        {
            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temporaryPath, _filePath, true);
        }

        public static string ScreenshotDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "NvidiaLite",
            "Screenshots");

        public static string RecordingDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "NvidiaLite",
            "Recordings");
    }
}
