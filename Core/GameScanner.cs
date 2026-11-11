using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NvidiaCi
{
    public class GameScanner
    {
        private readonly List<GameItem> _games = new();
        private readonly HashSet<string> _gamePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _iconDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NvidiaCi",
            "GameIcons");

        public List<GameItem> ScanForGames(int maxResults = 200)
        {
            _games.Clear();
            _gamePaths.Clear();

            ScanSteamLibraries(maxResults);
            ScanEpicGames(maxResults);
            ScanGogGames(maxResults);
            ScanEaGames(maxResults);
            ScanManualLibraries(maxResults);

            return _games
                .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        private void ScanSteamLibraries(int maxResults)
        {
            var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((RegistryHive hive, RegistryView view) in GetRegistryLocations())
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var steamKey = baseKey.OpenSubKey(@"SOFTWARE\Valve\Steam");
                    if (steamKey != null)
                    {
                        AddPath(steamRoots, steamKey.GetValue("InstallPath") as string);
                        AddPath(steamRoots, steamKey.GetValue("SteamPath") as string);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Steam registry lookup failed: {ex.Message}");
                }
            }

            foreach (string steamRoot in steamRoots)
            {
                var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddPath(libraries, steamRoot);

                string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFile))
                {
                    try
                    {
                        foreach (string line in File.ReadLines(libraryFile))
                        {
                            Match match = Regex.Match(line, "\"path\"\\s+\"(.+?)\"", RegexOptions.IgnoreCase);
                            if (match.Success)
                                AddPath(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Steam library list could not be read: {ex.Message}");
                    }
                }

                foreach (string library in libraries)
                {
                    string steamApps = Path.Combine(library, "steamapps");
                    if (!Directory.Exists(steamApps))
                        steamApps = Path.Combine(library, "steamapps");

                    foreach (string manifest in EnumerateFiles(steamApps, "appmanifest_*.acf"))
                    {
                        try
                        {
                            string content = File.ReadAllText(manifest);
                            string? name = ReadVdfValue(content, "name");
                            string? installDirectory = ReadVdfValue(content, "installdir");
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDirectory))
                                continue;

                            AddGame(name, Path.Combine(steamApps, "common", installDirectory), null, "Steam");
                            if (_games.Count >= maxResults) return;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Steam manifest could not be read ({manifest}): {ex.Message}");
                        }
                    }
                }
            }
        }

        private void ScanEpicGames(int maxResults)
        {
            string manifestDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic",
                "EpicGamesLauncher",
                "Data",
                "Manifests");

            foreach (string manifest in EnumerateFiles(manifestDirectory, "*.item"))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
                    JsonElement root = document.RootElement;
                    string? name = ReadJsonString(root, "DisplayName");
                    string? installLocation = ReadJsonString(root, "InstallLocation");
                    string? launchExecutable = ReadJsonString(root, "LaunchExecutable");

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(installLocation))
                    {
                        AddGame(name, installLocation, launchExecutable, "Epic Games");
                        if (_games.Count >= maxResults) return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Epic manifest could not be read ({manifest}): {ex.Message}");
                }
            }
        }

        private void ScanGogGames(int maxResults)
        {
            foreach ((RegistryHive hive, RegistryView view) in GetRegistryLocations())
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var gamesKey = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games");
                    if (gamesKey == null) continue;

                    foreach (string subKeyName in gamesKey.GetSubKeyNames())
                    {
                        using var gameKey = gamesKey.OpenSubKey(subKeyName);
                        string? installPath = gameKey?.GetValue("path") as string;
                        string? name = gameKey?.GetValue("gameName") as string;
                        if (string.IsNullOrWhiteSpace(name))
                            name = gameKey?.GetValue("name") as string;
                        if (string.IsNullOrWhiteSpace(name))
                            name = subKeyName;

                        AddGame(name, installPath, null, "GOG");
                        if (_games.Count >= maxResults) return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GOG registry lookup failed: {ex.Message}");
                }
            }
        }

        private void ScanEaGames(int maxResults)
        {
            foreach ((RegistryHive hive, RegistryView view) in GetRegistryLocations())
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var gamesKey = baseKey.OpenSubKey(@"SOFTWARE\Electronic Arts\Electronic Arts");
                    if (gamesKey == null) continue;

                    foreach (string subKeyName in gamesKey.GetSubKeyNames())
                    {
                        using var gameKey = gamesKey.OpenSubKey(subKeyName);
                        string? installPath =
                            gameKey?.GetValue("Install Dir") as string ??
                            gameKey?.GetValue("InstallDir") as string ??
                            gameKey?.GetValue("InstallPath") as string;
                        if (string.IsNullOrWhiteSpace(installPath))
                            continue;

                        string? name = gameKey?.GetValue("DisplayName") as string;
                        AddGame(name ?? subKeyName, installPath, null, "EA");
                        if (_games.Count >= maxResults) return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EA registry lookup failed: {ex.Message}");
                }
            }

            string eaDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "EA Games");
            ScanInstallFolders(eaDirectory, "EA", maxResults);
        }

        private void ScanManualLibraries(int maxResults)
        {
            foreach (string root in new[] { @"C:\Games", @"D:\Games", @"E:\Games" })
            {
                ScanInstallFolders(root, "Local", maxResults);
                if (_games.Count >= maxResults) return;
            }
        }

        private void ScanInstallFolders(string root, string platform, int maxResults)
        {
            foreach (string directory in EnumerateDirectories(root))
            {
                AddGame(Path.GetFileName(directory), directory, null, platform);
                if (_games.Count >= maxResults) return;
            }
        }

        private void AddGame(string name, string? installPath, string? relativeExecutable, string platform)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                return;

            installPath = Environment.ExpandEnvironmentVariables(installPath.Trim().Trim('"'));
            if (!Directory.Exists(installPath))
                return;

            string? executable = null;
            if (!string.IsNullOrWhiteSpace(relativeExecutable))
            {
                string candidate = Path.GetFullPath(Path.Combine(installPath, relativeExecutable));
                if (File.Exists(candidate))
                    executable = candidate;
            }

            executable ??= FindGameExecutable(installPath);
            if (executable == null || !_gamePaths.Add(executable))
                return;

            _games.Add(new GameItem
            {
                Name = name.Trim(),
                Path = executable,
                Platform = platform,
                IconPath = ExtractIcon(executable)
            });
        }

        private static string? FindGameExecutable(string installPath)
        {
            var candidates = new List<string>();
            CollectExecutables(installPath, candidates, 0);

            string? preferred = candidates
                .Where(path => !IsSupportExecutable(Path.GetFileNameWithoutExtension(path)))
                .OrderByDescending(GetFileSize)
                .FirstOrDefault();

            return preferred ?? candidates.OrderByDescending(GetFileSize).FirstOrDefault();
        }

        private static void CollectExecutables(string directory, List<string> candidates, int depth)
        {
            if (depth > 3) return;

            try
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly));
                if (depth == 3) return;

                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    string folderName = Path.GetFileName(child);
                    if (folderName.Equals("Redist", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("Support", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("BattlEye", StringComparison.OrdinalIgnoreCase))
                        continue;

                    CollectExecutables(child, candidates, depth + 1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not inspect game folder ({directory}): {ex.Message}");
            }
        }

        private static bool IsSupportExecutable(string name) =>
            name.Contains("unins", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("redist", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("vc_redist", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("report", StringComparison.OrdinalIgnoreCase);

        private string? ExtractIcon(string executable)
        {
            try
            {
                Directory.CreateDirectory(_iconDirectory);
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(executable.ToUpperInvariant()));
                string iconPath = Path.Combine(_iconDirectory, Convert.ToHexString(hash) + ".png");
                if (File.Exists(iconPath))
                    return iconPath;

                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executable);
                if (icon == null) return null;

                using var bitmap = icon.ToBitmap();
                bitmap.Save(iconPath, System.Drawing.Imaging.ImageFormat.Png);
                return iconPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not extract game icon ({executable}): {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<(RegistryHive Hive, RegistryView View)> GetRegistryLocations()
        {
            yield return (RegistryHive.CurrentUser, RegistryView.Registry64);
            yield return (RegistryHive.CurrentUser, RegistryView.Registry32);
            yield return (RegistryHive.LocalMachine, RegistryView.Registry64);
            yield return (RegistryHive.LocalMachine, RegistryView.Registry32);
        }

        private static string? ReadVdfValue(string content, string key)
        {
            Match match = Regex.Match(
                content,
                $"\"{Regex.Escape(key)}\"\\s+\"(.+?)\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string? ReadJsonString(JsonElement root, string propertyName) =>
            root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static void AddPath(HashSet<string> paths, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            if (Directory.Exists(path))
                paths.Add(Path.GetFullPath(path));
        }

        private static IEnumerable<string> EnumerateFiles(string directory, string pattern)
        {
            try
            {
                return Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).ToArray()
                    : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not list files in {directory}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> EnumerateDirectories(string directory)
        {
            try
            {
                return Directory.Exists(directory)
                    ? Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray()
                    : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not list folders in {directory}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static long GetFileSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not read executable size ({path}): {ex.Message}");
                return 0;
            }
        }
    }
}
