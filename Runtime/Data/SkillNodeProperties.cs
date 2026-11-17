using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [Serializable]
    public struct SkillNodeProperties
    {
        [SerializeField, Tooltip("Stable identifier used by layouts and saved progress. Keep it unchanged after authoring.")]
        private int _id;
        [SerializeField] private string _label;
        [SerializeField] private string _title;
        [SerializeField, TextArea(3, 8)] private string _description;
        [SerializeField] private string _category;
        [SerializeField] private SkillTreeNodeKind _kind;
        [SerializeField] private bool _hideUntilAvailable;
        [SerializeField, Tooltip("Visibility conditions when Hide if unavailable is enabled. Leave empty to use the next level's Requirements. Learned nodes remain visible.")]
        private SkillRequirement[] _showRequirements;
        [SerializeField, Tooltip("One entry per purchasable level, in order, with its own cost, description, prerequisites, and effects.")]
        private SkillLevel[] _levels;

        public int Id => _id;
        public string Label => _label;
        public string Title => _title;
        public string Description => _description;
        public string Category => _category;
        public SkillTreeNodeKind Kind => _kind;
        public bool HideUntilAvailable => _hideUntilAvailable;
        public IReadOnlyList<SkillRequirement> ShowRequirements => _showRequirements ?? Array.Empty<SkillRequirement>();
        public IReadOnlyList<SkillLevel> Levels => _levels ?? Array.Empty<SkillLevel>();
        public int MaxLevel => _levels?.Length ?? 0;

        public SkillNodeProperties(int id, string label, string title, string description,
            string category, SkillTreeNodeKind kind, bool hideUntilAvailable,
            SkillLevel[] levels, SkillRequirement[] showRequirements = null)
        {
            _id = id;
            _label = label ?? string.Empty;
            _title = title ?? string.Empty;
            _description = description ?? string.Empty;
            _category = category ?? string.Empty;
            _kind = kind;
            _hideUntilAvailable = hideUntilAvailable;
            _showRequirements = showRequirements == null ? Array.Empty<SkillRequirement>() : (SkillRequirement[])showRequirements.Clone();
            _levels = CopyLevels(levels);
        }

        public static SkillNodeProperties CreateDefault()
        {
            return new SkillNodeProperties(0, string.Empty, string.Empty,
                string.Empty, string.Empty, SkillTreeNodeKind.Skill, false,
                new[] { new SkillLevel(0) });
        }

        internal SkillNodeProperties Copy()
        {
            return new SkillNodeProperties(_id, _label, _title, _description, _category,
                _kind, _hideUntilAvailable, _levels, _showRequirements);
        }

        internal void AssignIdentifier(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            _id = id;
        }

        private static SkillLevel[] CopyLevels(SkillLevel[] levels)
        {
            if (levels == null)
            {
                return Array.Empty<SkillLevel>();
            }

            var copy = new SkillLevel[levels.Length];
            for (int index = 0; index < levels.Length; index++)
            {
                copy[index] = levels[index].Copy();
            }
            return copy;
        }
    }
}
