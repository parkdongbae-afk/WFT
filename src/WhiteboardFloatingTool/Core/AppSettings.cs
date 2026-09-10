using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WFT.Core;

public sealed class PositionSettings
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public string Orientation { get; set; } = "vertical";
}

public sealed class AppearanceSettings
{
    public double Opacity { get; set; } = 0.85;
    public bool OpacityOnHover { get; set; } = true;
    public int ButtonSize { get; set; } = 56;
    public string Theme { get; set; } = "dark";
    public bool ShowLabels { get; set; } = true;
}

public sealed class BehaviorSettings
{
    public int AutoClosePanelSec { get; set; } = 0;
    public bool StartWithWindows { get; set; } = false;
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+Space";
    public bool HotkeyEnabled { get; set; } = true;
    /// <summary>"custom"(자체 패널) | "windows"(Win11 Win+Z)</summary>
    public string SnapLayoutMode { get; set; } = "custom";
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public bool Onboarded { get; set; }
    public PositionSettings Position { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public List<string>? EnabledActions { get; set; }

    public static readonly string[] DefaultActions =
    {
        "switchWindow", "prevWindow", "nextWindow",
        "tabList", "prevTab", "nextTab",
        "taskView", "desktopMenu", "snapLeft", "snapRight", "snapLayout"
    };
}

/// <summary>시작 프로그램 등록 (HKCU Run — 사용자 단위, 관리자 권한 불필요)</summary>
public static class StartupEntry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WFT";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    /// <summary>등록 시도. 이미 등록되어 있으면 false(변경 없음) 반환.</summary>
    public static bool TryAdd()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (key.GetValue(ValueName) != null) return false;
            string exe = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exe)) return false;
            key.SetValue(ValueName, $"\"{exe}\"");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("StartupEntry.TryAdd", ex);
            return false;
        }
    }

    public static void Remove()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(ValueName, false);
        }
        catch (Exception ex)
        {
            Log.Error("StartupEntry.Remove", ex);
        }
    }
}

public static class AppState
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WhiteboardFloatingTool");
    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppSettings Settings { get; private set; } = new();
    public static event Action? Changed;

    /// <summary>유효한 기능 ID 순서 (알 수 없는 ID 제거, 기본 보장)</summary>
    public static List<string> ResolvedActions()
    {
        var known = ActionDefs.All.Keys.ToHashSet();
        var list = (Settings.EnabledActions ?? Enumerable.Empty<string>())
            .Where(known.Contains)
            .Distinct()
            .ToList();
        return list;
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) { Normalize(); return; }
            string json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
            if (loaded == null) { Normalize(); return; }
            Settings = loaded;
            Normalize();
        }
        catch (Exception ex)
        {
            // 손상된 파일은 백업 후 기본값 (SKILL §7)
            Log.Error("Settings.Load", ex);
            try
            {
                if (File.Exists(SettingsPath))
                    File.Copy(SettingsPath, Path.Combine(SettingsDir, "settings.broken.json"), true);
            }
            catch { }
            Settings = new AppSettings();
            Normalize();
        }
    }

    private static void Normalize()
    {
        Settings.Position ??= new PositionSettings();
        Settings.Appearance ??= new AppearanceSettings();
        Settings.Behavior ??= new BehaviorSettings();
        Settings.Version = 1;

        var a = Settings.Appearance;
        a.Opacity = Math.Clamp(a.Opacity, 0.20, 1.0);
        a.ButtonSize = a.ButtonSize is 44 or 56 or 72 ? a.ButtonSize : 56;
        a.Theme = a.Theme is "dark" or "light" or "system" or "highcontrast" ? a.Theme : "dark";
        Settings.Position.Orientation =
            Settings.Position.Orientation is "vertical" or "horizontal" ? Settings.Position.Orientation : "vertical";

        var b = Settings.Behavior;
        b.AutoClosePanelSec = b.AutoClosePanelSec is 0 or 2 or 3 or 5 ? b.AutoClosePanelSec : 0;
        b.SnapLayoutMode = b.SnapLayoutMode is "custom" or "windows" ? b.SnapLayoutMode : "custom";
        b.ToggleHotkey = string.IsNullOrWhiteSpace(b.ToggleHotkey) ? "Ctrl+Alt+Space" : b.ToggleHotkey;

        Settings.EnabledActions ??= new List<string>(AppSettings.DefaultActions);
        _ = ResolvedActions(); // 검증만
    }

    public static void Reset()
    {
        Settings = new AppSettings();
        Normalize();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOpts));
        }
        catch (Exception ex)
        {
            Log.Error("Settings.Save", ex);
        }
    }

    public static string SettingsFolder => SettingsDir;

    /// <summary>설정 변경 알림 (즉시 반영)</summary>
    public static void RaiseChanged()
    {
        Save();
        Changed?.Invoke();
    }
}
