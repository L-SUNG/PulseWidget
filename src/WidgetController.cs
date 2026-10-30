using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using PulseWidget.Core;
using PulseWidget.Native;
using PulseWidget.Services;
using PulseWidget.Theming;
using Forms = System.Windows.Forms;
using Activity = PulseWidget.Core.Activity;

namespace PulseWidget;

public sealed class WidgetController : IDisposable
{
    public Settings Settings { get; } = Settings.Load();
    private readonly CodexClient codex = new();
    private readonly ActivityMonitor activity = new();
    private readonly SystemMonitor resources = new();
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<string, WidgetWindow> windows = new(StringComparer.OrdinalIgnoreCase);
    private Forms.NotifyIcon? tray;
    private SessionRecovery? recovery;
    private bool quitting, disposed, readingActivity, readingUsage;
    private DateTimeOffset nextUsage, nextActivity;
    private ProviderUsage openAi = ProviderUsage.Empty("Codex 연결 중…"), claude = ProviderUsage.Empty("Claude 사용량 확인 중…");
    private Activity openActivity = new(WorkState.Unknown), claudeActivity = new(WorkState.Unknown);
    private SystemMonitor.Snapshot? system;
    private Dispatcher Dispatcher => Application.Current.Dispatcher;

    public void Start()
    {
        // In all-display mode the primary monitor is the one migration reference;
        // never derive a different percentage independently for each widget.
        string? reference = Settings.ShowOnAllDisplays ? null : Settings.DisplayDeviceName;
        if (Settings.PositionPercent is null && Settings.MigratePosition(Displays.Resolve(reference).Bounds.Width, Taskbar.Bounds(reference).Scale)) SaveSettings();
        ThemeManager.Apply(Settings.ResolveTheme());
        tray = new Forms.NotifyIcon { Icon = TrayIcon.Create(ThemeManager.Current.Surface, ThemeManager.Current.Accent), Text = "Pulse · AI 사용량 & PC", Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(() => PreferredWindow()?.ToggleDetail()); };
        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, e) => { BuildTrayMenu(menu); e.Cancel = false; }; tray.ContextMenuStrip = menu;
        recovery = new SessionRecovery(RestartForRecovery);
        ReconcileWindows();
        clock.Tick += (_, _) => Tick(); clock.Start(); Tick();
        if (Environment.GetCommandLineArgs().Contains("--show-details")) PreferredWindow()?.ToggleDetail();
    }

    private WidgetWindow? PreferredWindow()
    {
        string device = Forms.Screen.FromPoint(Forms.Cursor.Position).DeviceName;
        return windows.GetValueOrDefault(device) ?? windows.Values.FirstOrDefault();
    }
    private void ReconcileWindows()
    {
        if (quitting) return;
        var displays = Displays.All;
        var selected = DisplaySelection.Select(displays.Select(s => s.DeviceName), displays.FirstOrDefault(s => s.Primary)?.DeviceName, Settings.DisplayDeviceName, Settings.ShowOnAllDisplays);
        foreach (string device in windows.Keys.Except(selected, StringComparer.OrdinalIgnoreCase).ToArray())
        { var window = windows[device]; windows.Remove(device); window.CloseForUpdate(); }
        foreach (string device in selected)
        {
            if (windows.ContainsKey(device)) continue;
            var window = new WidgetWindow(this, device); windows.Add(device, window); window.Show();
        }
        foreach (var window in windows.Values) window.ApplyPosition();
        Render();
    }
    public void SaveSettings()
    {
        try { Settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show("설정을 저장할 수 없습니다.", "Pulse"); }
        foreach (var window in windows.Values) { window.ApplyPosition(); window.PositionChanged(); }
    }
    private void ChooseDisplay(bool all, string? device)
    {
        Settings.ShowOnAllDisplays = all;
        if (!all) Settings.DisplayDeviceName = device;
        SaveSettings();
        Dispatcher.BeginInvoke(new Action(ReconcileWindows));
    }
    public void SetTheme(string id)
    {
        Settings.ThemeId = id == CustomTheme.Id ? id : ThemeCatalog.Find(id).Id;
        ThemeManager.Apply(Settings.ResolveTheme());
        foreach (var window in windows.Values) window.ThemeChanged();
        if (tray is not null)
        { var previous = tray.Icon; tray.Icon = TrayIcon.Create(ThemeManager.Current.Surface, ThemeManager.Current.Accent); previous?.Dispose(); }
        SaveSettings();
    }
    public void EditColors(WidgetWindow owner)
    {
        var editor = new ThemeEditorWindow(Settings) { Owner = owner };
        editor.Loaded += (_, _) => Taskbar.PositionPopup(editor, owner, owner.DeviceName);
        if (editor.ShowDialog() != true) return;
        Settings.CustomTheme = editor.Result;
        SetTheme(CustomTheme.Id);
    }
    public void Refresh() { nextUsage = DateTimeOffset.MinValue; nextActivity = DateTimeOffset.MinValue; Tick(); }
    private async void Tick()
    {
        if (quitting) return;
        if (windows.Values.Any(w => !w.NativeAlive)) { RestartForRecovery("taskbar-exit"); return; }
        ReconcileWindows();
        var now = DateTimeOffset.UtcNow;
        try { system = resources.Read(); } catch { system = null; }
        if (!readingActivity && now >= nextActivity)
        {
            readingActivity = true; nextActivity = now.AddSeconds(6);
            try { var values = await Task.Run(() => (activity.ReadCodex(now), activity.ReadClaude(now), activity.ReadClaudeUsage())); openActivity = values.Item1; claudeActivity = values.Item2; claude = values.Item3; }
            catch { openActivity = new(WorkState.Unknown); claudeActivity = new(WorkState.Unknown); }
            finally { readingActivity = false; }
        }
        if (quitting) return;
        Render();
        if (!readingUsage && now >= nextUsage)
        {
            readingUsage = true; nextUsage = now.AddMinutes(1);
            try { openAi = await codex.ReadAsync(Settings.CodexPath); }
            catch (Exception ex) { openAi = openAi with { Message = ex is TimeoutException ? "Codex 조회 시간 초과 · 1분 후 재시도" : ex is InvalidOperationException ? ex.Message : "Codex 연결 실패 · 로그인 상태를 확인하세요." }; }
            finally { readingUsage = false; }
            if (!quitting) Render();
        }
    }
    private void Render()
    { foreach (var window in windows.Values) window.Render(openAi, claude, openActivity, claudeActivity, system); }

    public ContextMenu CreateMenu(WidgetWindow owner)
    {
        var menu = new ContextMenu();
        void Fill()
        {
            menu.Items.Clear();
            void Add(string label, Action action) => menu.Items.Add(Item(label, action));
            Add("상세 보기", owner.ToggleDetail); Add("지금 새로고침", Refresh); Add("화면 다시 표시", () => RestartForRecovery("manual"));
            var themes = new MenuItem { Header = "테마" };
            foreach (var family in ThemeCatalog.All.GroupBy(t => t.Family))
            {
                var group = new MenuItem { Header = family.Key }; themes.Items.Add(group);
                foreach (var theme in family) group.Items.Add(Item(theme.ChoiceName, () => SetTheme(theme.Id), Settings.ThemeId == theme.Id));
            }
            themes.Items.Add(new Separator()); themes.Items.Add(Item("사용자 지정", () => SetTheme(CustomTheme.Id), Settings.ThemeId == CustomTheme.Id));
            themes.Items.Add(Item("색상 편집…", () => EditColors(owner))); menu.Items.Add(themes);
            var displays = new MenuItem { Header = "디스플레이" }; FillDisplayItems(displays.Items); menu.Items.Add(displays);
            menu.Items.Add(new Separator());
            var position = new MenuItem { Header = $"가로 위치 · {Settings.PositionPercent:0.#}%" };
            void Move(double percent) { Settings.PositionPercent = HorizontalPosition.Clamp(percent); SaveSettings(); }
            position.Items.Add(Item("← 왼쪽으로 1% 이동", () => Move((Settings.PositionPercent ?? 0) - 1)));
            position.Items.Add(Item("오른쪽으로 1% 이동 →", () => Move((Settings.PositionPercent ?? 0) + 1)));
            position.Items.Add(new Separator());
            position.Items.Add(Item("왼쪽 · 0%", () => Move(0)));
            position.Items.Add(Item("가운데 · 50%", () => Move(50)));
            position.Items.Add(Item("오른쪽 · 100%", () => Move(100)));
            menu.Items.Add(position);
            Add("기본 위치 · 10%", () => { Settings.PositionPercent = HorizontalPosition.DefaultPercent; Settings.Scale = 1; Settings.AboveTaskbar = false; SaveSettings(); });
            menu.Items.Add(Item("작업 표시줄 위에 표시", () => { Settings.AboveTaskbar = !Settings.AboveTaskbar; SaveSettings(); }, Settings.AboveTaskbar));
            menu.Items.Add(Item("항상 표시", () => { Settings.AlwaysVisible = !Settings.AlwaysVisible; SaveSettings(); }, Settings.AlwaysVisible));
            menu.Items.Add(Item("Windows 시작 시 실행", () => SetStartup(!StartupEnabled()), StartupEnabled()));
            menu.Items.Add(new Separator()); Add("Claude 작업 상태 연결", InstallClaude); Add("Codex 실행 파일 선택…", SelectCodex); Add("종료", Exit);
        }
        Fill(); menu.Opened += (_, _) => Fill(); return menu;
    }
    private static MenuItem Item(string label, Action action, bool? check = null)
    {
        var item = new MenuItem { Header = label, IsCheckable = check.HasValue, IsChecked = check == true };
        item.Click += (_, _) => action(); return item;
    }
    private void FillDisplayItems(ItemCollection items)
    {
        items.Add(Item("모든 디스플레이에 표시", () => ChooseDisplay(true, null), Settings.ShowOnAllDisplays));
        var one = new MenuItem { Header = "한 디스플레이에만 표시", IsChecked = !Settings.ShowOnAllDisplays };
        one.Items.Add(Item("주 디스플레이 (자동)", () => ChooseDisplay(false, null), !Settings.ShowOnAllDisplays && Settings.DisplayDeviceName is null));
        foreach (var screen in Displays.All) one.Items.Add(Item(Displays.Label(screen), () => ChooseDisplay(false, screen.DeviceName), !Settings.ShowOnAllDisplays && Settings.DisplayDeviceName == screen.DeviceName));
        if (Settings.DisplayDeviceName is { } preferred && !Displays.All.Any(s => s.DeviceName == preferred))
            one.Items.Add(new MenuItem { Header = $"{preferred} 연결 해제 · 주 화면에 임시 표시", IsEnabled = false });
        items.Add(one);
    }
    public ContextMenu CreateDisplayMenu()
    { var menu = new ContextMenu(); FillDisplayItems(menu.Items); return menu; }
    private void BuildTrayMenu(Forms.ContextMenuStrip menu)
    {
        foreach (var item in menu.Items.Cast<Forms.ToolStripItem>().ToArray()) item.Dispose(); menu.Items.Clear();
        void Add(string label, Action action) => menu.Items.Add(label, null, (_, _) => Dispatcher.Invoke(action));
        Add("상세 보기", () => PreferredWindow()?.ToggleDetail()); Add("새로고침", Refresh);
        Add("색상 편집…", () => { if (PreferredWindow() is { } owner) EditColors(owner); });
        var themes = new Forms.ToolStripMenuItem("테마"); menu.Items.Add(themes);
        foreach (var family in ThemeCatalog.All.Append(Settings.CustomTheme.Resolve()).GroupBy(t => t.Family))
        {
            var group = new Forms.ToolStripMenuItem(family.Key); themes.DropDownItems.Add(group);
            foreach (var theme in family) { var option = new Forms.ToolStripMenuItem(theme.ChoiceName) { Checked = Settings.ThemeId == theme.Id }; option.Click += (_, _) => Dispatcher.Invoke(() => SetTheme(theme.Id)); group.DropDownItems.Add(option); }
        }
        var displays = new Forms.ToolStripMenuItem("디스플레이"); menu.Items.Add(displays);
        void DisplayItem(string label, bool check, Action action) { var item = new Forms.ToolStripMenuItem(label) { Checked = check }; item.Click += (_, _) => Dispatcher.Invoke(action); displays.DropDownItems.Add(item); }
        DisplayItem("모든 디스플레이에 표시", Settings.ShowOnAllDisplays, () => ChooseDisplay(true, null));
        DisplayItem("주 디스플레이만 (자동)", !Settings.ShowOnAllDisplays && Settings.DisplayDeviceName is null, () => ChooseDisplay(false, null));
        foreach (var screen in Displays.All) DisplayItem(Displays.Label(screen) + "에만 표시", !Settings.ShowOnAllDisplays && Settings.DisplayDeviceName == screen.DeviceName, () => ChooseDisplay(false, screen.DeviceName));
        Add("화면 다시 표시", () => RestartForRecovery("manual")); Add("종료", Exit); TrayThemeRenderer.Apply(menu, ThemeManager.Current);
    }
    public bool RestartForRecovery(string reason)
    {
        if (quitting) return true;
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--recover-after"); start.ArgumentList.Add(Environment.ProcessId.ToString());
        try
        {
            using var next = Process.Start(start); if (next is null) return false;
            try { Directory.CreateDirectory(Settings.DataDirectory); string path = Path.Combine(Settings.DataDirectory, "recovery.log"); if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Move(path, path + ".previous", true); File.AppendAllText(path, $"{DateTimeOffset.Now:O} {reason} PID={Environment.ProcessId} -> {next.Id}\n"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch { return false; }
        Exit(); return true;
    }
    public void Exit() { quitting = true; Dispose(); Application.Current.Shutdown(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; quitting = true; recovery?.Dispose(); clock.Stop();
        foreach (var window in windows.Values.ToArray()) window.CloseForUpdate(); windows.Clear();
        if (tray is not null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        codex.Dispose();
    }

    private void SelectCodex()
    {
        var dialog = new OpenFileDialog { Filter = "Codex|codex.exe", Title = "Codex 실행 파일" };
        if (dialog.ShowDialog() == true) { Settings.CodexPath = dialog.FileName; SaveSettings(); nextUsage = DateTimeOffset.MinValue; Tick(); }
    }
    public async void InstallClaude()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "scripts", "Install-Claude.ps1");
        if (!File.Exists(script)) { MessageBox.Show("배포 폴더에서 실행해 주세요. scripts/Install-Claude.ps1이 필요합니다.", "Pulse"); return; }
        try
        {
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask; var error = await errorTask;
            MessageBox.Show(process.ExitCode == 0 ? "작업 상태 연동을 설치했습니다. 기존 설정은 백업되었습니다.\n\nClaude Desktop 사용량은 앱의 로컬 기록에서 자동으로 읽습니다.\nDesktop에 로그인하고 사용량 메뉴를 열어 갱신할 수 있습니다.\n터미널 Claude Code의 상태줄 사용량도 지원합니다.\n저장된 사용량은 위젯을 재실행해도 유지됩니다." : "Claude 연결을 완료하지 못했습니다.\n" + error, "Pulse");
        }
        catch { MessageBox.Show("Claude 연결 스크립트를 실행할 수 없습니다.", "Pulse"); }
    }
    private static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("PulseWidget") is string;
    }
    private static void SetStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled) key.SetValue("PulseWidget", "\"" + Environment.ProcessPath + "\"");
            else key.DeleteValue("PulseWidget", false);
        }
        catch { MessageBox.Show("자동 시작 설정을 변경하지 못했습니다.", "Pulse"); }
    }
}
