using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PulseWidget;
using PulseWidget.Core;
using PulseWidget.Theming;

internal static class UiFeatureTests
{
    internal static void Run(Action<bool, string> check)
    {
        var now = DateTimeOffset.UtcNow;
        var strip = new WidgetStrip { Width = 546, Height = 42 };
        void Render(UsageWindow? weekly, UsageWindow? five, DateTimeOffset? received) => strip.Render(
            new(weekly, null, received, "test"), new(weekly, five, received, "test"), new(WorkState.Idle), new(WorkState.Working), null);
        ProgressBar Bar(string name) => (ProgressBar)strip.FindName(name);
        Render(new(36, now.AddDays(1)), new(48, now.AddHours(1)), now);
        check(Bar("CodexWeekBar").Value == 36 && Bar("ClaudeWeekBar").Value == 36 && Bar("ClaudeFiveBar").Value == 48, "three strip bars reflect their own weekly and five-hour usage");
        Render(new(0, null), new(100, null), now);
        check(Bar("CodexWeekBar").Opacity == 1 && Bar("CodexWeekBar").Value == 0 && Bar("ClaudeFiveBar").Value == 100, "zero and full usage are valid bar states");
        Render(null, new(42, now.AddSeconds(-1)), now);
        check(Bar("CodexWeekBar").Value == 0 && Bar("CodexWeekBar").Opacity == .3 && AutomationProperties.GetHelpText(Bar("CodexWeekBar")).Contains("미수신") && AutomationProperties.GetHelpText(Bar("ClaudeFiveBar")).Contains("경과"), "missing and expired data have empty subdued bars with explicit explanations");
        Render(new(63, null), null, now.AddMinutes(-20));
        check(Bar("CodexWeekBar").Value == 63 && Bar("CodexWeekBar").Opacity == .65 && AutomationProperties.GetHelpText(Bar("CodexWeekBar")).Contains("오래된"), "stale usage remains visible and identified");
        Render(new(double.NaN, null), new(120, null), now);
        check(Bar("CodexWeekBar").Value == 0 && Bar("ClaudeFiveBar").Value == 100, "invalid and out-of-range values cannot break bar rendering");

        var settings = new Settings { ThemeId = CustomTheme.Id, PositionPercent = 37.25, CustomTheme = new() { Colors = new() { ["Surface"] = "#112233", ["OpenAI"] = "#AA3355", ["Text"] = "#EEDDCC" } } };
        var before = JsonSerializer.Serialize(settings);
        var appColor = Application.Current.Resources["Pulse.Surface"];
        var editor = new ThemeEditorWindow(settings);
        try
        {
            var detail = PercentPositionTests.FindAll<DetailView>(editor).Single();
            var previewStrip = PercentPositionTests.FindAll<WidgetStrip>(editor).Single();
            check(PercentPositionTests.FindAll<ProgressBar>(detail).Count() == 3 && PercentPositionTests.FindAll<TextBox>(detail).Any() && PercentPositionTests.FindAll<Button>(detail).Any(b => Equals(b.Content, "디스플레이 설정")), "custom preview includes the real full detail view and usage bars");
            check(((SolidColorBrush)detail.FindResource("Pulse.Surface")).Color.ToString() == "#FF112233" && ((SolidColorBrush)previewStrip.FindResource("Pulse.OpenAI")).Color.ToString() == "#FFAA3355", "draft colors reach both detail and strip previews");
            var reset = PercentPositionTests.FindAll<Button>(editor).Single(b => Equals(b.Content, "기준 테마의 기본색")); reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((SolidColorBrush)detail.FindResource("Pulse.Surface")).Color == ThemeManager.Brush(ThemeCatalog.Find(ThemeCatalog.DefaultId).Surface).Color, "draft changes update the embedded detail without applying");
            var leakedCopy = editor.Result; leakedCopy.Colors["Surface"] = "#000000";
            check(JsonSerializer.Serialize(settings) == before && ReferenceEquals(Application.Current.Resources["Pulse.Surface"], appColor) && !editor.Result.Colors.ContainsKey("Surface"), "editing and cancelling previews never mutate live settings or application resources");
        }
        finally { editor.Close(); }
    }

    internal static void RenderPreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string theme in new[] { "claude-dark", "openai-light" })
        {
            ThemeManager.Apply(theme);
            var editor = new ThemeEditorWindow(new Settings { ThemeId = theme, PositionPercent = 37.25 });
            try
            {
                Save((FrameworkElement)editor.Content, 1000, 780, Path.Combine(directory, theme + "-editor.png"));
                Save((FrameworkElement)editor.Content, 600, 780, Path.Combine(directory, theme + "-compact-editor.png"));
                var now = DateTimeOffset.UtcNow;
                var settings = new Settings { ThemeId = theme, PositionPercent = 37.25 };
                var detail = new DetailView(settings, () => { }, () => { }, () => { }, _ => { }, () => { }, () => new ContextMenu());
                detail.Update(new(new UsageWindow(36, now.AddDays(2)), null, now, "Codex 계정에서 갱신"),
                    new(new UsageWindow(22, now.AddDays(4)), new UsageWindow(48, now.AddHours(3)), now, "Claude Desktop에서 갱신"),
                    new(WorkState.Working), new(WorkState.Waiting), new(24, 58, 18.6, 32));
                Save(detail, 402, 740, Path.Combine(directory, theme + "-detail.png"));
            }
            finally { editor.Close(); }
        }
    }
    private static void Save(FrameworkElement visual, int width, int height, string path)
    {
        visual.Measure(new Size(width, height)); visual.Arrange(new Rect(0, 0, width, height)); visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)Application.Current.Resources["Pulse.Surface"], null, new Rect(0, 0, width, height));
        bitmap.Render(background); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
        Console.WriteLine(path);
    }
}
