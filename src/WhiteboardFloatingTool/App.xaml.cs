using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WFT.Core;
using WFT.UI;

namespace WFT;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static FloatingBarWindow? _bar;
    private static System.Windows.Forms.NotifyIcon? _tray;
    private static SettingsWindow? _settingsWindow;

    public static FloatingBarWindow Bar => _bar!;
    public static ToastService Toast { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "WhiteboardFloatingTool.Singleton", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error("Dispatcher", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            Log.Error("AppDomain", args.ExceptionObject as Exception ?? new Exception("unknown"));
        TaskScheduler.UnobservedTaskException += (s, args) => Log.Error("Task", args.Exception);

        Log.Write($"=== WFT 시작 (pid {Environment.ProcessId}) ===");

        AppState.Load();
        ApplyTheme();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _bar = new FloatingBarWindow();
        MainWindow = _bar;
        Toast = new ToastService(_bar);
        _bar.Show();

        AppState.Changed += ApplyAll;
        ApplyAll();

        CreateTray();

        if (!AppState.Settings.Onboarded)
        {
            AppState.Settings.Onboarded = true;
            AppState.Save();
            var onboarding = new OnboardingWindow(() => { });
            onboarding.Owner = null;
            onboarding.Show();
        }
    }

    public static void ApplyTheme()
    {
        string theme = AppState.Settings.Appearance.Theme;
        if (SystemParameters.HighContrast) theme = "highcontrast";
        else if (theme == "system")
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                theme = key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? "dark" : "light";
            }
            catch { theme = "dark"; }
        }
        string uri = theme switch
        {
            "light" => "Themes/Light.xaml",
            "highcontrast" => "Themes/HighContrast.xaml",
            _ => "Themes/Dark.xaml"
        };
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
        var merged = Current.Resources.MergedDictionaries;
        if (merged.Count >= 4) merged[3] = dict;
        else merged.Add(dict);
    }

    public static void ApplyAll()
    {
        ApplyTheme();
        Bar.ApplySettings(rebuild: true);
    }

    public static void OpenManual()
    {
        try
        {
            string path = System.IO.Path.Combine(AppState.SettingsFolder, "WFT_매뉴얼.pdf");
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("WFT.Assets.WFT_Manual.pdf");
            if (stream == null)
            {
                Toast.Show("설명서를 찾을 수 없습니다");
                return;
            }
            var file = new System.IO.FileInfo(path);
            if (!file.Exists || file.Length != stream.Length)
            {
                using var fs = System.IO.File.Create(path);
                stream.CopyTo(fs);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("OpenManual", ex);
            Toast.Show("설명서를 열 수 없습니다");
        }
    }

    public static void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }
        var w = new SettingsWindow(Bar)
        {
            Owner = null
        };
        _settingsWindow = w;
        w.Closed += (_, _) => _settingsWindow = null;
        w.Show();
    }

    private void CreateTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("표시/숨김", null, (s, _) => Bar.ToggleVisibility());
        menu.Items.Add("설정", null, (s, _) => ShowSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (s, _) => ExitApp());

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "전자칠판 플로팅 툴",
            Icon = MakeTrayIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (s, _) => Bar.ToggleVisibility();
    }

    private static System.Drawing.Icon MakeTrayIcon()
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(2, 2, 28, 28, 0, 360);
                using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(28, 29, 34));
                g.FillPath(bg, path);
                using var pen1 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(242, 243, 245), 2.5f);
                g.DrawRectangle(pen1, 6, 9, 11, 11);
                using var pen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(76, 141, 255), 2.5f);
                g.DrawRectangle(pen2, 15, 14, 11, 11);
            }
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    public static void ShutdownNow()
    {
        try
        {
            _bar?.SavePosition();
            AppState.Save();
            _tray?.Dispose();
        }
        catch { }
        Current.Shutdown();
    }

    private void ExitApp()
    {
        ShutdownNow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _tray?.Dispose();
            _mutex?.ReleaseMutex();
        }
        catch { }
        Log.Write("=== WFT 종료 ===");
        base.OnExit(e);
    }
}
