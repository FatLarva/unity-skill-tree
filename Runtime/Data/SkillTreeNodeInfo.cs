using System;

namespace Game.SkillTree.Data
{
    /// <summary>Read-only gameplay and presentation data for a custom skill tree UI.</summary>
    public readonly struct SkillTreeNodeInfo
    {
        public SkillNodeConfig Definition { get; }
        public int Id => Definition.Id;
        public int Level { get; }
        public int MaxLevel => Definition.MaxLevel;
        public int Cost { get; }
        public bool Visible { get; }
        public bool CanPurchase { get; }
        public SkillTreeNodeState State { get; }
        public string Title => Definition.Title;
        public string Description { get; }
        public string Requirement { get; }
        public SkillLevel? NextLevel => Definition.IsRanked && Level < MaxLevel
            ? Definition.Levels[Level] : null;

        internal SkillTreeNodeInfo(SkillNodeConfig definition, int level, int cost,
            bool visible, bool canPurchase, SkillTreeNodeState state,
            string description, string requirement)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Level = level;
            Cost = cost;
            Visible = visible;
            CanPurchase = canPurchase;
            State = state;
            Description = description;
            Requirement = requirement;
        }
    }
}
