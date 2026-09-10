using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WFT.Interop;

namespace WFT.UI;

/// <summary>토스트 (WFT_UI §4.9): 320x56, 2.5초, 동시 1개, 사운드 없음</summary>
public sealed class ToastService
{
    private Window? _toast;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private readonly FloatingBarWindow _bar;

    public ToastService(FloatingBarWindow bar)
    {
        _bar = bar;
        _timer.Tick += (_, _) => Close();
    }

    public void Show(string message)
    {
        Close();
        var text = Ui.Txt(message, "Txt.Body");
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Center;

        var border = new Border
        {
            Padding = new Thickness(14, 8, 14, 8),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            MaxWidth = 320,
            Child = text
        };
        border.SetResourceReference(Border.BackgroundProperty, "Brush.Bg.Elevated");
        border.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var w = new Window
        {
            Content = border,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Focusable = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize
        };
        w.SourceInitialized += (s, e) =>
        {
            var src = (HwndSource)PresentationSource.FromVisual(w);
            NativeMethods.ApplyNoActivateStyle(src.Handle);
        };

        bool barOnRight = _bar.Left + _bar.Width / 2 >
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        w.Left = barOnRight
            ? Math.Max(SystemParameters.VirtualScreenLeft + 8, _bar.Left - 340)
            : Math.Min(_bar.Left + _bar.Width + 8, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 330);
        w.Top = Math.Max(SystemParameters.VirtualScreenTop + 8, _bar.Top);
        w.Show();
        _toast = w;
        _timer.Start();
    }

    private void Close()
    {
        _timer.Stop();
        if (_toast != null)
        {
            var t = _toast;
            _toast = null;
            try { t.Close(); } catch { }
        }
    }
}
