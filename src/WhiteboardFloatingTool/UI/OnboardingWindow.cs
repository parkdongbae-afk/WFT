using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WFT.Core;

namespace WFT.UI;

/// <summary>첫 실행 온보딩 (WFT_UI §7.1): 3단계, 60초 이내</summary>
public sealed class OnboardingWindow : Window
{
    private int _step;
    private readonly Action _onDone;

    private readonly (string Title, string Body)[] _steps =
    {
        ("끌어서 옮기세요", "위쪽 손잡이(≡)를 끌면 화면 어느 곳으로든\n플로팅 바를 옮길 수 있습니다."),
        ("접어서 숨기세요", "접기 버튼을 누르면 작은 원 하나만 남습니다.\n다시 탭하면 펼쳐집니다."),
        ("필요한 기능만 고르세요", "설정에서 사용할 기능과 순서를 정할 수 있습니다.\n설정 버튼은 언제나 맨 아래에 있습니다."),
    };

    public OnboardingWindow(Action onDone)
    {
        _onDone = onDone;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        var card = new Border
        {
            Width = 380,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1)
        };
        card.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        Content = card;
        BuildStep();
    }

    private void BuildStep()
    {
        if (Content is not Border card) return;
        card.Child = null;
        var (title, body) = _steps[_step];

        var sp = new StackPanel();
        var t = Ui.Txt(title, "Txt.Title");
        t.FontSize = 24;
        t.Margin = new Thickness(0, 0, 0, 12);
        sp.Children.Add(t);

        var b = Ui.Txt(body, "Txt.Body");
        b.TextWrapping = TextWrapping.Wrap;
        b.LineHeight = 22;
        b.Margin = new Thickness(0, 0, 0, 24);
        sp.Children.Add(b);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var skip = Ui.Txt("건너뛰기", "Txt.BodySecondary");
        skip.Cursor = System.Windows.Input.Cursors.Hand;
        skip.MouseLeftButtonUp += (_, _) => Done();
        Grid.SetColumn(skip, 0);
        row.Children.Add(skip);

        var next = Ui.MakeWindowButton(_step == _steps.Length - 1 ? "바로 시작" : "다음", Next, accent: true);
        Grid.SetColumn(next, 2);
        row.Children.Add(next);

        sp.Children.Add(row);
        card.Child = sp;
    }

    private void Next()
    {
        if (_step < _steps.Length - 1) { _step++; BuildStep(); }
        else Done();
    }

    private void Done()
    {
        _onDone();
        Close();
    }
}
