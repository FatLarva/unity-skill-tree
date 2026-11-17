namespace Game.SkillTree
{
    public enum SkillTreeLayoutPreset
    {
        TreeDown,
        TreeUp,
        Fractal,
        Radial,
        Circle,
        CenteredCircle
    }

    public enum SkillTreeArrangementScope
    {
        All,
        SelectedSubtreeOnly,
        SelectedNodeOnly
    }

    public sealed class SkillTreeArrangementSettings
    {
        public SkillTreeLayoutPreset Preset { get; set; }
        public SkillTreeArrangementScope Scope { get; set; }
        public float HorizontalSpacing { get; set; } = 160f;
        public float VerticalSpacing { get; set; } = 150f;
        public float BranchAngle { get; set; } = 55f;
        public float LengthScale { get; set; } = 0.75f;
        public float Radius { get; set; } = 300f;
        public float Rotation { get; set; }
    }
}
