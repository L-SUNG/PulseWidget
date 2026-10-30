using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PulseWidget.Core;

namespace PulseWidget.Theming;

public sealed class ThemePicker : StackPanel
{
    private readonly Button toggle;
    private readonly Popup popup;
    private readonly Dictionary<string, RadioButton> options = new();
    private readonly TextBlock selectedName;
    private readonly Border selectedSwatch;
    private string selectedId = ThemeCatalog.DefaultId;
    private bool selecting;
    private readonly Func<CustomTheme> custom;
    private Border? customSwatch;

    public ThemePicker(string id, Action<string> change, Func<CustomTheme>? custom = null)
    {
        this.custom = custom ?? (() => new CustomTheme());
        Margin = new Thickness(0, 0, 0, 12);
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var hint = Label("선택 즉시 적용", 10, "Muted"); DockPanel.SetDock(hint, Dock.Right); heading.Children.Add(hint);
        heading.Children.Add(Label("테마", 11, "Secondary")); Children.Add(heading);
        toggle = new Button { Style = (Style)FindResource("Pulse.Button"), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        System.Windows.Automation.AutomationProperties.SetName(toggle, "테마 선택");
        var row = new DockPanel { Width = 320 };
        var arrow = Label("⌄", 13, "Secondary"); DockPanel.SetDock(arrow, Dock.Right); row.Children.Add(arrow);
        selectedSwatch = new Border { Width = 32, Height = 18, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(selectedSwatch, Dock.Left); row.Children.Add(selectedSwatch);
        selectedName = Label("", 11, "Text"); selectedName.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(selectedName); toggle.Content = row; Children.Add(toggle);

        var surface = new Border { Width = 356, BorderThickness = new Thickness(1), Padding = new Thickness(10) };
        ThemeManager.Bind(surface, Border.BackgroundProperty, "SurfaceBackground");
        ThemeManager.Bind(surface, Border.BorderBrushProperty, "Border");
        ThemeManager.Bind(surface, Border.CornerRadiusProperty, "DialogRadius");
        var scroll = new ScrollViewer { MaxHeight = Math.Clamp(SystemParameters.WorkArea.Height - 80, 180, 400), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        surface.Child = scroll;
        var groups = new StackPanel { Margin = new Thickness(2, 0, 8, 0) }; scroll.Content = groups;
        popup = new Popup { PlacementTarget = toggle, Placement = PlacementMode.Top, VerticalOffset = -5, AllowsTransparency = true, StaysOpen = false, Child = surface };
        foreach (var family in ThemeCatalog.All.Append(this.custom().Resolve()).GroupBy(t => t.Family))
        {
            var familyTitle = Label(family.Key, 11, "Secondary"); familyTitle.Margin = new Thickness(2, 7, 0, 7); groups.Children.Add(familyTitle);
            var grid = new UniformGrid { Columns = 2 }; groups.Children.Add(grid);
            foreach (var theme in family)
            {
                var option = new RadioButton { GroupName = "PulseThemes", Style = (Style)FindResource("Pulse.ThemeOption"), ToolTip = theme.Description, Margin = new Thickness(0, 0, 5, 5) };
                System.Windows.Automation.AutomationProperties.SetName(option, theme.Name);
                var content = new StackPanel();
                var swatch = new Border { Height = 25, CornerRadius = new CornerRadius(Math.Min(theme.WidgetRadius, 9)), Background = ThemeManager.Finish(theme.Surface, theme.SurfaceHighlight), BorderBrush = ThemeManager.Brush(theme.Border), BorderThickness = new Thickness(1), Margin = new Thickness(2, 0, 2, 4) };
                var marks = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (var color in new[] { theme.OpenAI, theme.Claude, theme.Accent })
                    marks.Children.Add(new Border { Width = 24, Height = 4, CornerRadius = new CornerRadius(2), Background = ThemeManager.Brush(color), Margin = new Thickness(3, 0, 3, 0) });
                swatch.Child = marks; content.Children.Add(swatch);
                if (theme.Id == CustomTheme.Id) customSwatch = swatch;
                content.Children.Add(new TextBlock { Text = theme.ChoiceName, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
                option.Content = content; options[theme.Id] = option; grid.Children.Add(option);
                option.Checked += (_, _) =>
                {
                    if (selecting) return;
                    change(theme.Id); Select(theme.Id); popup.IsOpen = false; toggle.Focus();
                };
            }
        }
        toggle.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        popup.Opened += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (options.TryGetValue(selectedId, out var option)) { option.BringIntoView(); option.Focus(); }
        }));
        surface.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            popup.IsOpen = false; toggle.Focus(); e.Handled = true;
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) popup.IsOpen = false; };
        Unloaded += (_, _) => popup.IsOpen = false;
        Select(id);
    }

    public void Select(string id)
    {
        var own = custom().Resolve();
        if (customSwatch is not null)
        {
            customSwatch.Background = ThemeManager.Brush(own.Surface); customSwatch.BorderBrush = ThemeManager.Brush(own.Border);
            if (customSwatch.Child is StackPanel marks)
                foreach (var (mark, color) in marks.Children.Cast<Border>().Zip(new[] { own.OpenAI, own.Claude, own.Accent })) mark.Background = ThemeManager.Brush(color);
        }
        var theme = id == CustomTheme.Id ? custom().Resolve() : ThemeCatalog.Find(id); selectedId = theme.Id;
        selectedName.Text = theme.Name; toggle.ToolTip = theme.Description;
        selectedSwatch.Background = ThemeManager.Finish(theme.Surface, theme.SurfaceHighlight);
        selectedSwatch.BorderBrush = ThemeManager.Brush(theme.Accent);
        selecting = true;
        try { options[theme.Id].IsChecked = true; }
        finally { selecting = false; }
    }

    private static TextBlock Label(string text, double size, string role)
    {
        var label = new TextBlock { Text = text, FontSize = size };
        ThemeManager.Bind(label, TextBlock.ForegroundProperty, role); return label;
    }
}
