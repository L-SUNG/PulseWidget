using System.Drawing;
using System.Windows.Forms;
using PulseWidget.Core;

namespace PulseWidget.Native;

internal static class TrayThemeRenderer
{
    internal static void Apply(ContextMenuStrip menu, WidgetTheme theme)
    {
        var renderer = new ToolStripProfessionalRenderer(new Palette(theme)) { RoundedEdges = false };
        void Update(ToolStrip strip)
        {
            strip.Renderer = renderer;
            strip.BackColor = ColorTranslator.FromHtml(theme.Panel);
            strip.ForeColor = ColorTranslator.FromHtml(theme.Text);
            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = strip.ForeColor;
                if (item is ToolStripDropDownItem child) Update(child.DropDown);
            }
        }
        Update(menu);
    }
    private sealed class Palette(WidgetTheme theme) : ProfessionalColorTable
    {
        private readonly Color panel = ColorTranslator.FromHtml(theme.Panel), hover = ColorTranslator.FromHtml(theme.Hover), border = ColorTranslator.FromHtml(theme.Border);
        public override Color ToolStripDropDownBackground => panel;
        public override Color ImageMarginGradientBegin => panel;
        public override Color ImageMarginGradientMiddle => panel;
        public override Color ImageMarginGradientEnd => panel;
        public override Color MenuItemSelected => hover;
        public override Color MenuItemSelectedGradientBegin => hover;
        public override Color MenuItemSelectedGradientEnd => hover;
        public override Color MenuItemPressedGradientBegin => hover;
        public override Color MenuItemPressedGradientMiddle => hover;
        public override Color MenuItemPressedGradientEnd => hover;
        public override Color MenuItemBorder => border;
        public override Color MenuBorder => border;
        public override Color CheckBackground => hover;
        public override Color CheckSelectedBackground => hover;
        public override Color CheckPressedBackground => hover;
        public override Color SeparatorDark => border;
        public override Color SeparatorLight => panel;
    }
}
