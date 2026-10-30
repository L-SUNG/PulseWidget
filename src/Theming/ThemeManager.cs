using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PulseWidget.Core;

namespace PulseWidget.Theming;

public static class ThemeManager
{
    public static WidgetTheme Current { get; private set; } = ThemeCatalog.Find(ThemeCatalog.DefaultId);
    private static ResourceDictionary? styles;

    public static void Apply(string? id) => Apply(ThemeCatalog.Find(id));
    public static void Apply(WidgetTheme theme)
    {
        Current = theme;
        ApplyTo(Application.Current.Resources, theme);
        if (styles is null)
        {
            styles = new ResourceDictionary { Source = new Uri("/PulseWidget;component/Theming/Controls.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(styles);
        }
    }
    public static void ApplyTo(ResourceDictionary resources, WidgetTheme t)
    {
        foreach (var (name, color) in CustomTheme.Palette(t)) resources["Pulse." + name] = Brush(color);
        resources["Pulse.SurfaceBackground"] = Finish(t.Surface, t.SurfaceHighlight);
        resources["Pulse.PanelBackground"] = Finish(t.Panel, t.PanelHighlight);
        resources["Pulse.WidgetRadius"] = new CornerRadius(t.WidgetRadius);
        resources["Pulse.DialogRadius"] = new CornerRadius(t.DialogRadius);
        resources["Pulse.CardRadius"] = new CornerRadius(t.CardRadius);
        resources["Pulse.ControlRadius"] = new CornerRadius(t.ControlRadius);
        var shadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = t.CardShadow };
        shadow.Freeze(); resources["Pulse.CardShadow"] = shadow;
    }
    public static Brush Finish(string color, string? highlight)
    {
        if (highlight is null) return Brush(color);
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(highlight), (Color)ColorConverter.ConvertFromString(color), 65);
        brush.Freeze(); return brush;
    }

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze(); return brush;
    }
    public static void Bind(FrameworkElement element, DependencyProperty property, string role) =>
        element.SetResourceReference(property, "Pulse." + role);
    public static string StateRole(Activity activity) => activity.State switch
    { WorkState.Working => "Working", WorkState.Waiting => "Waiting", _ => "Muted" };
}
