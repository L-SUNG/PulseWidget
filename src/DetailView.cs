using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using PulseWidget.Core;
using PulseWidget.Services;
using PulseWidget.Theming;

namespace PulseWidget;

public sealed class DetailView : UserControl
{
    private readonly TextBlock openStatus, claudeStatus, openInfo, claudeInfo, resourceInfo;
    private readonly Meter openWeek, claudeWeek, claudeFive;
    private readonly ThemePicker themePicker;
    private readonly Slider position;
    private readonly TextBlock positionLabel;
    private readonly TextBox positionInput;
    private readonly TextBlock positionError;
    private readonly Settings settings;
    private readonly Action save;
    private bool inputDirty;
    private bool syncingPosition;

    public DetailView(Settings settings, Action save, Action connect, Action refresh, Action<string> changeTheme,
        Action? editColors = null, Func<ContextMenu>? displayMenu = null, Action? close = null)
    {
        this.settings = settings; this.save = save;
        Width = 402;
        Background = Brushes.Transparent; FontFamily = new FontFamily("Segoe UI, Malgun Gothic");
        var surface = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(21, 17, 21, 15) };
        ThemeManager.Bind(surface, Border.BackgroundProperty, "SurfaceBackground");
        ThemeManager.Bind(surface, Border.BorderBrushProperty, "Border");
        ThemeManager.Bind(surface, Border.CornerRadiusProperty, "DialogRadius");
        Content = surface;
        var root = new StackPanel(); surface.Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var header = new DockPanel(); root.Children.Add(header);
        var closeButton = Button("✕", () => close?.Invoke()); closeButton.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(closeButton, Dock.Right); header.Children.Add(closeButton);
        var title = new StackPanel(); header.Children.Add(title);
        title.Children.Add(Text("PULSE", 12, "Accent", FontWeights.SemiBold));
        title.Children.Add(Text("내 작업의 현재 상태", 21, "Text", FontWeights.SemiBold));
        root.Children.Add(Text("구독 한도와 PC 자원을 한눈에", 11, "Muted", margin: new Thickness(0, 5, 0, 13)));
        var open = Card(root, "OpenAI", "Codex", "OpenAI", out openStatus);
        openWeek = new Meter("주간", "OpenAI"); open.Children.Add(openWeek.Root);
        openInfo = Text("연결 중…", 10, "Muted"); open.Children.Add(openInfo);
        var claude = Card(root, "Claude", "Desktop / Code", "Claude", out claudeStatus);
        claudeWeek = new Meter("주간", "Claude"); claude.Children.Add(claudeWeek.Root);
        claudeFive = new Meter("5시간", "Claude"); claude.Children.Add(claudeFive.Root);
        claudeInfo = Text("사용량 수신 확인 중", 10, "Muted"); claude.Children.Add(claudeInfo);
        resourceInfo = Text("CPU —     MEM —", 12, "Secondary", margin: new Thickness(0, 2, 0, 14)); root.Children.Add(resourceInfo);

        themePicker = new ThemePicker(settings.ThemeId, changeTheme, () => settings.CustomTheme); root.Children.Add(themePicker);
        var settingsActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        if (editColors is not null) settingsActions.Children.Add(Button("색상 편집…", editColors));
        if (displayMenu is not null)
        {
            var displays = Button("디스플레이 설정", () => { });
            displays.Click += (_, _) => { var menu = displayMenu(); menu.PlacementTarget = displays; menu.IsOpen = true; };
            settingsActions.Children.Add(displays);
        }
        root.Children.Add(settingsActions);

        var positionRow = new DockPanel { Margin = new Thickness(0, 0, 0, 2) }; root.Children.Add(positionRow);
        var inputRow = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(inputRow, Dock.Right); positionRow.Children.Add(inputRow);
        positionInput = new TextBox { Width = 67, MaxLength = 20, VerticalContentAlignment = VerticalAlignment.Center, Style = (Style)FindResource("Pulse.TextBox"), ToolTip = "0~100 사이의 숫자 · 소수 입력 가능 · Enter 또는 적용" };
        System.Windows.Automation.AutomationProperties.SetName(positionInput, "가로 위치 직접 입력 (%)");
        inputRow.Children.Add(positionInput);
        var percentLabel = Text("%", 11, "Secondary", margin: new Thickness(4, 0, 7, 0)); percentLabel.VerticalAlignment = VerticalAlignment.Center; inputRow.Children.Add(percentLabel);
        inputRow.Children.Add(Button("적용", () => CommitPosition()));
        positionLabel = Text("", 10, "Muted"); positionLabel.VerticalAlignment = VerticalAlignment.Center; positionRow.Children.Add(positionLabel);
        positionError = Text("0~100 사이의 숫자를 입력하세요.", 10, "Claude", margin: new Thickness(0, 3, 0, 2)); positionError.Visibility = Visibility.Collapsed;
        root.Children.Add(positionError);
        position = new Slider { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 5, Margin = new Thickness(0, 2, 0, 2), ToolTip = "디스플레이별 이동 가능 범위 기준 · 0% 왼쪽 / 50% 가운데 / 100% 오른쪽", Style = (Style)FindResource("Pulse.Slider") };
        System.Windows.Automation.AutomationProperties.SetName(position, "가로 위치 (%)");
        SetPosition(settings.PositionPercent ?? HorizontalPosition.DefaultPercent);
        position.ValueChanged += (_, _) =>
        {
            if (syncingPosition) return;
            settings.PositionPercent = HorizontalPosition.Clamp(position.Value);
            SetPosition(settings.PositionPercent.Value); save();
        };
        positionInput.TextChanged += (_, _) => { if (!syncingPosition) { inputDirty = true; positionError.Visibility = Visibility.Collapsed; } };
        positionInput.LostKeyboardFocus += (_, _) => { if (inputDirty) CommitPosition(); };
        positionInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitPosition(); e.Handled = true; }
            if (e.Key == Key.Escape) { SetPosition(settings.PositionPercent ?? HorizontalPosition.DefaultPercent); e.Handled = true; }
        };
        root.Children.Add(position);
        root.Children.Add(Text("0% 왼쪽  ·  50% 가운데  ·  100% 오른쪽", 10, "Muted", margin: new Thickness(0, 0, 0, 8)));
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Button("새로고침", refresh)); actions.Children.Add(Button("Claude 연결", connect));
        var above = new CheckBox { Content = "위에 표시", FontSize = 11, Margin = new Thickness(10, 8, 0, 0), IsChecked = settings.AboveTaskbar, Style = (Style)FindResource("Pulse.CheckBox") };
        above.Click += (_, _) => { settings.AboveTaskbar = above.IsChecked == true; save(); }; actions.Children.Add(above); root.Children.Add(actions);
        root.Children.Add(Text("* 10분 이상 지난 수신값 · 상태는 이 PC 기준", 10, "Muted", margin: new Thickness(0, 10, 0, 0)));
    }

    public void SelectTheme(string id) => themePicker.Select(id);
    public void SetPosition(double percent)
    {
        syncingPosition = true;
        try
        {
            position.Value = HorizontalPosition.Clamp(percent); positionLabel.Text = $"가로 위치 · {position.Value:0.##}%";
            positionInput.Text = position.Value.ToString("0.##", CultureInfo.CurrentCulture);
            inputDirty = false; positionError.Visibility = Visibility.Collapsed;
        }
        finally { syncingPosition = false; }
    }
    private bool CommitPosition()
    {
        if (!inputDirty) return true;
        var input = positionInput.Text.Trim().TrimEnd('%').Trim();
        const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!(double.TryParse(input, style, CultureInfo.CurrentCulture, out var value) || double.TryParse(input, style, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value) || value < 0 || value > 100)
        { positionError.Visibility = Visibility.Visible; return false; }
        bool changed = settings.PositionPercent != value;
        settings.PositionPercent = value; SetPosition(value);
        if (changed) save();
        return true;
    }
    public void Update(ProviderUsage open, ProviderUsage claude, Activity oa, Activity ca, SystemMonitor.Snapshot? pc)
    {
        var now = DateTimeOffset.UtcNow;
        openStatus.Text = "● " + oa.Label; claudeStatus.Text = "● " + (ca.State == WorkState.Unknown && claude.UpdatedAt is null ? "연결 대기" : ca.Label);
        ThemeManager.Bind(openStatus, TextBlock.ForegroundProperty, ThemeManager.StateRole(oa));
        ThemeManager.Bind(claudeStatus, TextBlock.ForegroundProperty, ThemeManager.StateRole(ca));
        openWeek.Update(open.Weekly, open.UpdatedAt, now); claudeWeek.Update(claude.Weekly, claude.UpdatedAt, now); claudeFive.Update(claude.FiveHour, claude.UpdatedAt, now);
        string Info(ProviderUsage p) => p.UpdatedAt is { } date ? $"{date.ToLocalTime():MM/dd HH:mm} 수신{(p.IsStale(now) ? " · 오래된 수신값 *" : "")}\n{p.Message}" : p.Message;
        openInfo.Text = Info(open); claudeInfo.Text = Info(claude);
        resourceInfo.Text = pc is null ? "CPU —     MEM —" : $"CPU {pc.Cpu?.ToString("0") ?? "—"}%     MEM {pc.Memory:0}%   ·   {pc.UsedGb:0.0} / {pc.TotalGb:0.0} GB";
    }
    private static StackPanel Card(Panel parent, string name, string subtitle, string role, out TextBlock status)
    {
        var layer = new Grid { Margin = new Thickness(0, 0, 0, 10) }; parent.Children.Add(layer);
        // Keep the shadow on the background so text stays sharp when themes change.
        var card = new Border(); layer.Children.Add(card);
        ThemeManager.Bind(card, Border.BackgroundProperty, "PanelBackground");
        ThemeManager.Bind(card, Border.CornerRadiusProperty, "CardRadius");
        ThemeManager.Bind(card, Border.EffectProperty, "CardShadow");
        var content = new StackPanel { Margin = new Thickness(13, 11, 13, 10) }; layer.Children.Add(content);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; content.Children.Add(header);
        status = Text("● 확인 필요", 10, "Muted"); DockPanel.SetDock(status, Dock.Right); header.Children.Add(status);
        header.Children.Add(Text(name + "  ·  " + subtitle, 12, role, FontWeights.SemiBold)); return content;
    }
    private static TextBlock Text(string text, double size, string role, FontWeight? weight = null, Thickness? margin = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, Margin = margin ?? new Thickness(0), TextWrapping = TextWrapping.Wrap };
        ThemeManager.Bind(block, TextBlock.ForegroundProperty, role); return block;
    }
    private static Button Button(string text, Action click)
    {
        var button = new Button { Content = text, Style = (Style)Application.Current.FindResource("Pulse.Button"), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) => click(); return button;
    }
    private sealed class Meter
    {
        public StackPanel Root { get; } = new() { Margin = new Thickness(0, 0, 0, 9) };
        private readonly TextBlock values, reset;
        private readonly ProgressBar bar;
        private readonly string title;
        public Meter(string title, string role)
        {
            this.title = (role == "OpenAI" ? "Codex" : "Claude") + " " + title;
            var row = new DockPanel(); Root.Children.Add(row);
            values = Text("—", 12, "Text", FontWeights.SemiBold); DockPanel.SetDock(values, Dock.Right); row.Children.Add(values);
            row.Children.Add(Text(title, 11, "Secondary"));
            bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4, BorderThickness = new Thickness(0), Margin = new Thickness(0, 7, 0, 5), Style = (Style)Application.Current.FindResource("Pulse.Meter") };
            ThemeManager.Bind(bar, ProgressBar.ForegroundProperty, role); Root.Children.Add(bar);
            reset = Text("한도 정보 대기", 10, "Muted"); Root.Children.Add(reset);
        }
        public void Update(UsageWindow? window, DateTimeOffset? received, DateTimeOffset now)
        {
            bool valid = UsageBar.Update(bar, window, received, now, title);
            values.Text = valid ? $"사용 {window!.Used:0.#}%   남음 {window.Remaining:0.#}%" : "사용 —   남음 —";
            if (valid && window!.IsStale(now, received)) values.Text += " *";
            reset.Text = window?.ResetsAt is { } at ? at <= now ? "초기화 시각 경과 · 새 데이터 대기" : $"{at.ToLocalTime():MM/dd HH:mm} 초기화" : "초기화 시각 정보 없음";
            if (valid && window!.ObservedAt is { } observed) reset.Text += $" · {observed.ToLocalTime():MM/dd HH:mm} 수신";
        }
    }
}
