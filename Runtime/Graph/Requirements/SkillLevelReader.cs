using System;
using System.Collections.Generic;
using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    public sealed class SkillLevelReader : ISkillLevelReader
    {
        private readonly IReadOnlyDictionary<int, int> _levels;

        public SkillLevelReader(IReadOnlyDictionary<int, int> levels)
        {
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
        }

        public int GetLevel(SkillNodeConfig node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            return _levels.TryGetValue(node.Id, out int level) ? Math.Clamp(level, 0, node.MaxLevel) : 0;
        }
    }
}
