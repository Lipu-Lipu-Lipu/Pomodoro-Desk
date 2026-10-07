namespace PomodoroTimer;

/// <summary>托盘右键菜单的深色渲染器，与主界面配色一致。</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color MenuBg = Color.FromArgb(0x2D, 0x2D, 0x2D);
        private static readonly Color BorderColor = Color.FromArgb(0x45, 0x45, 0x4D);
        private static readonly Color HighlightColor = Color.FromArgb(0x3F, 0x3F, 0x46);

        public override Color ToolStripDropDownBackground => MenuBg;
        public override Color ImageMarginGradientBegin => MenuBg;
        public override Color ImageMarginGradientMiddle => MenuBg;
        public override Color ImageMarginGradientEnd => MenuBg;
        public override Color MenuBorder => BorderColor;
        public override Color MenuItemBorder => HighlightColor;
        public override Color MenuItemSelected => HighlightColor;
        public override Color MenuItemSelectedGradientBegin => HighlightColor;
        public override Color MenuItemSelectedGradientEnd => HighlightColor;
        public override Color MenuItemPressedGradientBegin => HighlightColor;
        public override Color MenuItemPressedGradientEnd => HighlightColor;
        public override Color SeparatorDark => BorderColor;
        public override Color SeparatorLight => BorderColor;
        public override Color MenuStripGradientBegin => MenuBg;
        public override Color MenuStripGradientEnd => MenuBg;
    }
}
