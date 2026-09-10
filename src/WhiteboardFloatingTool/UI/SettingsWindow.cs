using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WFT.Core;

namespace WFT.UI;

public sealed class ActionRow : System.ComponentModel.INotifyPropertyChanged
{
    public string Id { get; }
    public ActionDef Def { get; }
    public System.Windows.UIElement IconElement { get; }

    private bool _isOn;
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOn)));
        }
    }

    public ActionRow(string id, bool on)
    {
        Id = id;
        Def = ActionDefs.All[id];
        _isOn = on;
        IconElement = Ui.MakeIcon(Def.IconKey, 20);
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>설정 창 (WFT_UI §4.13): 기능/모양/동작/정보, 즉시 반영·즉시 저장</summary>
public sealed class SettingsWindow : Window
{
    private readonly FloatingBarWindow _bar;
    private ObservableCollection<ActionRow> _rows = new();
    private ItemsControl _funcList = new();
    private StackPanel _previewPanel = new();
    private TextBlock _startupStatus = new();
    private bool _funcDragging;
    private ActionRow? _dragRow;
    private Point _funcDragStart;

    public SettingsWindow(FloatingBarWindow bar)
    {
        _bar = bar;
        Title = "WFT 설정";
        Width = 780; MinWidth = 640;
        SizeToContent = SizeToContent.Height;
        MinHeight = 480;
        MaxHeight = Math.Max(500, SystemParameters.WorkArea.Height - 60);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        SetResourceReference(Window.BackgroundProperty, "Brush.Bg.Bar");
        Closed += (_, _) => bar.SavePosition();

        BuildUi();
    }

    private void BuildUi()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nav = new ListBox { Margin = new Thickness(12), SelectionMode = SelectionMode.Single };
        nav.SetResourceReference(ListBox.BackgroundProperty, "Brush.Bg.Bar");
        nav.SetResourceReference(ListBox.ForegroundProperty, "Brush.Fg.Primary");
        nav.BorderThickness = new Thickness(0);
        foreach (string t in new[] { "기능", "모양", "동작", "정보" })
            nav.Items.Add(MakeNavItem(t));
        nav.SelectedIndex = 0;
        Grid.SetColumn(nav, 0);
        root.Children.Add(nav);

        var content = new Grid { Margin = new Thickness(4, 12, 12, 12) };
        Grid.SetColumn(content, 1);
        root.Children.Add(content);

        var pages = new Dictionary<string, FrameworkElement>
        {
            ["기능"] = BuildFuncPage(),
            ["모양"] = BuildAppearancePage(),
            ["동작"] = BuildBehaviorPage(),
            ["정보"] = BuildInfoPage(),
        };
        string[] navOrder = { "기능", "모양", "동작", "정보" };
        foreach (var key in navOrder)
        {
            pages[key].Visibility = Visibility.Collapsed;
            content.Children.Add(pages[key]);
        }

        nav.SelectionChanged += (s, e) =>
        {
            foreach (var key in navOrder) pages[key].Visibility = Visibility.Collapsed;
            int i = nav.SelectedIndex;
            if (i >= 0 && i < navOrder.Length) pages[navOrder[i]].Visibility = Visibility.Visible;
        };
        pages["기능"].Visibility = Visibility.Visible;

        Content = root;
    }

    private FrameworkElement MakeNavItem(string title)
    {
        var tb = Ui.Txt(title, "Txt.Body");
        tb.FontSize = 16;
        tb.FontWeight = FontWeights.SemiBold;
        tb.Margin = new Thickness(12);
        return tb;
    }

    // ── 기능 탭 (F-08) ────────────────────────────────────────

    private FrameworkElement BuildFuncPage()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });

        var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 0) };
        _funcList = new ItemsControl { ItemTemplate = MakeRowTemplate() };
        _funcList.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
        AttachFuncDrag();
        leftScroll.Content = _funcList;
        Grid.SetColumn(leftScroll, 0);
        grid.Children.Add(leftScroll);

        var right = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        var previewTitle = Ui.Txt("미리보기", "Txt.Title");
        previewTitle.Margin = new Thickness(0, 0, 0, 8);
        right.Children.Add(previewTitle);

        var previewBar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1)
        };
        previewBar.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Bar");
        previewBar.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        _previewPanel = new StackPanel { Orientation = Orientation.Vertical };
        previewBar.Child = _previewPanel;
        var previewScroll = new ScrollViewer
        {
            Content = previewBar,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.WorkArea.Height - 240,
            PanningMode = PanningMode.VerticalOnly,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        right.Children.Add(previewScroll);

        var desc = Ui.Txt("⋮⋮ 행을 끌어 순서를 바꿉니다.\n오른쪽 토글로 표시 여부를 정합니다.", "Txt.BodySecondary");
        desc.Margin = new Thickness(0, 16, 0, 0);
        right.Children.Add(desc);

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        LoadRows();
        return grid;
    }

    private static DataTemplate MakeRowTemplate()
    {
        var borderF = new FrameworkElementFactory(typeof(Border));
        borderF.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borderF.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 4));
        borderF.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
        borderF.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Elevated");

        var dockF = new FrameworkElementFactory(typeof(DockPanel));

        var handleF = new FrameworkElementFactory(typeof(TextBlock));
        handleF.SetValue(TextBlock.TextProperty, "⋮⋮");
        handleF.SetValue(TextBlock.FontSizeProperty, 14.0);
        handleF.SetValue(DockPanel.DockProperty, Dock.Left);
        handleF.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        handleF.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        handleF.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg.Secondary");
        dockF.AppendChild(handleF);

        var iconHostF = new FrameworkElementFactory(typeof(ContentControl));
        iconHostF.SetValue(FrameworkElement.WidthProperty, 22.0);
        iconHostF.SetValue(FrameworkElement.HeightProperty, 22.0);
        iconHostF.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconHostF.SetValue(DockPanel.DockProperty, Dock.Left);
        iconHostF.SetBinding(ContentControl.ContentProperty, new System.Windows.Data.Binding("IconElement"));
        dockF.AppendChild(iconHostF);

        var toggleF = new FrameworkElementFactory(typeof(CheckBox));
        toggleF.SetValue(DockPanel.DockProperty, Dock.Right);
        toggleF.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        toggleF.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 0, 0));
        toggleF.SetValue(CheckBox.StyleProperty, Ui.TryStyle("WftToggle"));
        toggleF.SetBinding(CheckBox.IsCheckedProperty,
            new System.Windows.Data.Binding("IsOn") { Mode = System.Windows.Data.BindingMode.TwoWay });
        dockF.AppendChild(toggleF);

        var labelF = new FrameworkElementFactory(typeof(TextBlock));
        labelF.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Def.Label"));
        labelF.SetValue(TextBlock.FontSizeProperty, 14.0);
        labelF.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        labelF.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 0, 0));
        labelF.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg.Primary");
        labelF.SetResourceReference(TextBlock.FontFamilyProperty, "Font.App");
        dockF.AppendChild(labelF);

        borderF.AppendChild(dockF);
        return new DataTemplate { VisualTree = borderF };
    }

    private void LoadRows()
    {
        var enabled = AppState.ResolvedActions();
        var order = new List<string>(enabled);
        foreach (var g in ActionDefs.Groups)
            foreach (var id in g.Ids)
                if (!order.Contains(id)) order.Add(id);

        _rows = new ObservableCollection<ActionRow>(order.Select(id => new ActionRow(id, enabled.Contains(id))));
        foreach (var r in _rows)
            r.PropertyChanged += (_, _) => { CommitOrder(); RebuildPreview(); };
        _funcList.ItemsSource = _rows;
        RebuildPreview();
    }

    private void AttachFuncDrag()
    {
        _funcList.PreviewMouseDown += (s, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var src = e.OriginalSource as DependencyObject;
            if (IsOverInteractive(src)) return;
            var row = RowFromVisual(src);
            if (row == null) return;
            _dragRow = row;
            _funcDragging = false;
            _funcDragStart = e.GetPosition(_funcList);
            _funcList.CaptureMouse();
            e.Handled = true;
        };
        _funcList.PreviewMouseMove += (s, e) =>
        {
            if (_dragRow == null || !_funcList.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(_funcList);
            if (!_funcDragging && Math.Abs(p.X - _funcDragStart.X) + Math.Abs(p.Y - _funcDragStart.Y) > 6)
            {
                _funcDragging = true;
                SetRowHighlight(_dragRow, true);
                Cursor = Cursors.Hand;
            }
            if (_funcDragging)
            {
                var over = RowFromPoint(p);
                if (over != null && over != _dragRow)
                {
                    _rows.Move(_rows.IndexOf(_dragRow), _rows.IndexOf(over));
                    SetRowHighlight(_dragRow, true);
                }
            }
        };
        _funcList.PreviewMouseUp += (s, e) =>
        {
            if (_funcList.IsMouseCaptured) _funcList.ReleaseMouseCapture();
            if (_dragRow != null) SetRowHighlight(_dragRow, false);
            Cursor = Cursors.Arrow;
            if (_funcDragging) CommitOrder();
            _dragRow = null;
            _funcDragging = false;
        };
        _funcList.LostMouseCapture += (s, e) =>
        {
            if (_dragRow != null) SetRowHighlight(_dragRow, false);
            Cursor = Cursors.Arrow;
            _dragRow = null;
            _funcDragging = false;
        };
    }

    private void SetRowHighlight(ActionRow row, bool on)
    {
        var border = FindRowBorder(row);
        if (border == null) return;
        if (on)
        {
            border.BorderThickness = new Thickness(2);
            border.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
            border.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Hover");
            border.Opacity = 0.9;
        }
        else
        {
            border.BorderThickness = new Thickness(0);
            border.ClearValue(Border.OpacityProperty);
            border.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Elevated");
        }
    }

    private Border? FindRowBorder(ActionRow row)
    {
        foreach (var cp in _funcList.FindVisualChildren<ContentPresenter>())
        {
            if (ReferenceEquals(cp.DataContext, row))
                return cp.FindVisualChildren<Border>().FirstOrDefault();
        }
        return null;
    }

    private bool IsOverInteractive(DependencyObject? v)
    {
        while (v != null && !ReferenceEquals(v, _funcList))
        {
            if (v is CheckBox or Button or ComboBox or TextBox or Slider) return true;
            v = VisualTreeHelper.GetParent(v);
        }
        return false;
    }

    private ActionRow? RowFromVisual(DependencyObject? v)
    {
        while (v != null && !ReferenceEquals(v, _funcList))
        {
            if (v is FrameworkElement fe && fe.DataContext is ActionRow r) return r;
            v = VisualTreeHelper.GetParent(v);
        }
        return null;
    }

    private ActionRow? RowFromPoint(Point p)
        => RowFromVisual(_funcList.InputHitTest(p) as DependencyObject);

    private void CommitOrder()
    {
        AppState.Settings.EnabledActions = _rows.Where(r => r.IsOn).Select(r => r.Id).ToList();
        AppState.RaiseChanged();
    }

    private void RebuildPreview()
    {
        _previewPanel.Children.Clear();
        var gripIcon = Ui.MakeIcon("Icon.grip", 16, 2);
        var gripHost = new Border { Child = gripIcon, Padding = new Thickness(0, 4, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
        _previewPanel.Children.Add(gripHost);

        foreach (var row in _rows.Where(r => r.IsOn))
        {
            var cell = new Border { Child = Ui.MakeIcon(row.Def.IconKey, 22), Width = 48, Height = 48, CornerRadius = new CornerRadius(6), Margin = new Thickness(2) };
            cell.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Elevated");
            _previewPanel.Children.Add(cell);
        }
    }

    // ── 모양 탭 ───────────────────────────────────────────────

    private FrameworkElement BuildAppearancePage()
    {
        var a = AppState.Settings.Appearance;
        var sp = new StackPanel { MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left };

        sp.Children.Add(SectionTitle("테마"));
        var themeCombo = new ComboBox { SelectedIndex = Array.IndexOf(new[] { "dark", "light", "system", "highcontrast" }, a.Theme), Width = 220, Margin = new Thickness(0, 4, 0, 20) };
        themeCombo.Items.Add("다크"); themeCombo.Items.Add("라이트"); themeCombo.Items.Add("시스템"); themeCombo.Items.Add("고대비");
        themeCombo.SelectionChanged += (s, e) =>
        {
            AppState.Settings.Appearance.Theme = ((string?)themeCombo.SelectedItem) switch
            {
                "라이트" => "light", "시스템" => "system", "고대비" => "highcontrast", _ => "dark"
            };
            AppState.RaiseChanged();
        };
        sp.Children.Add(themeCombo);

        sp.Children.Add(SectionTitle("버튼 크기"));
        var sizePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 20) };
        foreach (int size in new[] { 44, 56, 72 })
        {
            int s = size;
            var rb = new RadioButton
            {
                Content = $"{size}px",
                Margin = new Thickness(0, 0, 16, 0),
                IsChecked = a.ButtonSize == size
            };
            rb.SetResourceReference(RadioButton.ForegroundProperty, "Brush.Fg.Primary");
            rb.Checked += (_, _) => { AppState.Settings.Appearance.ButtonSize = s; AppState.RaiseChanged(); };
            sizePanel.Children.Add(rb);
        }
        sp.Children.Add(sizePanel);

        sp.Children.Add(SectionTitle("라벨 표시"));
        sp.Children.Add(ToggleRowUi("아이콘 아래 한글 라벨 표시", a.ShowLabels, v => { AppState.Settings.Appearance.ShowLabels = v; AppState.RaiseChanged(); }));

        sp.Children.Add(SectionTitle("바 방향"));
        var orientCombo = new ComboBox { SelectedIndex = AppState.Settings.Position.Orientation == "vertical" ? 0 : 1, Width = 220, Margin = new Thickness(0, 4, 0, 20) };
        orientCombo.Items.Add("세로"); orientCombo.Items.Add("가로");
        orientCombo.SelectionChanged += (s, e) =>
        {
            AppState.Settings.Position.Orientation = orientCombo.SelectedIndex == 0 ? "vertical" : "horizontal";
            AppState.RaiseChanged();
        };
        sp.Children.Add(orientCombo);

        sp.Children.Add(SectionTitle("불투명도"));
        var opacityHeader = new Grid { Margin = new Thickness(0, 4, 0, 2) };
        opacityHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        opacityHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var opacityName = Ui.Txt("플로팅 바 불투명도", "Txt.BodySecondary");
        opacityName.FontSize = 12;
        Grid.SetColumn(opacityName, 0);
        opacityHeader.Children.Add(opacityName);
        var opacityValue = Ui.Txt($"{Math.Round(AppState.Settings.Appearance.Opacity * 100)}%", "Txt.Body");
        opacityValue.FontWeight = FontWeights.SemiBold;
        Grid.SetColumn(opacityValue, 1);
        opacityHeader.Children.Add(opacityValue);
        sp.Children.Add(opacityHeader);

        var slider = new Slider
        {
            Minimum = 20, Maximum = 100, Width = 300,
            Value = Math.Round(AppState.Settings.Appearance.Opacity * 100),
            Style = Ui.TryStyle("WftHSlider"),
            Margin = new Thickness(0, 4, 0, 4),
            IsSnapToTickEnabled = true,
            TickFrequency = 5
        };
        slider.ValueChanged += (s, e) =>
        {
            AppState.Settings.Appearance.Opacity = e.NewValue / 100.0;
            opacityValue.Text = $"{Math.Round(e.NewValue)}%";
            _bar.ApplyOpacity();
        };
        sp.Children.Add(slider);
        var sliderLabels = new Grid { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        sliderLabels.ColumnDefinitions.Add(new ColumnDefinition());
        sliderLabels.ColumnDefinitions.Add(new ColumnDefinition());
        var sl20 = Ui.Txt("20% (옅게)", "Txt.BodySecondary"); sl20.FontSize = 11;
        var sl100 = Ui.Txt("100% (진하게)", "Txt.BodySecondary"); sl100.FontSize = 11; sl100.HorizontalAlignment = HorizontalAlignment.Right;
        sliderLabels.Children.Add(sl20); Grid.SetColumn(sl20, 0);
        sliderLabels.Children.Add(sl100); Grid.SetColumn(sl100, 1);
        sliderLabels.Margin = new Thickness(0, 0, 0, 8);
        sp.Children.Add(sliderLabels);
        sp.Children.Add(ToggleRowUi("터치하면 선명하게", AppState.Settings.Appearance.OpacityOnHover, v => { AppState.Settings.Appearance.OpacityOnHover = v; _bar.ApplyOpacity(); AppState.Save(); }));

        var sv = new ScrollViewer { Content = sp };
        return sv;
    }

    // ── 동작 탭 ───────────────────────────────────────────────

    private FrameworkElement BuildBehaviorPage()
    {
        var b = AppState.Settings.Behavior;
        var sp = new StackPanel { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };

        sp.Children.Add(SectionTitle("패널 자동 닫힘"));
        var acCombo = new ComboBox { SelectedIndex = Array.IndexOf(new[] { 0, 2, 3, 5 }, b.AutoClosePanelSec), Width = 220, Margin = new Thickness(0, 4, 0, 20) };
        acCombo.Items.Add("끄기"); acCombo.Items.Add("2초"); acCombo.Items.Add("3초"); acCombo.Items.Add("5초");
        acCombo.SelectionChanged += (s, e) =>
        {
            AppState.Settings.Behavior.AutoClosePanelSec = acCombo.SelectedIndex switch { 1 => 2, 2 => 3, 3 => 5, _ => 0 };
            AppState.Save();
        };
        sp.Children.Add(acCombo);

        sp.Children.Add(SectionTitle("표시/숨김 전역 단축키"));
        sp.Children.Add(ToggleRowUi($"전역 단축키 사용 ({b.ToggleHotkey})", b.HotkeyEnabled, v => { AppState.Settings.Behavior.HotkeyEnabled = v; AppState.RaiseChanged(); }));
        var hkCombo = new ComboBox { Width = 220, Margin = new Thickness(0, 4, 0, 20) };
        foreach (string hk in new[] { "Ctrl+Alt+Space", "Ctrl+Alt+Q", "Ctrl+Shift+F1" }) hkCombo.Items.Add(hk);
        hkCombo.SelectedItem = b.ToggleHotkey;
        hkCombo.SelectionChanged += (s, e) =>
        {
            if (hkCombo.SelectedItem is string hk) { AppState.Settings.Behavior.ToggleHotkey = hk; AppState.RaiseChanged(); }
        };
        sp.Children.Add(hkCombo);

        sp.Children.Add(SectionTitle("스냅 레이아웃 방식"));
        var snapCombo = new ComboBox { SelectedIndex = b.SnapLayoutMode == "windows" ? 1 : 0, Width = 320, Margin = new Thickness(0, 4, 0, 20) };
        snapCombo.Items.Add("자체 패널 (Windows 10/11 모두 사용 가능)"); snapCombo.Items.Add("Windows 기본 (Win11, Win+Z)");
        snapCombo.SelectionChanged += (s, e) =>
        {
            AppState.Settings.Behavior.SnapLayoutMode = snapCombo.SelectedIndex == 1 ? "windows" : "custom";
            AppState.Save();
        };
        sp.Children.Add(snapCombo);

        sp.Children.Add(SectionTitle("시작 프로그램"));
        _startupStatus = Ui.Txt("", "Txt.BodySecondary");
        sp.Children.Add(_startupStatus);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        btnRow.Children.Add(Ui.MakeWindowButton("시작 프로그램에 추가", AddToStartup, accent: true));
        btnRow.Children.Add(new Border { Width = 8 });
        btnRow.Children.Add(Ui.MakeWindowButton("시작 프로그램에서 제거", RemoveFromStartup));
        sp.Children.Add(btnRow);
        UpdateStartupStatus();

        var sv = new ScrollViewer { Content = sp };
        return sv;
    }

    private void AddToStartup()
    {
        if (StartupEntry.IsRegistered())
        {
            App.Toast.Show("이미 시작 프로그램에 추가되어 있습니다");
        }
        else if (StartupEntry.TryAdd())
        {
            AppState.Settings.Behavior.StartWithWindows = true;
            AppState.Save();
            App.Toast.Show("시작 프로그램에 추가했습니다");
        }
        else
        {
            App.Toast.Show("시작 프로그램에 추가하지 못했습니다");
        }
        UpdateStartupStatus();
    }

    private void RemoveFromStartup()
    {
        if (!StartupEntry.IsRegistered())
        {
            App.Toast.Show("시작 프로그램에 등록되어 있지 않습니다");
        }
        else
        {
            StartupEntry.Remove();
            AppState.Settings.Behavior.StartWithWindows = false;
            AppState.Save();
            App.Toast.Show("시작 프로그램에서 제거했습니다");
        }
        UpdateStartupStatus();
    }

    private void UpdateStartupStatus()
    {
        bool registered = StartupEntry.IsRegistered();
        _startupStatus.Text = registered
            ? "현재 시작 프로그램에 등록되어 있습니다. Windows 시작 시 자동으로 실행됩니다."
            : "현재 시작 프로그램에 등록되어 있지 않습니다. 추가하면 Windows 시작 시 자동으로 실행됩니다.";
    }

    // ── 정보 탭 ───────────────────────────────────────────────

    private FrameworkElement BuildInfoPage()
    {
        var sp = new StackPanel { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };

        var name = Ui.Txt("전자칠판 플로팅 툴 (WFT)", "Txt.Title");
        name.Margin = new Thickness(0, 0, 0, 4);
        sp.Children.Add(name);
        var ver = Ui.Txt($"버전 {ThisVersion()}", "Txt.BodySecondary");
        ver.Margin = new Thickness(0, 0, 0, 20);
        sp.Children.Add(ver);

        var btns = new StackPanel { Orientation = Orientation.Vertical };
        var manualBtn = Ui.MakeWindowButton("사용 설명서 보기 (PDF)", () => App.OpenManual(), accent: true);
        manualBtn.HorizontalAlignment = HorizontalAlignment.Left;
        manualBtn.Margin = new Thickness(0, 0, 0, 8);
        btns.Children.Add(manualBtn);

        var logBtn = Ui.MakeWindowButton("로그 폴더 열기", () => OpenFolder(Core.Log.FolderOf()));
        logBtn.HorizontalAlignment = HorizontalAlignment.Left;
        logBtn.Margin = new Thickness(0, 0, 0, 8);
        btns.Children.Add(logBtn);

        var setBtn = Ui.MakeWindowButton("설정 폴더 열기", () => OpenFolder(AppState.SettingsFolder));
        setBtn.HorizontalAlignment = HorizontalAlignment.Left;
        setBtn.Margin = new Thickness(0, 0, 0, 8);
        btns.Children.Add(setBtn);

        var resetBtn = Ui.MakeWindowButton("기본값으로 되돌리기", ResetSettings);
        resetBtn.HorizontalAlignment = HorizontalAlignment.Left;
        btns.Children.Add(resetBtn);

        sp.Children.Add(btns);

        var lic = Ui.Txt("창 제목과 탭 제목은 화면에만 표시되며 저장·전송하지 않습니다.\n네트워크 통신은 없습니다.", "Txt.BodySecondary");
        lic.Margin = new Thickness(0, 24, 0, 0);
        lic.TextWrapping = TextWrapping.Wrap;
        sp.Children.Add(lic);

        var sv = new ScrollViewer { Content = sp };
        return sv;
    }

    private static string ThisVersion()
    {
        try { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"; }
        catch { return "1.0.0"; }
    }

    private void ResetSettings()
    {
        var result = MessageBox.Show(this, "모든 설정을 기본값으로 되돌립니다. 계속하시겠습니까?",
            "확인", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;
        AppState.Reset();
        AppState.RaiseChanged();
        LoadRows();
        App.Toast.Show("설정을 기본값으로 되돌렸습니다");
        Close();
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex) { Core.Log.Error("OpenFolder", ex); }
    }

    // ── 공통 조각 ─────────────────────────────────────────────

    private static TextBlock SectionTitle(string t)
    {
        var tb = Ui.Txt(t, "Txt.Title");
        tb.FontSize = 15;
        return tb;
    }

    private static Grid ToggleRowUi(string label, bool value, Action<bool> onChange)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tb = Ui.Txt(label, "Txt.Body");
        tb.VerticalAlignment = VerticalAlignment.Center;
        tb.Margin = new Thickness(0, 6, 0, 6);
        Grid.SetColumn(tb, 0);
        g.Children.Add(tb);
        var cb = new CheckBox { IsChecked = value, Style = Ui.TryStyle("WftToggle"), VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => onChange(true);
        cb.Unchecked += (_, _) => onChange(false);
        Grid.SetColumn(cb, 1);
        g.Children.Add(cb);
        return g;
    }
}
