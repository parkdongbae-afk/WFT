using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WFT.UI;

/// <summary>코드에서 컨트롤을 만들 때 공통 헬퍼</summary>
internal static class Ui
{
    public static Path MakeIcon(string iconKey, double size = 24, double stroke = 1.75)
    {
        var path = new Path
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = (Geometry)System.Windows.Application.Current.TryFindResource(iconKey)
                   ?? Geometry.Parse("M4 4 H20 V20 H4 Z"),
            Stroke = new SolidColorBrush(Colors.White),
            Fill = Brushes.Transparent
        };
        path.SetResourceReference(Shape.StrokeProperty, "Brush.Fg.Primary");
        return path;
    }

    /// <summary>아이콘+라벨 액션 버튼 (터치 다운 즉시 실행)</summary>
    public static Button MakeActionButton(string iconKey, string? label, string tooltip,
        double buttonSize, Action onExecute, bool showLabel = true)
    {
        object content;
        if (showLabel && label != null)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(MakeIcon(iconKey, buttonSize >= 56 ? 24 : 20));
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextAlignment = TextAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg.Secondary");
            tb.SetResourceReference(TextBlock.FontFamilyProperty, "Font.App");
            sp.Children.Add(tb);
            content = sp;
        }
        else
        {
            content = MakeIcon(iconKey, buttonSize >= 56 ? 28 : 22);
        }

        var btn = new Button
        {
            Width = buttonSize,
            Height = buttonSize,
            Content = content,
            ToolTip = tooltip,
            Style = (Style)System.Windows.Application.Current.TryFindResource("WftActionButton")
        };

        // 터치/마우스 다운 즉시 실행 (P1 원터치, 터치 다운 0ms 피드백)
        btn.PreviewTouchDown += (s, e) => { e.Handled = true; onExecute(); };
        btn.PreviewMouseLeftButtonDown += (s, e) => { e.Handled = true; onExecute(); };
        return btn;
    }

    public static TextBlock Txt(string text, string styleKey = "Txt.Body")
    {
        var tb = new TextBlock { Text = text };
        if (System.Windows.Application.Current.TryFindResource(styleKey) is Style st) tb.Style = st;
        return tb;
    }

    public static Border Card(double width)
    {
        var b = new Border
        {
            Width = width,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12)
        };
        b.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Panel");
        b.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        return b;
    }

    public static Separator HSep()
    {
        var s = new Separator { Height = 1, Margin = new Thickness(6, 4, 6, 4) };
        s.SetResourceReference(Separator.BackgroundProperty, "Brush.Border");
        return s;
    }

    public static CheckBox MakeToggle(bool isChecked, Action<bool> onToggle, string? label = null)
    {
        var cb = new CheckBox { IsChecked = isChecked, Style = TryStyle("WftToggle") };
        if (label != null)
        {
            var tb = Txt(label);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(tb);
            sp.Children.Add(cb);
            // 라벨 좌측, 토글 우측 (WFT_UI §4.8) — 배치는 호출자가 Grid로
        }
        cb.Checked += (_, _) => onToggle(true);
        cb.Unchecked += (_, _) => onToggle(false);
        return cb;
    }

    public static Style? TryStyle(string key)
        => System.Windows.Application.Current.TryFindResource(key) as Style;

    public static Button MakeWindowButton(string text, Action onClick, bool accent = false)
    {
        var b = new Button { Content = text, Style = TryStyle(accent ? "WftAccentButton" : "WftWindowButton") };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>그림자 없는 접이식 헤더 (팝업 패널)</summary>
    public static Grid PanelHeader(string title, Action onClose)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = Txt(title, "Txt.Title");
        t.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(t, 0);
        g.Children.Add(t);

        var close = MakeActionButton("Icon.close", null, "닫기", 32, onClose, false);
        close.Width = 32; close.Height = 32;
        Grid.SetColumn(close, 1);
        g.Children.Add(close);
        return g;
    }
}
