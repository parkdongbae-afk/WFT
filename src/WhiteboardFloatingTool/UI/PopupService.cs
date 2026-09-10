using System;
using System.Collections.Generic;
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

internal static class VisualTreeExt
{
    public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var gc in FindVisualChildren<T>(child))
                yield return gc;
        }
    }
}

/// <summary>전체화면 클릭 캐처 + 카드 호스트 (NOACTIVATE, 바깥 탭 닫힘)</summary>
internal sealed class OverlayWindow : Window
{
    public readonly Canvas Host = new();
    private readonly Grid _root;

    public Action? OutsideClicked;

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // 히트테스트 유지 (SKILL §10)
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _root = new Grid { Background = Background };
        _root.Children.Add(Host);
        Content = _root;

        _root.PreviewMouseDown += (s, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, _root) || ReferenceEquals(e.OriginalSource, Host))
            {
                OutsideClicked?.Invoke();
                e.Handled = true;
            }
        };

        SourceInitialized += (s, e) =>
        {
            var src = (HwndSource)PresentationSource.FromVisual(this);
            NativeMethods.ApplyNoActivateStyle(src.Handle);
        };
    }

    public void Place(FrameworkElement card, FrameworkElement anchor, bool preferLeftOfScreen)
    {
        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double cardW = card.DesiredSize.Width;
        double cardH = card.DesiredSize.Height;

        Point tlDevice = anchor.PointToScreen(new Point(0, 0));
        var src = (HwndSource?)PresentationSource.FromVisual(this);
        var fromDevice = src?.CompositionTarget?.TransformFromDevice;
        var tl = fromDevice.HasValue ? fromDevice.Value.Transform(tlDevice) : tlDevice;
        double anchorH = anchor.ActualHeight > 0 ? anchor.ActualHeight : 56;
        double anchorW = anchor.ActualWidth > 0 ? anchor.ActualWidth : 56;

        double x, y = tl.Y + anchorH / 2 - 48;
        x = preferLeftOfScreen ? tl.X - cardW - 8 : tl.X + anchorW + 8;

        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
        x = Math.Clamp(x, vl + 8, vl + vw - cardW - 8);
        y = Math.Clamp(y, vt + 8, Math.Max(vt + 8, vt + vh - cardH - 8));

        Canvas.SetLeft(card, x - vl);
        Canvas.SetTop(card, y - vt);
        if (!Host.Children.Contains(card)) Host.Children.Add(card);
    }
}

/// <summary>팝업 패널 관리 (창 목록/탭 목록/스냅 레이아웃/투명도)</summary>
public sealed class PopupService
{
    private readonly FloatingBarWindow _bar;
    private OverlayWindow? _overlay;
    private FrameworkElement? _mainCard;
    private readonly DispatcherTimer _autoClose = new() { Interval = TimeSpan.FromSeconds(3) };

    public bool IsOpen => _overlay != null;

    public PopupService(FloatingBarWindow bar)
    {
        _bar = bar;
        _autoClose.Tick += (_, _) => Close();
    }

    private OverlayWindow EnsureOverlay()
    {
        Close();
        var ov = new OverlayWindow();
        ov.OutsideClicked += Close;
        ov.Show();
        if (_bar.Hwnd != 0) NativeMethods.ReassertTopmost(_bar.Hwnd); // 바를 오버레이 위에 유지
        _overlay = ov;
        return ov;
    }

    private void ArmAutoClose()
    {
        int sec = AppState.Settings.Behavior.AutoClosePanelSec;
        if (sec <= 0) { _autoClose.Stop(); return; }
        _autoClose.Stop();
        _autoClose.Interval = TimeSpan.FromSeconds(sec);
        _autoClose.Start();
    }

    public void Close()
    {
        _autoClose.Stop();
        _mainCard = null;
        if (_overlay != null)
        {
            var ov = _overlay;
            _overlay = null;
            try { ov.Close(); } catch { }
        }
    }

    private void ShowCard(FrameworkElement card, FrameworkElement anchor)
    {
        var ov = EnsureOverlay();
        _mainCard = card;
        card.PreviewMouseDown += (s, e) => ArmAutoClose();
        card.PreviewMouseMove += (s, e) => ArmAutoClose();
        bool preferLeft = _bar.Left + _bar.Width / 2 >
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        ov.Place(card, anchor, preferLeft);
        ArmAutoClose();
    }

    public void ShowDesktopMenu(FrameworkElement anchorBtn)
    {
        var card = Ui.Card(320);
        var sp = new StackPanel();
        sp.Children.Add(Ui.PanelHeader("화면 전환", Close));

        var create = Ui.MakeWindowButton("새 화면전환 만들기", () =>
        {
            Close();
            InputSim.WinCtrlKey(NativeMethods.VK_D);
        }, accent: true);
        create.Margin = new Thickness(0, 14, 0, 0);
        sp.Children.Add(create);

        var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var prev = Ui.MakeWindowButton("◀  이전 화면", () =>
        {
            Close();
            InputSim.WinCtrlKey(NativeMethods.VK_LEFT);
        });
        Grid.SetColumn(prev, 0);
        row.Children.Add(prev);
        var next = Ui.MakeWindowButton("다음 화면  ▶", () =>
        {
            Close();
            InputSim.WinCtrlKey(NativeMethods.VK_RIGHT);
        });
        Grid.SetColumn(next, 2);
        row.Children.Add(next);
        sp.Children.Add(row);

        var remove = Ui.MakeWindowButton("현재 화면전환 닫기", () =>
        {
            Close();
            InputSim.WinCtrlKey(NativeMethods.VK_F4);
        });
        remove.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(remove);
        var warn = Ui.Txt("현재 화면에 열려 있는 창은 이전 화면으로 이동됩니다", "Txt.BodySecondary");
        warn.FontSize = 11;
        warn.TextWrapping = TextWrapping.Wrap;
        warn.Margin = new Thickness(2, 6, 2, 0);
        sp.Children.Add(warn);

        var hint = Ui.Txt("키보드로는 Ctrl + ⊞Win + ← / → 키로 전환할 수 있습니다", "Txt.BodySecondary");
        hint.FontSize = 11;
        hint.TextWrapping = TextWrapping.Wrap;
        hint.Margin = new Thickness(2, 14, 2, 0);
        sp.Children.Add(hint);

        card.Child = sp;
        ShowCard(card, anchorBtn);
    }

    // ── 창 목록 (F-01) ────────────────────────────────────────

    public void ShowWindowList(FrameworkElement anchorBtn)
    {
        var windows = WindowService.GetAltTabList();

        var card = Ui.Card(440);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(Ui.PanelHeader("창 전환", Close));
        Grid.SetRow(grid.Children[0], 0);

        var listHost = new StackPanel();
        var sv = new ScrollViewer
        {
            Content = listHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.VirtualScreenHeight * 0.7,
            PanningMode = PanningMode.VerticalOnly
        };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);

        if (windows.Count == 0)
        {
            var empty = Ui.Txt("전환할 창이 없습니다", "Txt.BodySecondary");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Margin = new Thickness(0, 40, 0, 40);
            listHost.Children.Add(empty);
        }
        else
        {
            foreach (var w in windows)
                listHost.Children.Add(BuildWindowItem(w));
        }

        card.Child = grid;
        ShowCard(card, anchorBtn);

        // 창 아이콘 비동기 로딩
        foreach (var w in windows)
        {
            var item = listHost.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag is WinInfo wi && wi.Hwnd == w.Hwnd);
            if (item?.Child is Grid row)
            {
                var iconImg = row.Children.OfType<StackPanel>()
                    .SelectMany(sp => sp.Children.OfType<Image>())
                    .FirstOrDefault(img => img.Tag as string == "icon");
                if (iconImg != null)
                {
                    nint hwnd = w.Hwnd;
                    Task.Run(() =>
                    {
                        var icon = WindowService.GetWindowIcon(hwnd);
                        Application.Current.Dispatcher.BeginInvoke(() => { if (icon != null && iconImg.Source == null) iconImg.Source = icon; });
                    });
                }
            }
        }
    }

    private Border BuildWindowItem(WinInfo w)
    {
        const double thumbW = 160, thumbH = 90;

        // 썸네일/최소화 배지
        var thumbImg = new Image { Stretch = Stretch.Uniform };
        FrameworkElement thumbContent;
        if (w.IsMinimized)
        {
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var ph = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6) };
            ph.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Pressed");
            sp.Children.Add(ph);
            var badge = Ui.Txt("최소화됨", "Txt.BodySecondary");
            badge.FontSize = 11;
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            badge.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(badge);
            thumbContent = sp;
        }
        else
        {
            thumbContent = thumbImg;
        }

        var thumb = new Border
        {
            Width = thumbW,
            Height = thumbH,
            Child = thumbContent,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true
        };
        thumb.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Pressed");
        if (w.IsForeground)
        {
            thumb.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
            thumb.BorderThickness = new Thickness(2);
        }

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var iconImg = new Image { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, Tag = "icon" };
        right.Children.Add(iconImg);
        var title = Ui.Txt(w.Title, "Txt.Body");
        title.FontWeight = FontWeights.SemiBold;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.MaxHeight = 40;
        title.Margin = new Thickness(0, 2, 0, 0);
        right.Children.Add(title);
        var proc = Ui.Txt(w.ProcessName, "Txt.BodySecondary");
        proc.FontSize = 12;
        proc.Margin = new Thickness(0, 2, 0, 0);
        right.Children.Add(proc);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (w.IsForeground) // 좌측 3px Accent 바 (WFT_UI §4.4)
        {
            row.ColumnDefinitions.Insert(0, new ColumnDefinition { Width = GridLength.Auto });
            var accentBar = new Border { Width = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 0, 6, 0) };
            accentBar.SetResourceReference(Border.BackgroundProperty, "Brush.Accent");
            Grid.SetColumn(accentBar, 0);
            row.Children.Add(accentBar);
        }
        Grid.SetColumn(thumb, w.IsForeground ? 1 : 0);
        Grid.SetColumn(right, w.IsForeground ? 2 : 1);
        row.Children.Add(thumb);
        row.Children.Add(right);

        var item = new Border
        {
            Child = row,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 8),
            Tag = w
        };
        item.SetResourceBgElevated();

        nint hwnd = w.Hwnd;
        item.PreviewMouseDown += (s, e) =>
        {
            e.Handled = true;
            Close();
            WindowService.Activate(hwnd);
        };

        if (!w.IsMinimized)
        {
            WindowService.RequestThumbnail(hwnd, bmp =>
            {
                Application.Current.Dispatcher.BeginInvoke(() => { if (bmp != null) thumbImg.Source = bmp; });
            });
        }
        return item;
    }

    // ── 탭 목록 (F-03) ────────────────────────────────────────

    public void ShowTabList(FrameworkElement anchorBtn, nint targetHwnd)
    {
        nint bh = 0;
        if (targetHwnd != 0 && WindowService.IsBrowserProcess(WindowService.GetProcessName(targetHwnd)))
            bh = targetHwnd;
        else
        {
            var b = WindowService.GetAltTabList().FirstOrDefault(w => w.IsBrowser);
            if (b != null) bh = b.Hwnd;
        }

        var card = Ui.Card(360);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        string browserName = bh != 0
            ? (WindowService.GetProcessName(bh).Equals("chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome" : "Edge")
            : "브라우저";
        grid.Children.Add(Ui.PanelHeader($"{browserName} · 탭 목록", Close));
        Grid.SetRow(grid.Children[0], 0);

        var listHost = new StackPanel();
        var sv = new ScrollViewer
        {
            Content = listHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.VirtualScreenHeight * 0.7,
            PanningMode = PanningMode.VerticalOnly
        };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);
        card.Child = grid;

        if (bh == 0)
        {
            var msg = Ui.Txt("브라우저 창을 먼저 선택하세요", "Txt.BodySecondary");
            msg.Margin = new Thickness(0, 32, 0, 32);
            msg.HorizontalAlignment = HorizontalAlignment.Center;
            listHost.Children.Add(msg);
            ShowCard(card, anchorBtn);
            return;
        }

        for (int i = 0; i < 3; i++) // 스켈레톤 (최대 600ms)
        {
            var sk = new Border { Height = 40, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 4, 0, 4) };
            sk.SetResourceBgElevated();
            listHost.Children.Add(sk);
        }

        ShowCard(card, anchorBtn);
        nint browserHwnd = bh;

        Task.Run(() =>
        {
            var tabs = BrowserTabs.EnumerateTabs(browserHwnd);
            string? winName = null;
            try { winName = BrowserTabs.GetWindowName(browserHwnd); } catch { }
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_overlay == null) return;
                listHost.Children.Clear();

                if (tabs == null || tabs.Count == 0)
                {
                    var fail = new StackPanel();
                    var msg = Ui.Txt("탭 목록을 읽을 수 없습니다", "Txt.Body");
                    msg.Margin = new Thickness(0, 20, 0, 4);
                    msg.HorizontalAlignment = HorizontalAlignment.Center;
                    fail.Children.Add(msg);
                    var sub = Ui.Txt("이전/다음 탭 버튼을 사용하세요", "Txt.BodySecondary");
                    sub.HorizontalAlignment = HorizontalAlignment.Center;
                    sub.Margin = new Thickness(0, 0, 0, 12);
                    fail.Children.Add(sub);
                    var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    btns.Children.Add(Ui.MakeWindowButton("이전 탭", () => { Close(); _bar.ExecuteAction("prevTab"); }));
                    btns.Children.Add(new Border { Width = 8 });
                    btns.Children.Add(Ui.MakeWindowButton("다음 탭", () => { Close(); _bar.ExecuteAction("nextTab"); }));
                    fail.Children.Add(btns);
                    listHost.Children.Add(fail);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(winName))
                {
                    var head = Ui.Txt(winName, "Txt.BodySecondary");
                    head.FontSize = 12;
                    head.TextTrimming = TextTrimming.CharacterEllipsis;
                    head.Margin = new Thickness(2, 0, 2, 8);
                    listHost.Children.Add(head);
                }

                foreach (var t in tabs)
                {
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var label = Ui.Txt(string.IsNullOrWhiteSpace(t.Title) ? "(제목 없음)" : t.Title, "Txt.Body");
                    label.VerticalAlignment = VerticalAlignment.Center;
                    label.TextTrimming = TextTrimming.CharacterEllipsis;
                    Grid.SetColumn(label, 0);
                    row.Children.Add(label);
                    if (t.IsSelected)
                    {
                        var dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
                        dot.SetResourceReference(Ellipse.FillProperty, "Brush.Accent");
                        Grid.SetColumn(dot, 1);
                        row.Children.Add(dot);
                    }

                    var it = new Border
                    {
                        Child = row,
                        Height = 48,
                        Padding = new Thickness(8, 0, 8, 0),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    it.SetResourceBgElevated();
                    var tab = t;
                    it.PreviewMouseDown += (s, e) =>
                    {
                        e.Handled = true;
                        Close();
                        WindowService.Activate(browserHwnd);
                        Task.Run(() => BrowserTabs.SelectTab(tab));
                    };
                    listHost.Children.Add(it);
                }
            });
        });
    }

    // ── 스냅 레이아웃 (F-07, 자체 패널) ───────────────────────

    private sealed record SnapLayoutDef(string Name, double[][] Slots);

    private static readonly SnapLayoutDef[] Layouts =
    {
        new("2분할", new[] { new[] { 0.0, 0, 0.5, 1 }, new[] { 0.5, 0, 0.5, 1 } }),
        new("1+2", new[] { new[] { 0.0, 0, 0.34, 1 }, new[] { 0.34, 0, 0.66, 0.5 }, new[] { 0.34, 0.5, 0.66, 0.5 } }),
        new("3분할", new[] { new[] { 0.0, 0, 1.0 / 3, 1 }, new[] { 1.0 / 3, 0, 1.0 / 3, 1 }, new[] { 2.0 / 3, 0, 1.0 / 3, 1 } }),
        new("4분할", new[] { new[] { 0, 0, 0.5, 0.5 }, new[] { 0.5, 0, 0.5, 0.5 }, new[] { 0, 0.5, 0.5, 0.5 }, new[] { 0.5, 0.5, 0.5, 0.5 } }),
        new("전체", new[] { new[] { 0.0, 0, 1, 1 } }),
    };

    public void ShowSnapLayout(FrameworkElement anchorBtn, nint targetHwnd)
    {
        var windows = WindowService.GetAltTabList();

        var card = Ui.Card(480);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(Ui.PanelHeader("스냅 레이아웃", Close));
        Grid.SetRow(grid.Children[0], 0);

        var body = new StackPanel();
        var sv = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.VirtualScreenHeight * 0.7,
            PanningMode = PanningMode.VerticalOnly
        };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);
        card.Child = grid;

        var intro = Ui.Txt("원하는 배치 모양을 선택하세요", "Txt.BodySecondary");
        intro.Margin = new Thickness(0, 10, 0, 8);
        body.Children.Add(intro);

        var pickerWrap = new WrapPanel();
        body.Children.Add(pickerWrap);

        var slotHost = new StackPanel();
        body.Children.Add(slotHost);
        var hint = Ui.Txt("레이아웃을 먼저 선택하세요", "Txt.BodySecondary");
        hint.Margin = new Thickness(0, 8, 0, 0);
        slotHost.Children.Add(hint);

        foreach (var layout in Layouts)
        {
            var content = new StackPanel();
            var preview = new Canvas { Width = 104, Height = 60, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var s in layout.Slots)
            {
                var r = new Border
                {
                    Width = Math.Max(6, 100 * s[2] - 3),
                    Height = Math.Max(6, 56 * s[3] - 3),
                    CornerRadius = new CornerRadius(3)
                };
                r.SetResourceReference(Border.BackgroundProperty, "Brush.Accent");
                r.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
                r.BorderThickness = new Thickness(1);
                r.Opacity = 0.45;
                Canvas.SetLeft(r, 2 + 100 * s[0]);
                Canvas.SetTop(r, 2 + 56 * s[1]);
                preview.Children.Add(r);
            }
            content.Children.Add(preview);
            var name = Ui.Txt(layout.Name, "Txt.Body");
            name.FontSize = 12;
            name.FontWeight = FontWeights.SemiBold;
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(name);

            var layoutBtn = new Button
            {
                Content = content,
                Style = Ui.TryStyle("WftWindowButton"),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var chosen = layout;
            layoutBtn.PreviewMouseDown += (s, e) =>
            {
                e.Handled = true;
                hint.Text = "";
                BuildSlotPicker(slotHost, chosen, windows);
            };
            pickerWrap.Children.Add(layoutBtn);
        }

        ShowCard(card, anchorBtn);
    }

    private void BuildSlotPicker(StackPanel slotHost, SnapLayoutDef layout, List<WinInfo> windows)
    {
        slotHost.Children.Clear();

        var info = Ui.Txt("영역을 탭한 뒤 창을 선택하세요", "Txt.BodySecondary");
        info.Margin = new Thickness(0, 4, 0, 8);
        slotHost.Children.Add(info);

        var canvas = new Canvas { Width = 324, Height = 182, HorizontalAlignment = HorizontalAlignment.Left };
        slotHost.Children.Add(canvas);

        var filled = new string?[layout.Slots.Length];
        var slotLabels = new TextBlock[layout.Slots.Length];

        for (int i = 0; i < layout.Slots.Length; i++)
        {
            var s = layout.Slots[i];
            int idx = i;
            var slot = new Border
            {
                Width = 324 * s[2] - 4,
                Height = 182 * s[3] - 4,
                CornerRadius = new CornerRadius(6)
            };
            slot.SetResourceBgElevated();
            slot.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
            slot.BorderThickness = new Thickness(2);
            var slotLabel = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            slotLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg.Secondary");
            slot.Child = slotLabel;
            slotLabels[idx] = slotLabel;
            Canvas.SetLeft(slot, 324 * s[0]);
            Canvas.SetTop(slot, 182 * s[1]);
            canvas.Children.Add(slot);

            slot.PreviewMouseDown += (s2, e2) =>
            {
                e2.Handled = true;
                ShowWindowChooserForSlot(canvas, layout, idx, windows, filled, slotLabels);
            };
        }

        var done = Ui.MakeWindowButton("완료", Close, accent: true);
        done.HorizontalAlignment = HorizontalAlignment.Left;
        done.Margin = new Thickness(0, 4, 0, 0);
        slotHost.Children.Add(done);
    }

    private void ShowWindowChooserForSlot(Canvas canvas, SnapLayoutDef layout, int slotIdx,
        List<WinInfo> windows, string?[] filled, TextBlock[] slotLabels)
    {
        var chooser = Ui.Card(320);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(Ui.PanelHeader($"{slotIdx + 1}번 영역 · 창 선택", () => HostRemove(chooser)));
        Grid.SetRow(grid.Children[0], 0);

        var list = new StackPanel();
        var sv = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 300,
            PanningMode = PanningMode.VerticalOnly
        };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);
        chooser.Child = grid;

        foreach (var w in windows)
        {
            var label = Ui.Txt(w.Title, "Txt.Body");
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var it = new Border
            {
                Child = label,
                Padding = new Thickness(8),
                Height = 40,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 4)
            };
            it.SetResourceBgElevated();
            var win = w;
            it.PreviewMouseDown += (s, e) =>
            {
                e.Handled = true;
                var sdef = layout.Slots[slotIdx];
                bool isFull = layout.Slots.Length == 1;
                WindowService.PlaceToSlot(win.Hwnd, sdef[0], sdef[1], sdef[2], sdef[3], isFull);
                filled[slotIdx] = win.Title;
                slotLabels[slotIdx].Text = win.Title.Length > 14 ? win.Title[..14] + "…" : win.Title;
                slotLabels[slotIdx].FontSize = 9;
                HostRemove(chooser);
                int next = Array.FindIndex(filled, f => f == null);
                if (next >= 0 && next != slotIdx)
                    ShowWindowChooserForSlot(canvas, layout, next, windows, filled, slotLabels);
            };
            list.Children.Add(it);
        }

        if (_overlay != null)
        {
            chooser.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double cardLeft = _mainCard != null ? Canvas.GetLeft(_mainCard) : 8;
            if (double.IsNaN(cardLeft)) cardLeft = 8;
            double cardW = _mainCard?.ActualWidth ?? 300;
            double left = cardLeft + cardW + 8;
            double top = _mainCard != null && !double.IsNaN(Canvas.GetTop(_mainCard)) ? Canvas.GetTop(_mainCard) : 8;
            Canvas.SetLeft(chooser, Math.Min(left, Math.Max(8, _overlay.Width - chooser.DesiredSize.Width - 8)));
            Canvas.SetTop(chooser, Math.Max(8, Math.Min(top, _overlay.Height - chooser.DesiredSize.Height - 8)));
            _overlay.Host.Children.Add(chooser);
        }
        ArmAutoClose();
    }

    private void HostRemove(FrameworkElement el)
    {
        try { _overlay?.Host.Children.Remove(el); } catch { }
        ArmAutoClose();
    }

    // ── 투명도 슬라이더 (F-09, Grip 길게 누르기) ───────────────

    public void ShowOpacitySlider(FrameworkElement anchorBtn)
    {
        var st = AppState.Settings.Appearance;

        var card = Ui.Card(280);
        var sp = new StackPanel();

        var title = Ui.Txt("투명도", "Txt.Title");
        title.Margin = new Thickness(0, 0, 0, 8);
        sp.Children.Add(title);

        var slider = new Slider
        {
            Minimum = 20,
            Maximum = 100,
            Value = Math.Round(st.Opacity * 100),
            Style = Ui.TryStyle("WftHSlider"),
            IsSnapToTickEnabled = true,
            TickFrequency = 1
        };
        slider.ValueChanged += (s, e) =>
        {
            AppState.Settings.Appearance.Opacity = e.NewValue / 100.0;
            _bar.ApplyOpacity();
        };
        sp.Children.Add(slider);

        var labels = new Grid();
        labels.ColumnDefinitions.Add(new ColumnDefinition());
        labels.ColumnDefinitions.Add(new ColumnDefinition());
        var l20 = Ui.Txt("20%", "Txt.BodySecondary"); l20.FontSize = 11;
        var l100 = Ui.Txt("100%", "Txt.BodySecondary"); l100.FontSize = 11; l100.HorizontalAlignment = HorizontalAlignment.Right;
        labels.Children.Add(l20); Grid.SetColumn(l20, 0);
        labels.Children.Add(l100); Grid.SetColumn(l100, 1);
        labels.Margin = new Thickness(0, 2, 0, 12);
        sp.Children.Add(labels);

        var hoverRow = new Grid();
        hoverRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hoverRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hoverLabel = Ui.Txt("터치하면 선명하게", "Txt.Body");
        Grid.SetColumn(hoverLabel, 0);
        hoverRow.Children.Add(hoverLabel);
        var toggle = new CheckBox { IsChecked = st.OpacityOnHover, Style = Ui.TryStyle("WftToggle") };
        toggle.Checked += (_, _) => { AppState.Settings.Appearance.OpacityOnHover = true; _bar.ApplyOpacity(); };
        toggle.Unchecked += (_, _) => { AppState.Settings.Appearance.OpacityOnHover = false; _bar.ApplyOpacity(); };
        Grid.SetColumn(toggle, 1);
        hoverRow.Children.Add(toggle);
        sp.Children.Add(hoverRow);

        card.Child = sp;
        ShowCard(card, anchorBtn);

        var ov = _overlay;
        if (ov != null) ov.Closed += (s, e) => AppState.Save();
    }
}

internal static class BorderResourceExtensions
{
    public static void SetResourceBgElevated(this Border b)
        => b.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Elevated");
}
