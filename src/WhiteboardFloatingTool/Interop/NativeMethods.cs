using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WFT.Interop;

internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_TOPMOST = 0x00000008;

    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;
    public const int SW_MAXIMIZE = 3;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public static readonly nint HWND_TOPMOST = new(-1);

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const int WM_GETICON = 0x007F;
    public const int WM_HOTKEY = 0x0312;

    public const int GCL_HICON = -14;
    public const int GCL_HICONSM = -34;

    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public const uint PW_RENDERFULLCONTENT = 2;

    public const byte VK_SHIFT = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12;
    public const byte VK_LWIN = 0x5B;
    public const byte VK_TAB = 0x09;
    public const byte VK_Q = 0x51;
    public const byte VK_Z = 0x5A;
    public const byte VK_SPACE = 0x20;
    public const byte VK_F1 = 0x70;
    public const byte VK_F4 = 0x73;
    public const byte VK_D = 0x44;
    public const byte VK_LEFT = 0x25;
    public const byte VK_RIGHT = 0x27;

    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder sb, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(nint hWnd, StringBuilder sb, int maxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(nint hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleBitmap(nint hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(nint hObject);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static extern int DwmGetWindowAttributeInt(nint hwnd, int attr, out int val, int cb);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static extern int DwmGetWindowAttributeRect(nint hwnd, int attr, out RECT val, int cb);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg, wParamL, wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf<INPUT>();
    }

    /// <summary>NOACTIVATE + TOOLWINDOW + TOPMOST 확장 스타일 적용 (SKILL §4.1)</summary>
    public static void ApplyNoActivateStyle(nint hwnd)
    {
        int ex = unchecked((int)GetWindowLongPtr(hwnd, GWL_EXSTYLE));
        long nex = ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, unchecked((nint)nex));
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void ReassertTopmost(nint hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void TapAltKey()
    {
        keybd_event(VK_MENU, 0, 0, nint.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, nint.Zero);
    }
}

/// <summary>SendInput 기반 키 조합 시뮬레이터 (F-04~F-07)</summary>
internal static class InputSim
{
    public static void Combo(byte[] modifiers, byte key)
    {
        var inputs = new List<NativeMethods.INPUT>();
        foreach (var m in modifiers)
            inputs.Add(Key(m, down: true));
        inputs.Add(Key(key, down: true));
        inputs.Add(Key(key, down: false));
        for (int i = modifiers.Length - 1; i >= 0; i--)
            inputs.Add(Key(modifiers[i], down: false));
        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), NativeMethods.INPUT.Size);
    }

    public static void WinTab() => Combo(new byte[] { NativeMethods.VK_LWIN }, NativeMethods.VK_TAB);
    public static void WinZ() => Combo(new byte[] { NativeMethods.VK_LWIN }, NativeMethods.VK_Z);
    public static void CtrlTab() => Combo(new byte[] { NativeMethods.VK_CONTROL }, NativeMethods.VK_TAB);
    public static void CtrlShiftTab() => Combo(new byte[] { NativeMethods.VK_CONTROL, NativeMethods.VK_SHIFT }, NativeMethods.VK_TAB);
    public static void WinCtrlKey(byte vk) => Combo(new byte[] { NativeMethods.VK_LWIN, NativeMethods.VK_CONTROL }, vk);

    public static void AltTab() => SendSequence(
        new (byte vk, bool down)[] { (NativeMethods.VK_MENU, true), (NativeMethods.VK_TAB, true), (NativeMethods.VK_TAB, false), (NativeMethods.VK_MENU, false) });

    public static void AltShiftTab() => SendSequence(
        new (byte vk, bool down)[]
        {
            (NativeMethods.VK_MENU, true), (NativeMethods.VK_SHIFT, true),
            (NativeMethods.VK_TAB, true), (NativeMethods.VK_TAB, false),
            (NativeMethods.VK_SHIFT, false), (NativeMethods.VK_MENU, false)
        });

    private static void SendSequence((byte vk, bool down)[] seq)
    {
        var inputs = new List<NativeMethods.INPUT>(seq.Length);
        foreach (var (vk, down) in seq)
            inputs.Add(Key(vk, down));
        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), NativeMethods.INPUT.Size);
    }

    private static NativeMethods.INPUT Key(byte vk, bool down)
    {
        var ki = new NativeMethods.KEYBDINPUT
        {
            wVk = vk,
            wScan = 0,
            dwFlags = down ? 0u : NativeMethods.KEYEVENTF_KEYUP,
            time = 0,
            dwExtraInfo = nint.Zero
        };
        return new NativeMethods.INPUT { type = 1 /* INPUT_KEYBOARD */, U = new NativeMethods.InputUnion { ki = ki } };
    }
}
