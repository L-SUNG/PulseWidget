namespace PulseWidget.Core;

public sealed record WidgetTheme(
    string Id, string Name, string Description,
    string Surface, string Panel, string Border, string Text, string Secondary, string Muted,
    string OpenAI, string Claude, string Accent, string Working, string Waiting,
    string Track, string Control, string Hover, string Pressed)
{
    public string Family { get; init; } = "기본";
    public string? Variant { get; init; }
    public string ChoiceName => Variant ?? Name;
    public double WidgetRadius { get; init; } = 7;
    public double DialogRadius { get; init; } = 16;
    public double CardRadius { get; init; } = 10;
    public double ControlRadius { get; init; } = 6;
    public string? SurfaceHighlight { get; init; }
    public string? PanelHighlight { get; init; }
    public double CardShadow { get; init; }
}

public static class ThemeCatalog
{
    public const string DefaultId = "graphite";
    public static IReadOnlyList<WidgetTheme> All { get; } = Array.AsReadOnly(new[]
    {
        new WidgetTheme("graphite", "그라파이트", "차콜 배경과 민트 포인트 · 기본",
            "#181D27", "#232B39", "#445065", "#F3F6FC", "#C0CDDF", "#A4B2C8",
            "#9DE9D3", "#EDB89F", "#9DE9D3", "#8EE4BA", "#F2C36D",
            "#445065", "#303B4E", "#3E4D64", "#4A5D79"),
        new WidgetTheme("paper", "페이퍼", "밝은 아이보리와 또렷한 잉크 색상",
            "#F6F4EF", "#FFFFFF", "#B7B9B5", "#202936", "#475366", "#586478",
            "#12684F", "#99451E", "#275EB3", "#126442", "#8A5100",
            "#DDE2E8", "#E7EBF0", "#D4DEEC", "#BFCEE3"),
        new WidgetTheme("midnight", "미드나이트", "짙은 네이비와 아이스 블루",
            "#0E1B30", "#172B45", "#385575", "#EEF6FF", "#C0D5EE", "#9FBADB",
            "#7EE5E1", "#FFD2A6", "#91C8FF", "#8FE6BC", "#FFD080",
            "#36516F", "#223D60", "#2E507A", "#3B6392"),
        new WidgetTheme("forest", "포레스트", "깊은 녹색과 세이지 포인트",
            "#14241F", "#1E342B", "#456454", "#F0F6EF", "#C6D9C9", "#A6C2AE",
            "#ACE6BB", "#EBCD9C", "#C5DEA1", "#9EE3B1", "#F0CF83",
            "#415F4D", "#2B4839", "#3A5B48", "#4A6D57"),
        new WidgetTheme("plum", "플럼", "차분한 자주색과 라벤더 포인트",
            "#261C32", "#352742", "#655075", "#F8F1FC", "#DCCCE8", "#BFAACF",
            "#A9E6D2", "#F0BBDA", "#D7B5F5", "#A9E6CA", "#F5D18C",
            "#5C486B", "#493455", "#5B426B", "#6C507D"),
        new WidgetTheme("claude-light", "Claude · 라이트", "따뜻한 크림색과 테라코타 포인트",
            "#F5F2EB", "#FFFDFA", "#CEC5B8", "#302D29", "#59534B", "#6F665D",
            "#365D50", "#98472D", "#98472D", "#316147", "#83530D",
            "#DED6CA", "#EAE3D8", "#DED3C3", "#D1C1AC")
            { Family = "Claude", Variant = "라이트", WidgetRadius = 9, CardRadius = 12, ControlRadius = 8 },
        new WidgetTheme("claude-dark", "Claude · 다크", "따뜻한 차콜과 부드러운 살구색",
            "#282623", "#34312D", "#5F584F", "#F5F0E8", "#D3C9BC", "#B7AA99",
            "#A2CCBA", "#E9AA8D", "#E9AA8D", "#A4CFAD", "#E7C281",
            "#595148", "#443D35", "#54493E", "#645442")
            { Family = "Claude", Variant = "다크", WidgetRadius = 9, CardRadius = 12, ControlRadius = 8 },
        new WidgetTheme("openai-light", "OpenAI · 라이트", "깨끗한 흰색과 절제된 흑백 대비",
            "#FFFFFF", "#F4F4F4", "#D1D1D1", "#181818", "#454545", "#676767",
            "#222222", "#874B34", "#146B55", "#166746", "#845600",
            "#DDDDDD", "#ECECEC", "#DFDFDF", "#CFCFCF")
            { Family = "OpenAI", Variant = "라이트", WidgetRadius = 12, DialogRadius = 20, CardRadius = 14, ControlRadius = 14 },
        new WidgetTheme("openai-dark", "OpenAI · 다크", "중성 회색의 레이어와 선명한 흰색",
            "#202020", "#2D2D2D", "#535353", "#F5F5F5", "#D4D4D4", "#ADADAD",
            "#F1F1F1", "#E0B197", "#8BD7BF", "#8BD7AB", "#EDC580",
            "#505050", "#3A3A3A", "#484848", "#565656")
            { Family = "OpenAI", Variant = "다크", WidgetRadius = 12, DialogRadius = 20, CardRadius = 14, ControlRadius = 14 },
        new WidgetTheme("material-light", "Material · 라이트", "라벤더 톤의 둥근 카드와 알약형 버튼",
            "#F7F2FA", "#EDE5F3", "#BCB0C9", "#211C28", "#51465F", "#645870",
            "#365A49", "#8B433E", "#674498", "#28613F", "#835100",
            "#D6C7E4", "#E1D2F0", "#D3BEE9", "#C2A6DF")
            { Family = "Material", Variant = "라이트", WidgetRadius = 16, DialogRadius = 24, CardRadius = 18, ControlRadius = 20, CardShadow = .12 },
        new WidgetTheme("material-dark", "Material · 다크", "보랏빛 표면과 파스텔 포인트의 카드",
            "#211B29", "#302738", "#60516F", "#F1E8F6", "#D5C6E1", "#B9A8C7",
            "#A7D9BE", "#F4B4AB", "#D4B5FC", "#A5D6B4", "#F1CB8A",
            "#574663", "#50405F", "#604C72", "#715A85")
            { Family = "Material", Variant = "다크", WidgetRadius = 16, DialogRadius = 24, CardRadius = 18, ControlRadius = 20, CardShadow = .18 },
        new WidgetTheme("fluent-light", "Fluent · 라이트", "Windows 11의 차분한 회색 레이어와 파란색",
            "#EDF1F6", "#FAFBFD", "#BEC8D4", "#192431", "#405269", "#5B6B7D",
            "#175C50", "#8D4C2E", "#0067B5", "#20603D", "#825400",
            "#D3DCE8", "#E2EAF4", "#D4E3F5", "#BCD5F0")
            { Family = "Fluent", Variant = "라이트", WidgetRadius = 8, DialogRadius = 8, CardRadius = 8, ControlRadius = 4, SurfaceHighlight = "#F8FAFD", CardShadow = .08 },
        new WidgetTheme("fluent-dark", "Fluent · 다크", "짙은 청회색 레이어와 아이스 블루",
            "#202833", "#2C3541", "#526175", "#F4F7FC", "#CBD8E8", "#A5B7CD",
            "#9DDCCB", "#EDBC9F", "#86C9FF", "#9BDBB2", "#EACC8E",
            "#4C5D72", "#384658", "#455A73", "#516D8C")
            { Family = "Fluent", Variant = "다크", WidgetRadius = 8, DialogRadius = 8, CardRadius = 8, ControlRadius = 4, SurfaceHighlight = "#303C4C", CardShadow = .16 },
        new WidgetTheme("apple-light", "Apple HIG · 라이트", "밝은 회색의 그룹과 흰색 카드, 시스템 블루",
            "#F2F2F7", "#FFFFFF", "#C9CAD3", "#1C1C1E", "#48484F", "#686873",
            "#23604E", "#985135", "#005EC4", "#226534", "#8A5200",
            "#DCDDE5", "#E5E5ED", "#D5DFEF", "#BED0EA")
            { Family = "Apple HIG", Variant = "라이트", WidgetRadius = 12, DialogRadius = 22, CardRadius = 14, ControlRadius = 9 },
        new WidgetTheme("apple-dark", "Apple HIG · 다크", "검정에 가까운 배경과 분리된 회색 카드",
            "#141416", "#252528", "#505057", "#F5F5F7", "#D1D1D8", "#ACACB8",
            "#97D6BD", "#EFBB9E", "#75B8FF", "#8ED9A1", "#F1C66D",
            "#484850", "#38383E", "#464953", "#555D6D")
            { Family = "Apple HIG", Variant = "다크", WidgetRadius = 12, DialogRadius = 22, CardRadius = 14, ControlRadius = 9 }
    });

    public static WidgetTheme Find(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
