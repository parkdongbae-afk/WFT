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
    private bool _dragActive;
    private bool _dragMoving;
    private Point _dragAnchor;
    private Point _dragLast;
    private double _dragThreshold;
    private bool _dragTapExpands;
    private double _grabOffsetX, _grabOffsetY;
    private readonly DispatcherTimer _longPressTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _longPressFired;
    private UIElement? _dragElement;
    private DateTime _lastTapTime = DateTime.MinValue;
    private Point _lastTapPoint;

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
            if (!_dragActive) return;
            if (Math.Abs(_dragLast.X - _dragAnchor.X) + Math.Abs(_dragLast.Y - _dragAnchor.Y) > 10) return;
            _longPressFired = true;
            Popup.ShowOpacitySlider(this);
        };

        MouseEnter += (_, _) => ApplyOpacity();
        MouseLeave += (_, _) => ApplyOpacity();

        _fgTimer.Tick += (_, _) => TrackForeground();
        _topTimer.Tick += (_, _) => { if (!_dragMoving) NativeMethods.ReassertTopmost(Hwnd); };
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
            Width = vertical ? bs : 44,
            Height = vertical ? 44 : bs,
            Background = Brushes.Transparent,
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

    private void AttachDragHandlers(UIElement el, bool tapExpands = false)
    {
        Stylus.SetIsPressAndHoldEnabled(el, false);
        Stylus.SetIsFlicksEnabled(el, false);

        el.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            DragPointerDown(el, tapExpands, e.GetPosition(this), 6);
            el.CaptureMouse();
            e.Handled = true;
        };
        el.PreviewMouseMove += (s, e) =>
        {
            if (!_dragActive || e.LeftButton != MouseButtonState.Pressed) return;
            DragPointerMove(e.GetPosition(this));
        };
        el.PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (!_dragActive) return;
            DragPointerUp(e.GetPosition(this));
            el.ReleaseMouseCapture();
            e.Handled = true;
        };
        el.LostMouseCapture += (s, e) => DragAbandoned();

        el.PreviewTouchDown += (s, e) =>
        {
            DragPointerDown(el, tapExpands, e.GetTouchPoint(this).Position, 14);
            e.TouchDevice.Capture(el);
            e.Handled = true;
        };
        el.PreviewTouchMove += (s, e) =>
        {
            if (!_dragActive) return;
            DragPointerMove(e.GetTouchPoint(this).Position);
            e.Handled = true;
        };
        el.PreviewTouchUp += (s, e) =>
        {
            if (!_dragActive) return;
            DragPointerUp(e.GetTouchPoint(this).Position);
            e.TouchDevice.Capture(null);
            e.Handled = true;
        };
        el.LostTouchCapture += (s, e) => DragAbandoned();

        el.PreviewStylusDown += (s, e) =>
        {
            if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            DragPointerDown(el, tapExpands, e.GetPosition(this), 14);
            e.StylusDevice.Capture(el);
            e.Handled = true;
        };
        el.PreviewStylusMove += (s, e) =>
        {
            if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus || !_dragActive) return;
            DragPointerMove(e.GetPosition(this));
            e.Handled = true;
        };
        el.PreviewStylusUp += (s, e) =>
        {
            if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus || !_dragActive) return;
            DragPointerUp(e.GetPosition(this));
            e.StylusDevice.Capture(null);
            e.Handled = true;
        };
        el.LostStylusCapture += (s, e) => DragAbandoned();
    }

    private void DragPointerDown(UIElement el, bool tapExpands, Point p, double threshold)
    {
        if (_dragActive) return;
        _dragActive = true;
        _dragMoving = false;
        _dragElement = el;
        _dragTapExpands = tapExpands;
        _dragAnchor = p;
        _dragLast = p;
        _dragThreshold = threshold;
        _longPressFired = false;
        if (!tapExpands) _longPressTimer.Start();
    }

    private void DragPointerMove(Point p)
    {
        if (!_dragActive) return;
        double dist = Math.Abs(p.X - _dragAnchor.X) + Math.Abs(p.Y - _dragAnchor.Y);
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

        if (!_dragMoving && dist > _dragThreshold)
        {
            _dragMoving = true;
            Popup.Close();
            Point sp = PointToScreen(p);
            if (NativeMethods.GetWindowRect(Hwnd, out var r))
            {
                _grabOffsetX = r.Left - sp.X;
                _grabOffsetY = r.Top - sp.Y;
            }
            else
            {
                double s0 = DeviceScale();
                _grabOffsetX = Left * s0 - sp.X;
                _grabOffsetY = Top * s0 - sp.Y;
            }
        }
        if (_dragMoving)
        {
            Point sp = PointToScreen(p);
            MoveDragWindowTo(sp.X + _grabOffsetX, sp.Y + _grabOffsetY);
        }
    }

    private void MoveDragWindowTo(double physX, double physY)
    {
        double s = DeviceScale();
        double xd = physX / s, yd = physY / s;
        (xd, yd) = ClampDragPosition(xd, yd, magnet: false);
        NativeMethods.SetWindowPos(Hwnd, nint.Zero,
            (int)Math.Round(xd * s), (int)Math.Round(yd * s), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private double DeviceScale()
    {
        var src = PresentationSource.FromVisual(this);
        double s = src?.CompositionTarget?.TransformToDevice.M11 ?? 0;
        return s > 0 ? s : 1.0;
    }

    private void SyncWindowPosition()
    {
        if (!NativeMethods.GetWindowRect(Hwnd, out var r)) return;
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        if (m != null) { Left = r.Left * m.Value.M11; Top = r.Top * m.Value.M22; }
        else { Left = r.Left; Top = r.Top; }
    }

    private void DragPointerUp(Point p)
    {
        _longPressTimer.Stop();
        if (!_dragActive) return;
        bool moved = _dragMoving;
        bool longFired = _longPressFired;
        _dragActive = false;
        _dragMoving = false;
        _dragElement = null;
        _longPressFired = false;

        if (moved) { SyncWindowPosition(); SetPositionClamped(Left, Top); SavePosition(); return; }
        if (longFired) return;

        if (_dragTapExpands) { ToggleCollapse(); return; }

        var now = DateTime.Now;
        bool doubleTap = (now - _lastTapTime).TotalMilliseconds <= 500
            && Math.Abs(p.X - _lastTapPoint.X) <= 30
            && Math.Abs(p.Y - _lastTapPoint.Y) <= 30;
        _lastTapTime = doubleTap ? DateTime.MinValue : now;
        _lastTapPoint = p;
        if (doubleTap) ToggleCollapse();
    }

    private void DragAbandoned()
    {
        _longPressTimer.Stop();
        if (!_dragActive) return;
        bool moved = _dragMoving;
        _dragActive = false;
        _dragMoving = false;
        _dragElement = null;
        _longPressFired = false;
        if (moved) { SyncWindowPosition(); SetPositionClamped(Left, Top); SavePosition(); }
    }

    private (double x, double y) ClampDragPosition(double x, double y, bool magnet = true)
    {
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
        const double minVisible = 40;

        double w = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 64 : Width);
        double h = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 64 : Height);

        x = Math.Clamp(x, vl - w + minVisible, vl + vw - minVisible);
        y = Math.Clamp(y, vt - h + minVisible, vt + vh - minVisible);

        if (magnet)
        {
            if (Math.Abs(x - vl) <= 12) x = vl;
            if (Math.Abs(x + w - (vl + vw)) <= 12) x = vl + vw - w;
            if (Math.Abs(y - vt) <= 12) y = vt;
        }

        return (x, y);
    }

    private void SetPositionClamped(double x, double y)
    {
        (x, y) = ClampDragPosition(x, y);
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
