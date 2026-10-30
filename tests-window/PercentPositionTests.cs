using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PulseWidget;
using PulseWidget.Core;
using PulseWidget.Theming;

internal static class PercentPositionTests
{
    internal static void Run(Action<bool, string> check)
    {
        ThemeManager.Apply("graphite");
        var settings = new Settings { PositionPercent = 33.3 };
        int saves = 0;
        DetailWindow? first = null, second = null;
        void Save()
        {
            saves++;
            first?.SetPosition(settings.PositionPercent!.Value);
            second?.SetPosition(settings.PositionPercent!.Value);
        }
        try
        {
            first = new DetailWindow(settings, Save, () => { }, () => { }, _ => { });
            second = new DetailWindow(settings, Save, () => { }, () => { }, _ => { });
            var slider = FindSlider(first);
            check(slider.Minimum == 0 && slider.Maximum == 100 && slider.Value == 33.3 && saves == 0, "percentage slider opens without overwriting saved precision");
            slider.Value = 52.4;
            check(settings.PositionPercent == 52.4 && FindSlider(second).Value == 52.4 && saves == 1, "changing one percentage slider synchronizes both details without recursive saves");
            settings.PositionPercent = 100; first.SetPosition(100); second.SetPosition(100);
            check(slider.Value == 100 && FindSlider(second).Value == 100 && saves == 1, "menu position changes refresh sliders without additional writes");
            var input = Find<TextBox>(first);
            var otherInput = Find<TextBox>(second);
            var apply = FindAll<Button>(first).Single(b => b.Content is string s && s == "적용");
            input.Text = "27.25";
            check(settings.PositionPercent == 100 && saves == 1, "typing a percentage does not move the widget before committing");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(settings.PositionPercent == 27.25 && slider.Value == 27.25 && FindSlider(second).Value == 27.25 && otherInput.Text == input.Text && saves == 2, "decimal input synchronizes both sliders and inputs with one save");
            foreach (var invalid in new[] { "", "-1", "100.1", "NaN", "Infinity", "hello", "1,000" })
            {
                input.Text = invalid; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(settings.PositionPercent == 27.25 && saves == 2 && FindAll<TextBlock>(first).Any(t => t.Text == "0~100 사이의 숫자를 입력하세요." && t.Visibility == Visibility.Visible), "invalid input keeps previous position: " + invalid);
            }
            using var source = new HwndSource(new HwndSourceParameters("Pulse numeric input test") { Width = 1, Height = 1, WindowStyle = 0 });
            void Key(Key key) => input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Key(System.Windows.Input.Key.Escape);
            check(input.Text == otherInput.Text && saves == 2, "Escape restores the saved value without closing or writing");
            input.Text = " 50% "; Key(System.Windows.Input.Key.Enter);
            check(settings.PositionPercent == 50 && saves == 3, "Enter accepts a pasted percentage with its suffix");
            input.Text = "0";
            input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, slider) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            check(settings.PositionPercent == 0 && saves == 4, "leaving a valid input commits the left edge");
            input.Text = "100"; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(settings.PositionPercent == 100 && saves == 5, "direct input accepts the right edge");
            settings.PositionPercent = 8.318154219793565; first.SetPosition(settings.PositionPercent.Value);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(settings.PositionPercent == 8.318154219793565 && saves == 5, "unchanged display text never rounds the persisted value");
        }
        finally { first?.Close(); second?.Close(); }
    }
    internal static System.Collections.Generic.IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var found in FindAll<T>(child)) yield return found;
    }
    private static T Find<T>(DependencyObject root) where T : DependencyObject => FindAll<T>(root).First();
    private static Slider FindSlider(DependencyObject root)
    {
        if (root is Slider slider) return slider;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            var match = TryFind(child); if (match is not null) return match;
        }
        throw new Exception("Position slider not found");
    }
    private static Slider? TryFind(DependencyObject root)
    {
        if (root is Slider slider) return slider;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        { var match = TryFind(child); if (match is not null) return match; }
        return null;
    }
}
