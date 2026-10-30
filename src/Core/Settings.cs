namespace PulseWidget.Core;

public sealed class Settings
{
    public double Offset { get; set; } = 164;
    public double? PositionPercent { get; set; }
    public double Scale { get; set; } = 1;
    public bool AboveTaskbar { get; set; }
    public bool AlwaysVisible { get; set; } = true;
    public string? CodexPath { get; set; }
    public string ThemeId { get; set; } = ThemeCatalog.DefaultId;
    public CustomTheme CustomTheme { get; set; } = new();
    public bool ShowOnAllDisplays { get; set; }
    public string? DisplayDeviceName { get; set; }
    public WidgetTheme ResolveTheme() => ThemeId == Core.CustomTheme.Id ? CustomTheme.Resolve() : ThemeCatalog.Find(ThemeId);
    public void Normalize()
    {
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .75, 1.5) : 1;
        Offset = double.IsFinite(Offset) ? Math.Max(0, Offset) : 164;
        PositionPercent = PositionPercent is { } percent && double.IsFinite(percent) ? HorizontalPosition.Clamp(percent) : null;
        CustomTheme ??= new(); CustomTheme.Normalize();
        ThemeId = string.Equals(ThemeId, Core.CustomTheme.Id, StringComparison.OrdinalIgnoreCase) ? Core.CustomTheme.Id : ThemeCatalog.Find(ThemeId).Id;
        if (string.IsNullOrWhiteSpace(DisplayDeviceName)) DisplayDeviceName = null;
    }
    public bool MigratePosition(int displayWidth, double dpiScale)
    {
        if (PositionPercent is not null) return false;
        // Offset was in DIPs; preserve the same physical pixel after conversion.
        PositionPercent = HorizontalPosition.FromPixels(displayWidth, (int)Math.Round(546 * Scale * dpiScale), Math.Truncate(Offset * dpiScale));
        return true;
    }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseWidget");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string ClaudeDirectory => Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
    public static string CodexDirectory => Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    public static Settings Load()
    {
        try
        {
            var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new();
            result.Normalize();
            return result;
        }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(SettingsPath + ".tmp", SettingsPath, true);
    }
}
