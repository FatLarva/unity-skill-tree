using System;
using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    /// <summary>Reads the current session's progress without storing it in authoring assets.</summary>
    public sealed class SkillNodeLevelCheck
    {
        private readonly ISkillLevelReader _levels;

        public SkillNodeLevelCheck(ISkillLevelReader levels) =>
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));

        public bool IsMet(SkillNodeLevelRequirementType.Parameters parameters) =>
            _levels.GetLevel(parameters.Node) >= parameters.Level;
    }
}
