using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PulseWidget.Core;
using Forms = System.Windows.Forms;

namespace PulseWidget.Theming;

public sealed class ThemeEditorWindow : Window
{
    private CustomTheme draft;
    private readonly WidgetTheme current;
    private readonly Dictionary<string, (Border Swatch, TextBlock Hex)> fields = new();
    private readonly StackPanel preview = new();
    private readonly DetailView detailPreview;
    private readonly Settings previewSettings;
    private readonly TextBlock basis;
    public CustomTheme Result => draft.Clone();

    public ThemeEditorWindow(Settings settings)
    {
        current = settings.ResolveTheme();
        draft = settings.ThemeId == CustomTheme.Id || settings.CustomTheme.Colors.Count > 0 ? settings.CustomTheme.Clone()
            : new CustomTheme { BaseThemeId = current.Id, Colors = CustomTheme.Palette(current) };
        Title = "Pulse · 사용자 지정 색상"; Width = 1040; Height = 820; ShowInTaskbar = false;
        MinWidth = 600; MinHeight = 480; ResizeMode = ResizeMode.CanResize; FontFamily = new FontFamily("Segoe UI, Malgun Gothic");
        ThemeManager.Bind(this, BackgroundProperty, "Surface");
        var root = new DockPanel { Margin = new Thickness(22, 16, 22, 18) }; Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(Label("사용자 지정 색상", 21, "Text"));
        header.Children.Add(Label("색상을 골라 미리 본 뒤 적용하세요. 취소하면 기존 설정을 유지합니다.", 11, "Secondary"));
        basis = Label("", 11, "Muted"); basis.Margin = new Thickness(0, 6, 0, 0); header.Children.Add(basis);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(Button("취소", () => DialogResult = false));
        var apply = Button("적용", () => { draft.Normalize(); DialogResult = true; }); apply.IsDefault = true; footer.Children.Add(apply);
        var layout = new Grid(); root.Children.Add(layout);
        var body = new StackPanel();
        var colorsScroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var previewScroll = new ScrollViewer { Content = preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        layout.Children.Add(colorsScroll); layout.Children.Add(previewScroll);
        preview.Children.Add(Label("작업 표시줄 미리보기 · 예시 수치", 11, "Secondary"));
        var strip = new WidgetStrip { Width = 546, Height = 42, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
        var now = DateTimeOffset.UtcNow;
        strip.Render(new(new UsageWindow(36, now.AddDays(2)), null, now, "미리보기"), new(new UsageWindow(22, null), new UsageWindow(48, null), now, "미리보기"), new(WorkState.Working), new(WorkState.Waiting), null);
        preview.Children.Add(new Viewbox { Child = strip, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 16) });
        preview.Children.Add(Label("상세 화면 미리보기 · 예시 수치", 11, "Secondary"));
        // Reuse the real detail content, with a separate settings copy and no live actions.
        previewSettings = new Settings { ThemeId = CustomTheme.Id, CustomTheme = draft.Clone(), PositionPercent = settings.PositionPercent, AboveTaskbar = settings.AboveTaskbar };
        detailPreview = new DetailView(previewSettings, () => { }, () => { }, () => { }, _ => { }, () => { }, () => new ContextMenu())
        { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 12), IsHitTestVisible = false, Focusable = false };
        System.Windows.Input.KeyboardNavigation.SetTabNavigation(detailPreview, System.Windows.Input.KeyboardNavigationMode.None);
        detailPreview.Update(new(new UsageWindow(36, now.AddDays(2)), null, now, "Codex 계정에서 갱신"),
            new(new UsageWindow(22, now.AddDays(4)), new UsageWindow(48, now.AddHours(3)), now, "Claude Desktop에서 갱신"),
            new(WorkState.Working), new(WorkState.Waiting), new(24, 58, 18.6, 32));
        preview.Children.Add(detailPreview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) }; preview.Children.Add(buttons);
        foreach (var (role, title) in new[] { ("Control", "버튼"), ("Hover", "마우스 오버"), ("Pressed", "누름") })
        {
            var chip = new Border { Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(5), Child = Label(title, 10, "Text") };
            ThemeManager.Bind(chip, Border.BackgroundProperty, role); buttons.Children.Add(chip);
        }
        var copies = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; body.Children.Add(copies);
        copies.Children.Add(Button("현재 테마 색상 복사", () => { draft = new CustomTheme { BaseThemeId = current.Id == CustomTheme.Id ? settings.CustomTheme.BaseThemeId : current.Id, Colors = CustomTheme.Palette(current) }; Refresh(); }));
        copies.Children.Add(Button("기준 테마의 기본색", () => { draft.Colors.Clear(); Refresh(); }));
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 1 }; body.Children.Add(grid);
        foreach (var (key, title) in CustomTheme.Roles)
        {
            var choose = Button("", () => ChooseColor(key)); choose.Margin = new Thickness(0, 0, 9, 7);
            System.Windows.Automation.AutomationProperties.SetName(choose, title + " 색상 선택");
            var row = new DockPanel { Width = 229 };
            var swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(swatch, Dock.Left); row.Children.Add(swatch);
            var hex = Label("", 10, "Secondary"); hex.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(hex, Dock.Right); row.Children.Add(hex);
            var label = Label(title, 11, "Text"); label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label);
            choose.Content = row; fields[key] = (swatch, hex); grid.Children.Add(choose);
        }
        bool? wasWide = null;
        void ArrangePanels(double width)
        {
            bool wide = width >= 900; if (wasWide == wide) return; wasWide = wide;
            layout.ColumnDefinitions.Clear(); layout.RowDefinitions.Clear();
            if (wide)
            {
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
                layout.ColumnDefinitions.Add(new ColumnDefinition());
                Grid.SetRow(colorsScroll, 0); Grid.SetRow(previewScroll, 0); Grid.SetColumn(colorsScroll, 0); Grid.SetColumn(previewScroll, 1);
                colorsScroll.Margin = new Thickness(0, 0, 18, 0); previewScroll.Margin = new Thickness(0); grid.Columns = 1;
            }
            else
            {
                layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition());
                Grid.SetColumn(colorsScroll, 0); Grid.SetColumn(previewScroll, 0); Grid.SetRow(previewScroll, 0); Grid.SetRow(colorsScroll, 1);
                colorsScroll.Margin = new Thickness(0, 14, 0, 0); previewScroll.Margin = new Thickness(0); grid.Columns = 2;
            }
        }
        layout.SizeChanged += (_, e) => ArrangePanels(e.NewSize.Width + 44); ArrangePanels(Width);
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { DialogResult = false; e.Handled = true; } };
        Refresh();
    }
    private void ChooseColor(string role)
    {
        using var picker = new Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.ColorTranslator.FromHtml(CustomTheme.Palette(draft.Resolve())[role]) };
        if (picker.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
        draft.Colors[role] = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"; Refresh();
    }
    private void Refresh()
    {
        var theme = draft.Resolve(); var colors = CustomTheme.Palette(theme);
        ThemeManager.ApplyTo(preview.Resources, theme);
        previewSettings.CustomTheme = draft.Clone(); detailPreview.SelectTheme(CustomTheme.Id);
        basis.Text = "기준 모양: " + ThemeCatalog.Find(draft.BaseThemeId).Name;
        foreach (var (key, field) in fields) { field.Swatch.Background = ThemeManager.Brush(colors[key]); field.Hex.Text = colors[key]; }
    }
    private static TextBlock Label(string text, double size, string role)
    { var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap }; ThemeManager.Bind(label, TextBlock.ForegroundProperty, role); return label; }
    private Button Button(string text, Action click)
    { var button = new Button { Content = text, Style = (Style)FindResource("Pulse.Button"), Margin = new Thickness(0, 0, 8, 0) }; button.Click += (_, _) => click(); return button; }
    private sealed record DialogOwner(IntPtr Handle) : Forms.IWin32Window;
}
