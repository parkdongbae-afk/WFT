using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Automation;
using WFT.Interop;

namespace WFT.Core;

public sealed class BrowserTab
{
    public string Title { get; init; } = "";
    public bool IsSelected { get; init; }
    public AutomationElement Element { get; init; } = null!;
}

/// <summary>Chrome/Edge 탭 열거·전환 (F-03, UI Automation)</summary>
public static class BrowserTabs
{
    /// <summary>탭 열거. 실패 시 null. 재시도 3회(200ms) — 브라우저 접근성 트리 지연 로딩 대응.</summary>
    public static List<BrowserTab>? EnumerateTabs(nint hwnd)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                var col = root.FindAll(TreeScope.Descendants, cond);
                if (col.Count == 0)
                {
                    Thread.Sleep(200); // WM_GETOBJECT 트리거 후 재시도
                    continue;
                }
                var tabs = new List<BrowserTab>(col.Count);
                foreach (AutomationElement el in col)
                {
                    string name = "";
                    bool sel = false;
                    try
                    {
                        name = el.Current.Name ?? "";
                        sel = (bool)el.GetCurrentPropertyValue(SelectionItemPattern.IsSelectedProperty, true);
                    }
                    catch { }
                    tabs.Add(new BrowserTab { Title = name, IsSelected = sel, Element = el });
                }
                return tabs;
            }
            catch (Exception ex)
            {
                Log.Error($"BrowserTabs.EnumerateTabs({attempt})", ex);
                Thread.Sleep(200);
            }
        }
        return null;
    }

    public static string? GetWindowName(nint hwnd)
    {
        try { return AutomationElement.FromHandle(hwnd).Current.Name; }
        catch { return null; }
    }

    public static void SelectTab(BrowserTab tab)
    {
        try
        {
            if (tab.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pat))
                ((SelectionItemPattern)pat).Select();
        }
        catch (Exception ex) { Log.Error("BrowserTabs.SelectTab", ex); }
    }
}
