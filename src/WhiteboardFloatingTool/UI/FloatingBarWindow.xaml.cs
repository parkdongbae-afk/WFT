using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WFT.Core;
using WFT.Interop;

namespace WFT.UI;

public partial class FloatingBarWindow : Window
{
    private const int HotkeyId = 0x5746_54; // "WFT"

    private readonly DispatcherTimer _fgTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _topTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private nint _lastActiveHwnd;
    private bool _lastActiveIsBrowser;
    private bool _collapsed;
    private bool _nativeDragStarted;
    private Point _dragStart;
    private double _dragStartL, _dragStartT;
    private readonly DispatcherTimer _longPressTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _longPressFired;
    private UIElement? _captureElement;

    private StackPanel _expandedPanel = null!;
    private StackPanel _actionPanel = null!;
    private UIElement _grip = null!;
    private Border _collapsedView = null!;
    private FrameworkElement _collapseBtn = null!;

    public nint Hwnd { get; private set; }
    public PopupService Popup { get; }

    public FloatingBarWindow()
    {
        InitializeComponent();
        Popup = new PopupService(this);
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;

        _longPressTimer.Interval = TimeSpan.FromMilliseconds(700);
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer.Stop();
            var now = Mouse.GetPosition(this);
            if (Math.Abs(now.X - _dragStart.X) + Math.Abs(now.Y - _dragStart.Y) > 10) return;
            _longPressFired = true;
            Popup.ShowOpacitySlider(this);
        };

        MouseEnter += (_, _) => ApplyOpacity();
        MouseLeave += (_, _) => ApplyOpacity();

        _fgTimer.Tick += (_, _) => TrackForeground();
        _topTimer.Tick += (_, _) => { if (!_nativeDragStarted) NativeMethods.ReassertTopmost(Hwnd); };
    }

    private void OnSourceInitialized(object? s, EventArgs e)
    {
        var src = (HwndSource)PresentationSource.FromVisual(this);
        Hwnd = src.Handle;
        NativeMethods.ApplyNoActivateStyle(Hwnd);
        src.AddHook(WndProc);
        RegisterHotkey();
    }

    private void OnLoaded(object? s, RoutedEventArgs e)
    {
        bool hadSaved = AppState.Settings.Position.X is not null;
        bool restored = RestorePosition();
        ApplySettings(rebuild: true);
        _fgTimer.Start();
        _topTimer.Start();
        if (hadSaved && !restored)
            App.Toast.Show("모니터 구성이 바뀌어 위치를 초기화했습니다");
    }

    private nint WndProc(nint hwnd, int msg, nint w, nint l, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && w == HotkeyId)
        {
            ToggleVisibility();
            handled = true;
        }
        return nint.Zero;
    }

    // ── 공개 API ──────────────────────────────────────────────

    public void ToggleVisibility()
    {
        Popup.Close();
        if (IsVisible) Hide();
        else { Show(); NativeMethods.ReassertTopmost(Hwnd); }
    }

    public void ApplySettings(bool rebuild)
    {
        if (rebuild) BuildLayout();
        ApplyOpacity();
        RegisterHotkey();
    }

    // ── 레이아웃 구성 ─────────────────────────────────────────

    private void BuildLayout()
    {
        var st = AppState.Settings;
        bool vertical = st.Position.Orientation != "horizontal";
        double bs = st.Appearance.ButtonSize;

        LayoutRoot.Children.Clear();

        _expandedPanel = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal
        };

        // Grip (드래그 손잡이)
        var gripContent = MakeGripIcon(vertical, bs);
        var grip = new Border
        {
            Width = vertical ? bs : 30,
            Height = vertical ? 30 : bs,
            Child = gripContent,
            Cursor = Cursors.SizeAll,
            ToolTip = "끌어서 옮기기 · 두 번 탭하면 접기 · 길게 누르면 투명도"
        };
        AttachDragHandlers(grip);
        _grip = grip;
        _expandedPanel.Children.Add(grip);

        // 접기 버튼 (chevron, 화면 가장자리 방향)
        bool rightSide = Left + Width / 2 > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        var collapseIconKey = vertical ? (rightSide ? "Icon.collapse" : "Icon.expand")
                                       : (Left + Width / 2 > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2 ? "Icon.collapse" : "Icon.expand");
        var collapseBtn = Ui.MakeActionButton(collapseIconKey, null, "접기", vertical ? bs : 28, ToggleCollapse, false);
        if (vertical) { collapseBtn.Width = bs; collapseBtn.Height = 28; }
        else { collapseBtn.Width = 28; collapseBtn.Height = bs; }
        _collapseBtn = collapseBtn;
        _expandedPanel.Children.Add(collapseBtn);

        _expandedPanel.Children.Add(vertical
            ? new Separator { Height = 1, Margin = new Thickness(6, 4, 6, 4) }
            : new Separator { Width = 1, Margin = new Thickness(4, 6, 4, 6) });
        SepBrush(_expandedPanel.Children[^1] as Separator);

        // 사용자 기능 버튼 (F-08)
        _actionPanel = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal
        };
        foreach (string id in AppState.ResolvedActions())
        {
            var def = ActionDefs.All[id];
            var btn = Ui.MakeActionButton(def.IconKey,
                st.Appearance.ShowLabels ? def.Label : null,
                def.Tooltip, bs, () => ExecuteAction(id), st.Appearance.ShowLabels);
            btn.Tag = id;
            _actionPanel.Children.Add(btn);
        }
        _expandedPanel.Children.Add(_actionPanel);

        _expandedPanel.Children.Add(vertical
            ? new Separator { Height = 1, Margin = new Thickness(6, 4, 6, 4) }
            : new Separator { Width = 1, Margin = new Thickness(4, 6, 4, 6) });
        SepBrush(_expandedPanel.Children[^1] as Separator);

        // 설정 버튼 (항상 마지막, 숨김 불가)
        var settingsBtn = Ui.MakeActionButton("Icon.settings", st.Appearance.ShowLabels ? "설정" : null,
            "설정", bs, () => App.ShowSettings(), st.Appearance.ShowLabels);
        _expandedPanel.Children.Add(settingsBtn);

        var exitBtn = Ui.MakeActionButton("Icon.power", st.Appearance.ShowLabels ? "종료" : null,
            "프로그램을 종료합니다", bs, () => App.ShutdownNow(), st.Appearance.ShowLabels);
        _expandedPanel.Children.Add(exitBtn);

        // 접힌 상태 뷰 (원형 버튼)
        var appIcon = Ui.MakeIcon("Icon.app", bs >= 56 ? 28 : 22);
        _collapsedView = new Border
        {
            Width = bs, Height = bs,
            CornerRadius = new CornerRadius(bs / 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = appIcon,
            Cursor = Cursors.SizeAll,
            ToolTip = "탭하면 펼칩니다 · 끌어서 옮기기"
        };
        AttachDragHandlers(_collapsedView, tapExpands: true);

        if (_collapsed)
        {
            const double cs = 44;
            _collapsedView.Width = cs;
            _collapsedView.Height = cs;
            _collapsedView.CornerRadius = new CornerRadius(cs / 2);
            LayoutRoot.Children.Add(_collapsedView);
            SizeToContent = SizeToContent.Manual;
            MinWidth = 0; MinHeight = 0;
            Width = cs + 10; Height = cs + 10;
            RootBorder.CornerRadius = new CornerRadius((cs + 10) / 2);
        }
        else
        {
            LayoutRoot.Children.Add(_expandedPanel);
            SizeToContent = vertical ? SizeToContent.Height : SizeToContent.Width;
            MinWidth = 0; MinHeight = 0;
            if (vertical) { Width = bs + 10; ClearValue(HeightProperty); }
            else { Height = bs + 10; ClearValue(WidthProperty); }
            RootBorder.CornerRadius = new CornerRadius(14);
        }

        UpdateBrowserButtons();
    }

    private static void SepBrush(Separator? s)
    {
        if (s != null) s.SetResourceReference(Separator.BackgroundProperty, "Brush.Border");
    }

    private UIElement MakeGripIcon(bool vertical, double bs)
    {
        var icon = Ui.MakeIcon("Icon.grip", 16, 2);
        var rt = new RotateTransform(vertical ? 0 : 90);
        icon.LayoutTransform = rt;
        var host = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };
        return host;
    }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        if (!_collapsed)
        {
            double vw = SystemParameters.VirtualScreenWidth;
            double estW = AppState.Settings.Position.Orientation == "horizontal" ? 400 : 74;
            if (Left > vw - estW) Left = Math.Max(SystemParameters.VirtualScreenLeft, vw - estW - 8);
        }
        BuildLayout();
        SavePosition();
    }

    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const nint HTCAPTION = 0x2;

    private void AttachDragHandlers(UIElement el, bool tapExpands = false)
    {
        el.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount >= 2 && !tapExpands) { ToggleCollapse(); e.Handled = true; return; }
            BeginDrag(el, tapExpands, e);
            e.Handled = true;
        };
        el.PreviewMouseMove += (s, e) => MoveDrag(e);
        el.PreviewMouseLeftButtonUp += (s, e) => EndDrag(tapExpands);
    }

    private void BeginDrag(UIElement? el, bool tapExpands, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragStartL = Left; _dragStartT = Top;
        _nativeDragStarted = false;
        _longPressFired = false;
        _captureElement = el;
        if (!tapExpands) _longPressTimer.Start();
    }

    private void MoveDrag(MouseEventArgs e)
    {
        if (_captureElement == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (_nativeDragStarted) return;

        var p = e.GetPosition(this);
        double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
        double dist = Math.Abs(dx) + Math.Abs(dy);

        if (dist > 3) _longPressTimer.Stop();

        if (_longPressFired)
        {
            if (dist > 10)
            {
                Popup.Close();
                _longPressFired = false;
            }
            else return;
        }

        if (dist > 6)
        {
            _nativeDragStarted = true;
            NativeMethods.SendMessage(Hwnd, WM_NCLBUTTONDOWN, HTCAPTION, nint.Zero);
            OnNativeDragEnded();
        }
    }

    private void OnNativeDragEnded()
    {
        SetPositionClamped(Left, Top);
        SavePosition();
        _nativeDragStarted = false;
        _captureElement = null;
        if (NativeMethods.GetCursorPos(out var p))
        {
            var pt = new NativeMethods.POINT { X = p.X, Y = p.Y };
            NativeMethods.ScreenToClient(Hwnd, ref pt);
            nint lp = (nint)(((pt.Y & 0xFFFF) << 16) | (pt.X & 0xFFFF));
            NativeMethods.PostMessage(Hwnd, 0x0202 /*WM_LBUTTONUP*/, 0, lp);
        }
    }

    private void EndDrag(bool tapExpands)
    {
        _longPressTimer.Stop();
        if (_captureElement == null) return; // 이 요소에서 누르지 않은 업 이벤트는 무시
        bool longFired = _longPressFired;
        bool wasNative = _nativeDragStarted;
        _captureElement = null;
        _longPressFired = false;

        if (wasNative) return;
        if (tapExpands && !longFired) ToggleCollapse();
    }

    private void SetPositionClamped(double x, double y)
    {
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
        const double minVisible = 40;

        double w = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 64 : Width);
        double h = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 64 : Height);

        x = Math.Clamp(x, vl - w + minVisible, vl + vw - minVisible);
        y = Math.Clamp(y, vt - h + minVisible, vt + vh - minVisible);

        // 모서리 근접 스냅 (12px)
        if (Math.Abs(x - vl) <= 12) x = vl;
        if (Math.Abs(x + w - (vl + vw)) <= 12) x = vl + vw - w;
        if (Math.Abs(y - vt) <= 12) y = vt;

        Left = x; Top = y;
    }

    // ── 투명도 (F-09) ─────────────────────────────────────────

    public void ApplyOpacity()
    {
        var st = AppState.Settings.Appearance;
        double opacity = SystemParameters.HighContrast ? 1.0 : st.Opacity; // 고대비 강제 100%
        if (st.OpacityOnHover && IsMouseOver) opacity = 1.0;
        Dispatcher.BeginInvoke(() => Opacity = opacity, DispatcherPriority.Render);
    }

    // ── 포그라운드 추적 ───────────────────────────────────────

    private void TrackForeground()
    {
        try
        {
            nint fg = NativeMethods.GetForegroundWindow();
            if (fg != 0 && !WindowService.IsOwnWindow(fg) && NativeMethods.IsWindowVisible(fg))
            {
                _lastActiveHwnd = fg;
                string proc = WindowService.GetProcessName(fg);
                _lastActiveIsBrowser = WindowService.IsBrowserProcess(proc);
            }
            UpdateBrowserButtons();
        }
        catch { }
    }

    public nint ResolveTarget()
    {
        nint fg = NativeMethods.GetForegroundWindow();
        if (fg != 0 && !WindowService.IsOwnWindow(fg)) return fg;
        return _lastActiveHwnd;
    }

    private void UpdateBrowserButtons()
    {
        foreach (var child in _actionPanel.Children.OfType<Button>())
        {
            string? id = child.Tag as string;
            if (id != null && ActionDefs.All.TryGetValue(id, out var def) && def.NeedsBrowser)
            {
                bool enabled = _lastActiveIsBrowser;
                if (child.IsEnabled != enabled)
                {
                    child.IsEnabled = enabled;
                    child.ToolTip = enabled ? def.Tooltip : "브라우저 창을 먼저 선택하세요";
                }
            }
        }
    }

    // ── 액션 실행 ─────────────────────────────────────────────

    public void ExecuteAction(string id)
    {
        Log.Write($"Action: {id}");
        var target = ResolveTarget();
        try
        {
            switch (id)
            {
                case "switchWindow":
                    Popup.ShowWindowList(this);
                    break;
                case "prevWindow":
                    InputSim.AltShiftTab();
                    break;
                case "nextWindow":
                    InputSim.AltTab();
                    break;
                case "tabList":
                    Popup.ShowTabList(this, target);
                    break;
                case "prevTab":
                    PrevNextTab(prev: true);
                    break;
                case "nextTab":
                    PrevNextTab(prev: false);
                    break;
                case "taskView":
                    InputSim.WinTab();
                    break;
                case "snapLeft":
                    WindowService.Snap(target, SnapDir.Left);
                    break;
                case "snapRight":
                    WindowService.Snap(target, SnapDir.Right);
                    break;
                case "snapTop":
                    WindowService.Snap(target, SnapDir.Top);
                    break;
                case "snapBottom":
                    WindowService.Snap(target, SnapDir.Bottom);
                    break;
                case "desktopMenu":
                    Popup.ShowDesktopMenu(this);
                    break;
                case "snapLayout":
                    if (AppState.Settings.Behavior.SnapLayoutMode == "windows")
                        InputSim.WinZ();
                    else
                        Popup.ShowSnapLayout(this, target);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"ExecuteAction({id})", ex);
            App.Toast.Show("기능을 실행할 수 없습니다");
        }
    }

    private void PrevNextTab(bool prev)
    {
        nint target = ResolveTarget();
        nint browserHwnd = 0;
        if (target != 0 && WindowService.IsBrowserProcess(WindowService.GetProcessName(target)))
            browserHwnd = target;
        else
        {
            // 직전 사용 브라우저 창 탐색
            var b = WindowService.GetAltTabList().FirstOrDefault(w => w.IsBrowser);
            if (b != null) browserHwnd = b.Hwnd;
        }
        if (browserHwnd == 0)
        {
            App.Toast.Show("브라우저 창을 먼저 선택하세요");
            return;
        }
        WindowService.Activate(browserHwnd);
        Task.Run(async () =>
        {
            await Task.Delay(80); // 전면 전환 대기 후 키 전송
            if (prev) InputSim.CtrlShiftTab(); else InputSim.CtrlTab();
        });
    }

    // ── 위치 저장/복원 ─────────────────────────────────────────

    private bool RestorePosition()
    {
        var pos = AppState.Settings.Position;
        if (pos.X is double x && pos.Y is double y)
        {
            double bs = AppState.Settings.Appearance.ButtonSize;
            double probeW = bs + 10, probeH = bs + 10;
            var center = new Point(x + probeW / 2, y + probeH / 2);
            if (IsPointOnAnyScreen(center))
            {
                Left = x; Top = y;
                return true;
            }
        }
        // 초기 위치: 작업 영역 우측 16px, 세로 중앙 (WFT_UI §3.5)
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - 64 - 16;
        Top = wa.Top + (wa.Height - 300) / 2;
        return false;
    }

    private static bool IsPointOnAnyScreen(Point dip)
    {
        var phys = DipToPhysical(dip);
        foreach (var s in System.Windows.Forms.Screen.AllScreens)
        {
            if (phys.X >= s.Bounds.Left && phys.X <= s.Bounds.Right &&
                phys.Y >= s.Bounds.Top && phys.Y <= s.Bounds.Bottom)
                return true;
        }
        return false;
    }

    private static Point DipToPhysical(Point p)
    {
        var src = (HwndSource?)PresentationSource.FromVisual(Application.Current.MainWindow);
        var t = src?.CompositionTarget?.TransformToDevice;
        return t.HasValue ? t.Value.Transform(p) : p;
    }

    public void SavePosition()
    {
        AppState.Settings.Position.X = Left;
        AppState.Settings.Position.Y = Top;
        AppState.Save();
    }

    // ── 전역 단축키 ───────────────────────────────────────────

    private void RegisterHotkey()
    {
        NativeMethods.UnregisterHotKey(Hwnd, HotkeyId);
        var b = AppState.Settings.Behavior;
        if (!b.HotkeyEnabled) return;

        string[] presets = { b.ToggleHotkey, "Ctrl+Alt+Space", "Ctrl+Shift+Space", "Ctrl+Alt+Q" };
        foreach (string preset in presets.Distinct())
        {
            try
            {
                (uint mods, uint vk) = ParseHotkey(preset);
                if (NativeMethods.RegisterHotKey(Hwnd, HotkeyId, mods | 0x4000 /*MOD_NOREPEAT*/, vk))
                {
                    if (preset != b.ToggleHotkey)
                    {
                        b.ToggleHotkey = preset;
                        AppState.Save();
                        App.Toast.Show($"전역 단축키가 {preset}(으)로 변경되었습니다");
                    }
                    return;
                }
            }
            catch (Exception ex) { Log.Error("RegisterHotkey", ex); }
        }
        Log.Write("Hotkey register failed for all presets");
    }

    private static (uint mods, uint vk) ParseHotkey(string s)
    {
        uint mods = 0; uint vk = NativeMethods.VK_SPACE;
        foreach (var part in s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": mods |= NativeMethods.MOD_CONTROL; break;
                case "ALT": mods |= NativeMethods.MOD_ALT; break;
                case "SHIFT": mods |= NativeMethods.MOD_SHIFT; break;
                case "SPACE": vk = NativeMethods.VK_SPACE; break;
                case "Q": vk = NativeMethods.VK_Q; break;
                case "F1": vk = NativeMethods.VK_F1; break;
            }
        }
        if (mods == 0) mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
        return (mods, vk);
    }

    protected override void OnClosed(EventArgs e)
    {
        _fgTimer.Stop();
        _topTimer.Stop();
        NativeMethods.UnregisterHotKey(Hwnd, HotkeyId);
        base.OnClosed(e);
    }
}
