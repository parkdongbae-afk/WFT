using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WFT.Interop;

namespace WFT.Core;

public sealed class WinInfo
{
    public nint Hwnd { get; init; }
    public string Title { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public bool IsMinimized { get; init; }
    public bool IsForeground { get; init; }
    public bool IsBrowser { get; init; }
    public ImageSource? Icon { get; set; }
    public BitmapSource? Thumbnail { get; set; }
}

public enum SnapDir { Left, Right, Top, Bottom }

/// <summary>창 열거/전환/스냅 (F-01, F-02, F-06)</summary>
public static class WindowService
{
    private static readonly Dictionary<uint, string> _procCache = new();
    private static readonly Thread _thumbThread;
    private static readonly BlockingCollection<(nint Hwnd, Action<BitmapSource?> Callback)> _thumbQueue = new();

    static WindowService()
    {
        _thumbThread = new Thread(ThumbWorker) { IsBackground = true, Name = "WftThumbs" };
        _thumbThread.SetApartmentState(ApartmentState.STA);
        _thumbThread.Start();
    }

    public static bool IsBrowserProcess(string name)
        => name.Equals("chrome", StringComparison.OrdinalIgnoreCase)
        || name.Equals("msedge", StringComparison.OrdinalIgnoreCase);

    public static string GetProcessName(nint hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return "";
        if (_procCache.TryGetValue(pid, out var cached)) return cached;
        string name = "";
        try { name = Process.GetProcessById((int)pid).ProcessName; }
        catch { /* 보호된 프로세스 */ }
        if (_procCache.Count > 512) _procCache.Clear();
        _procCache[pid] = name;
        return name;
    }

    public static bool IsOwnWindow(nint hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == Environment.ProcessId;
    }

    /// <summary>Alt-Tab 목록 (Z-order 순, 필터 규칙 적용)</summary>
    public static List<WinInfo> GetAltTabList()
    {
        var list = new List<WinInfo>();
        nint fg = NativeMethods.GetForegroundWindow();
        var hwnds = new List<nint>();

        NativeMethods.EnumWindows((h, _) => { hwnds.Add(h); return true; }, nint.Zero);

        foreach (var h in hwnds) // EnumWindows는 Z-order top→bottom
        {
            if (!NativeMethods.IsWindowVisible(h)) continue;
            if (IsOwnWindow(h)) continue;

            int len = NativeMethods.GetWindowTextLength(h);
            if (len <= 0) continue;
            var sb = new StringBuilder(len + 1);
            NativeMethods.GetWindowText(h, sb, sb.Capacity);
            string title = sb.ToString().Trim();
            if (title.Length == 0) continue;

            var cls = new StringBuilder(256);
            NativeMethods.GetClassName(h, cls, 256);
            string cn = cls.ToString();
            if (cn is "Progman" or "WorkerW" or "Shell_DefView" or "Windows.UI.Composition.DesktopWindowContentBridge") continue;

            long ex = unchecked((long)NativeMethods.GetWindowLongPtr(h, NativeMethods.GWL_EXSTYLE));
            if ((ex & 0x00000080L) != 0) continue; // WS_EX_TOOLWINDOW

            if (NativeMethods.DwmGetWindowAttributeInt(h, NativeMethods.DWMWA_CLOAKED, out int cloaked, 4) == 0
                && cloaked != 0) continue; // UWP 일시중지/가상 데스크톱

            if (!NativeMethods.GetWindowRect(h, out var r) || r.Width <= 0 || r.Height <= 0)
            {
                if (!NativeMethods.IsIconic(h)) continue;
            }

            string proc = GetProcessName(h);
            list.Add(new WinInfo
            {
                Hwnd = h,
                Title = title,
                ProcessName = proc,
                IsMinimized = NativeMethods.IsIconic(h),
                IsForeground = h == fg,
                IsBrowser = IsBrowserProcess(proc)
            });
        }
        return list;
    }

    /// <summary>창 전면으로 (최소화면 복원, AttachThreadInput 트릭 포함)</summary>
    public static void Activate(nint hwnd)
    {
        if (hwnd == 0) return;
        try
        {
            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            if (NativeMethods.GetForegroundWindow() == hwnd) return;

            nint fg = NativeMethods.GetForegroundWindow();
            uint ft = fg != 0 ? NativeMethods.GetWindowThreadProcessId(fg, out _) : 0;
            uint ct = NativeMethods.GetCurrentThreadId();
            bool attached = false;
            try
            {
                if (ft != 0 && ft != ct)
                    attached = NativeMethods.AttachThreadInput(ct, ft, true);
                NativeMethods.TapAltKey(); // ForegroundLockTimeout 우회
                NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) NativeMethods.AttachThreadInput(ct, ft, false);
            }
        }
        catch (Exception ex) { Log.Error("WindowService.Activate", ex); }
    }

    public static Rect GetWorkArea(nint hwnd)
    {
        nint mon = NativeMethods.MonitorFromWindow(hwnd != 0 ? hwnd : NativeMethods.GetForegroundWindow(),
            NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (NativeMethods.GetMonitorInfo(mon, ref mi))
        {
            var w = mi.rcWork;
            return new Rect(w.Left, w.Top, w.Width, w.Height); // 물리 픽셀
        }
        return new Rect(0, 0,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
    }

    /// <summary>스냅 (F-06): SetWindowPos 직접 배치 — Win+방향키보다 안정적</summary>
    public static void Snap(nint hwnd, SnapDir dir)
    {
        if (hwnd == 0) return;
        try
        {
            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            var wa = GetWorkArea(hwnd);
            int x = (int)wa.X, y = (int)wa.Y, w = (int)wa.Width, h = (int)wa.Height;
            const uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW;

            switch (dir)
            {
                case SnapDir.Left:
                    NativeMethods.SetWindowPos(hwnd, 0, x, y, w / 2, h, flags); break;
                case SnapDir.Right:
                    NativeMethods.SetWindowPos(hwnd, 0, x + (w + 1) / 2, y, w / 2, h, flags); break;
                case SnapDir.Top:
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE); break;
                case SnapDir.Bottom:
                    if (NativeMethods.IsZoomed(hwnd))
                        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                    else
                        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
                    break;
            }
        }
        catch (Exception ex) { Log.Error("WindowService.Snap", ex); }
    }

    /// <summary>스냅 레이아웃 슬롯 배치 (F-07)</summary>
    public static void PlaceToSlot(nint hwnd, double fx, double fy, double fw, double fh, bool maximize = false)
    {
        if (hwnd == 0) return;
        try
        {
            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            if (maximize) { NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE); return; }
            var wa = GetWorkArea(hwnd);
            int x = (int)(wa.X + wa.Width * fx);
            int y = (int)(wa.Y + wa.Height * fy);
            int w = (int)(wa.Width * fw);
            int h = (int)(wa.Height * fh);
            NativeMethods.SetWindowPos(hwnd, 0, x, y, w, h,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }
        catch (Exception ex) { Log.Error("WindowService.PlaceToSlot", ex); }
    }

    public static ImageSource? GetWindowIcon(nint hwnd)
    {
        try
        {
            nint h = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, 2, 0); // ICON_SMALL2
            if (h == 0) h = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, 0, 0);
            if (h == 0) h = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GCL_HICON);
            if (h == 0) h = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GCL_HICONSM);
            if (h != 0)
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(h, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            // exe 아이콘 폴백
            string proc = GetProcessName(hwnd);
            if (proc.Length > 0)
            {
                var p = Process.GetProcessesByName(proc).FirstOrDefault();
                string? path = null;
                try { path = p?.MainModule?.FileName; } catch { }
                if (path != null && System.IO.File.Exists(path))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        return src;
                    }
                }
            }
        }
        catch (Exception ex) { Log.Error("GetWindowIcon", ex); }
        return null;
    }

    /// <summary>썸네일 캡처 요청 (STA 작업 스레드에서 순차 처리)</summary>
    public static void RequestThumbnail(nint hwnd, Action<BitmapSource?> callback)
    {
        try { _thumbQueue.Add((hwnd, callback)); } catch { }
    }

    private static void ThumbWorker()
    {
        foreach (var (hwnd, cb) in _thumbQueue.GetConsumingEnumerable())
        {
            BitmapSource? bmp = null;
            try
            {
                if (hwnd == 0 || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd)) { }
                else
                {
                    var r = new NativeMethods.RECT();
                    if (NativeMethods.DwmGetWindowAttributeRect(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                        out var fb, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0)
                        r = fb;
                    else if (!NativeMethods.GetWindowRect(hwnd, out r)) r = new();

                    int w = r.Width, h = r.Height;
                    if (w > 0 && h > 0 && w <= 4096 && h <= 4096)
                    {
                        nint hdcScreen = NativeMethods.GetDC(nint.Zero);
                        nint hdc = NativeMethods.CreateCompatibleDC(hdcScreen);
                        nint hbmp = NativeMethods.CreateCompatibleBitmap(hdcScreen, w, h);
                        _ = NativeMethods.SelectObject(hdc, hbmp);
                        _ = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                        bmp = Imaging.CreateBitmapSourceFromHBitmap(hbmp, nint.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        _ = NativeMethods.DeleteObject(hbmp);
                        _ = NativeMethods.DeleteDC(hdc);
                        _ = NativeMethods.ReleaseDC(nint.Zero, hdcScreen);
                    }
                }
            }
            catch (Exception ex) { Log.Error("ThumbWorker", ex); }
            try { cb(bmp); } catch { }
        }
    }
}
