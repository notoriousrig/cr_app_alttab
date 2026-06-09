using System.IO;
using AltTabCustom.Settings;

namespace AltTabCustom.Core;

/// <summary>
/// Minimal append-only logger writing to %AppData%\AltTabCustom\log.txt. A
/// hook-based tray app has no console, so this is how we find out what went
/// wrong after the fact. All writes are best-effort and never throw.
/// </summary>
internal static class Logger
{
    private static readonly object Gate = new();
    private const long MaxBytes = 512 * 1024; // rotate past ~512 KB

    private static string LogPath => Path.Combine(SettingsStore.Directory, "log.txt");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Directory);
            lock (Gate)
            {
                Rotate();
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never take down the app.
        }
    }

    private static void Rotate()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (info.Exists && info.Length > MaxBytes)
            {
                string old = LogPath + ".old";
                File.Delete(old);
                File.Move(LogPath, old);
            }
        }
        catch
        {
            // ignore rotation failures
        }
    }
}
