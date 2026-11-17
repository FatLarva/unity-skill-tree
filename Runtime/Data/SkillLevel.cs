using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [Serializable]
    public struct SkillLevel
    {
        [SerializeField, Min(0)] private int _cost;
        [SerializeField, TextArea(3, 8), Tooltip("Leave empty to use the node's general description.")]
        private string _description;
        [SerializeField, TextArea, Tooltip("Overrides the displayed prerequisite list for this level. Leave empty to show its requirements automatically.")]
        private string _requirementText;
        [SerializeField] private SkillRequirement[] _requirements;
        [SerializeField] private SkillEffect[] _effects;

        public int Cost => _cost;
        public string Description => _description;
        public string RequirementText => _requirementText;
        public IReadOnlyList<SkillRequirement> Requirements => _requirements ?? Array.Empty<SkillRequirement>();
        /// <summary>Authored effect entries. Their managed parameter objects are read-only runtime configuration.</summary>
        public IReadOnlyList<SkillEffect> Effects => _effects ?? Array.Empty<SkillEffect>();

        public string GetRequirementText(string rankLabel)
        {
            if (!string.IsNullOrWhiteSpace(_requirementText)) return _requirementText;
            if (_requirements == null || _requirements.Length == 0) return "No prerequisites.";

            var text = new StringBuilder();
            foreach (var requirement in _requirements)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(requirement.Describe(rankLabel));
            }
            return text.ToString();
        }

        public SkillLevel(int cost, SkillRequirement[] requirements = null, SkillEffect[] effects = null,
            string requirementText = null, string description = null)
        {
            _cost = cost;
            _description = description ?? string.Empty;
            _requirementText = requirementText ?? string.Empty;
            _requirements = requirements == null ? Array.Empty<SkillRequirement>() : (SkillRequirement[])requirements.Clone();
            _effects = effects == null ? Array.Empty<SkillEffect>() : (SkillEffect[])effects.Clone();
        }

        /// <summary>Copies entry arrays; authored managed parameters are shared for read-only access.</summary>
        internal SkillLevel Copy()
        {
            return new SkillLevel(_cost, _requirements, _effects, _requirementText, _description);
        }
    }
}
