namespace WFT.Core;

/// <summary>액션 정의 (F-01 ~ F-08, WFT_UI §5.2 아이콘·라벨 매핑)</summary>
public sealed record ActionDef(string Id, string Label, string IconKey, bool NeedsBrowser, string Tooltip);

public static class ActionDefs
{
    public static readonly Dictionary<string, ActionDef> All = new()
    {
        ["switchWindow"] = new("switchWindow", "창 전환", "Icon.switchWindow", false, "열려 있는 창 목록을 보여 줍니다"),
        ["prevWindow"]   = new("prevWindow",   "이전 창",   "Icon.prevWindow",   false, "이전 창으로 전환합니다"),
        ["nextWindow"]   = new("nextWindow",   "다음 창",   "Icon.nextWindow",   false, "다음 창으로 전환합니다"),
        ["tabList"]      = new("tabList",      "탭 목록",   "Icon.tabList",      true,  "브라우저 탭 목록을 보여 줍니다"),
        ["prevTab"]      = new("prevTab",      "이전 탭",   "Icon.prevTab",      true,  "이전 탭으로 전환합니다"),
        ["nextTab"]      = new("nextTab",      "다음 탭",   "Icon.nextTab",      true,  "다음 탭으로 전환합니다"),
        ["taskView"]     = new("taskView",     "작업 보기", "Icon.taskView",     false, "작업 보기를 엽니다"),
        ["snapLeft"]     = new("snapLeft",     "좌",        "Icon.snapLeft",     false, "왼쪽으로 스냅합니다"),
        ["snapRight"]    = new("snapRight",    "우",        "Icon.snapRight",    false, "오른쪽으로 스냅합니다"),
        ["snapTop"]      = new("snapTop",      "상",        "Icon.snapTop",      false, "최대화합니다"),
        ["snapBottom"]   = new("snapBottom",   "하",        "Icon.snapBottom",   false, "이전 크기로 복원합니다"),
        ["snapLayout"]   = new("snapLayout",   "레이아웃",  "Icon.snapLayout",   false, "스냅 레이아웃으로 배치합니다"),
        ["desktopMenu"]  = new("desktopMenu",  "화면",      "Icon.desktop",      false, "화면 전환 메뉴를 엽니다 (가상 화면)"),
    };

    /// <summary>설정 창 그룹 (WFT_UI §7.2)</summary>
    public static readonly (string Group, string[] Ids)[] Groups =
    {
        ("창 관리", new[] { "switchWindow", "prevWindow", "nextWindow", "taskView", "snapLeft", "snapRight", "snapTop", "snapBottom", "snapLayout" }),
        ("화면", new[] { "desktopMenu" }),
        ("브라우저", new[] { "tabList", "prevTab", "nextTab" }),
    };
}
