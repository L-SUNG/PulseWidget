namespace PulseWidget.Core;

public sealed class CustomTheme
{
    public const string Id = "custom";
    public string BaseThemeId { get; set; } = ThemeCatalog.DefaultId;
    public Dictionary<string, string> Colors { get; set; } = new();
    public static IReadOnlyList<(string Key, string Label)> Roles { get; } = new[]
    {
        ("Surface", "위젯 배경"), ("Panel", "카드 배경"), ("Text", "주요 글자"),
        ("Secondary", "보조 글자"), ("Muted", "안내 글자"), ("Border", "테두리"),
        ("OpenAI", "OpenAI"), ("Claude", "Claude"), ("Accent", "강조 색상"),
        ("Working", "작업중"), ("Waiting", "승인 대기"), ("Track", "사용량 막대 배경"),
        ("Control", "버튼 배경"), ("Hover", "버튼 마우스 오버"), ("Pressed", "버튼 누름")
    };
    public CustomTheme Clone() => new() { BaseThemeId = BaseThemeId, Colors = new(Colors ?? new()) };
    public void Normalize()
    {
        BaseThemeId = ThemeCatalog.Find(BaseThemeId).Id;
        Colors = (Colors ?? new()).Where(p => Roles.Any(r => r.Key == p.Key) && IsColor(p.Value))
            .ToDictionary(p => p.Key, p => p.Value.ToUpperInvariant());
    }
    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    public WidgetTheme Resolve()
    {
        var t = ThemeCatalog.Find(BaseThemeId);
        string Color(string key, string fallback) => Colors is not null && Colors.TryGetValue(key, out var c) && IsColor(c) ? c : fallback;
        return t with
        {
            Id = Id, Name = "사용자 지정", Description = "직접 선택한 색상", Family = "사용자 지정", Variant = null,
            Surface = Color("Surface", t.Surface), Panel = Color("Panel", t.Panel), Border = Color("Border", t.Border),
            Text = Color("Text", t.Text), Secondary = Color("Secondary", t.Secondary), Muted = Color("Muted", t.Muted),
            OpenAI = Color("OpenAI", t.OpenAI), Claude = Color("Claude", t.Claude), Accent = Color("Accent", t.Accent),
            Working = Color("Working", t.Working), Waiting = Color("Waiting", t.Waiting), Track = Color("Track", t.Track),
            Control = Color("Control", t.Control), Hover = Color("Hover", t.Hover), Pressed = Color("Pressed", t.Pressed),
            SurfaceHighlight = null, PanelHighlight = null
        };
    }
    public static Dictionary<string, string> Palette(WidgetTheme t) => new()
    {
        ["Surface"] = t.Surface, ["Panel"] = t.Panel, ["Border"] = t.Border, ["Text"] = t.Text,
        ["Secondary"] = t.Secondary, ["Muted"] = t.Muted, ["OpenAI"] = t.OpenAI, ["Claude"] = t.Claude,
        ["Accent"] = t.Accent, ["Working"] = t.Working, ["Waiting"] = t.Waiting, ["Track"] = t.Track,
        ["Control"] = t.Control, ["Hover"] = t.Hover, ["Pressed"] = t.Pressed
    };
}
