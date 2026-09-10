using System;
using System.IO;

namespace WFT.Core;

internal static class Log
{
    private static readonly object _lock = new();
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WhiteboardFloatingTool", "logs");

    internal static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Dir);
                string file = Path.Combine(Dir, $"wft-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { /* 로깅 실패는 무시 (교실 환경 최우선) */ }
    }

    internal static void Error(string where, Exception ex)
        => Write($"[ERR] {where}: {ex.GetType().Name}: {ex.Message}");

    public static string FolderOf() => Dir;
}
