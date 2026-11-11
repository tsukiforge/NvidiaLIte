using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NvidiaCi
{
    public sealed class ScreenRecordingService : IDisposable
    {
        private readonly object _sync = new();
        private Process? _process;
        private Process? _finishedProcess;
        private string? _recordingPath;
        private string _lastError = string.Empty;

        public event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

        public bool IsRecording
        {
            get
            {
                lock (_sync)
                    return _process is { HasExited: false };
            }
        }

        public string? RecordingPath
        {
            get
            {
                lock (_sync)
                    return _recordingPath;
            }
        }

        public void Start(string? configuredFfmpegPath)
        {
            lock (_sync)
            {
                if (_process is { HasExited: false })
                    throw new InvalidOperationException("Screen recording is already active.");

                _finishedProcess?.Dispose();
                _finishedProcess = null;
                string ffmpegPath = ResolveFfmpeg(configuredFfmpegPath)
                    ?? throw new FileNotFoundException(
                        "FFmpeg was not found. Install FFmpeg or select ffmpeg.exe in Settings.");

                Directory.CreateDirectory(UserSettingsStore.RecordingDirectory);
                string fileName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss_fff}.mp4";
                _recordingPath = Path.Combine(UserSettingsStore.RecordingDirectory, fileName);
                int collision = 1;
                while (File.Exists(_recordingPath))
                {
                    fileName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{collision++}.mp4";
                    _recordingPath = Path.Combine(UserSettingsStore.RecordingDirectory, fileName);
                }
                _lastError = string.Empty;

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                };
                AddArgument(startInfo, "-hide_banner");
                AddArgument(startInfo, "-loglevel");
                AddArgument(startInfo, "error");
                AddArgument(startInfo, "-f");
                AddArgument(startInfo, "gdigrab");
                AddArgument(startInfo, "-framerate");
                AddArgument(startInfo, "30");
                AddArgument(startInfo, "-draw_mouse");
                AddArgument(startInfo, "1");
                AddArgument(startInfo, "-i");
                AddArgument(startInfo, "desktop");
                AddArgument(startInfo, "-c:v");
                AddArgument(startInfo, "libx264");
                AddArgument(startInfo, "-preset");
                AddArgument(startInfo, "ultrafast");
                AddArgument(startInfo, "-crf");
                AddArgument(startInfo, "27");
                AddArgument(startInfo, "-pix_fmt");
                AddArgument(startInfo, "yuv420p");
                AddArgument(startInfo, "-movflags");
                AddArgument(startInfo, "+faststart");
                AddArgument(startInfo, "-y");
                AddArgument(startInfo, _recordingPath);

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.ErrorDataReceived += OnErrorDataReceived;
                process.Exited += OnProcessExited;
                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException("FFmpeg could not be started.");
                }

                _process = process;
                process.BeginErrorReadLine();
            }
        }

        public async Task StopAsync()
        {
            Process? process;
            lock (_sync)
                process = _process;

            if (process == null || process.HasExited)
                return;

            try
            {
                await process.StandardInput.WriteLineAsync("q");
                await process.StandardInput.FlushAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            }
            catch (TimeoutException)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_sync)
                    _lastError = e.Data;
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (sender is not Process process)
                return;

            string? path;
            string error;
            int exitCode;
            lock (_sync)
            {
                if (!ReferenceEquals(process, _process))
                    return;

                path = _recordingPath;
                error = _lastError;
                exitCode = process.ExitCode;
                _process = null;
                _finishedProcess = process;
            }

            RecordingStopped?.Invoke(this, new RecordingStoppedEventArgs(path, exitCode, error));
        }

        private static string? ResolveFfmpeg(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string path = Environment.ExpandEnvironmentVariables(configuredPath.Trim().Trim('"'));
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }

            string? pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathVariable))
                return null;

            return pathVariable
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory.Trim('"'), "ffmpeg.exe"))
                .FirstOrDefault(File.Exists);
        }

        private static void AddArgument(ProcessStartInfo startInfo, string argument) =>
            startInfo.ArgumentList.Add(argument);

        public void Dispose()
        {
            Process? process;
            Process? finishedProcess;
            lock (_sync)
            {
                process = _process;
                finishedProcess = _finishedProcess;
                _process = null;
                _finishedProcess = null;
            }

            if (process is { HasExited: false })
            {
                try { process.Kill(true); }
                catch (InvalidOperationException) { }
            }
            process?.Dispose();
            finishedProcess?.Dispose();
        }
    }

    public sealed class RecordingStoppedEventArgs : EventArgs
    {
        public RecordingStoppedEventArgs(string? path, int exitCode, string error)
        {
            Path = path;
            ExitCode = exitCode;
            Error = error;
        }

        public string? Path { get; }
        public int ExitCode { get; }
        public string Error { get; }
    }
}
