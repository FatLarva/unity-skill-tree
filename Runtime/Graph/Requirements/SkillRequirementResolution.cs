namespace Game.SkillTree.Requirements
{
    public readonly struct SkillRequirementResolution
    {
        public static SkillRequirementResolution Unhandled => default;
        public static SkillRequirementResolution Met => new(true);
        public static SkillRequirementResolution Unmet => new(false);

        public bool Handled { get; }
        public bool IsMet { get; }

        private SkillRequirementResolution(bool isMet)
        {
            Handled = true;
            IsMet = isMet;
        }
    }
}
