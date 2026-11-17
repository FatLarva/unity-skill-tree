using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Node", fileName = "SkillNode")]
    public sealed class SkillNodeConfig : SkillTreeConfigAsset
    {
        [SerializeField]
        private SkillNodeProperties _properties = SkillNodeProperties.CreateDefault();

        [SerializeField, HideInInspector] private SkillTreeConfig _owner;

        public SkillTreeConfig Owner => _owner;

        /// <summary>A root value record with nested arrays shared for read-only access.</summary>
        public SkillNodeProperties Properties => _properties;
        public int Id => _properties.Id;
        public string Label => _properties.Label;
        public string Title => _properties.Title;
        public string Description => _properties.Description;
        public string Category => _properties.Category;
        public SkillTreeNodeKind Kind => _properties.Kind;
        public bool HideUntilAvailable => _properties.HideUntilAvailable;
        public IReadOnlyList<SkillRequirement> ShowRequirements => _properties.ShowRequirements;
        public IReadOnlyList<SkillLevel> Levels => _properties.Levels;
        public int MaxLevel => _properties.MaxLevel;
        public bool IsRanked => MaxLevel > 0 && Kind is SkillTreeNodeKind.Root or SkillTreeNodeKind.Skill;

        public string GetDescriptionForProgress(int acquiredLevels)
        {
            if (!IsRanked) return Description;

            int index = Mathf.Clamp(acquiredLevels, 0, MaxLevel - 1);
            string description = Levels[index].Description;
            return string.IsNullOrWhiteSpace(description) ? Description : description;
        }

        public void Configure(SkillNodeProperties properties)
        {
            if (_owner != null && properties.Id != Id)
                throw new InvalidOperationException("An owned node's identifier cannot be changed.");
            if (!SkillTreeConfigValidation.ValidateNode(properties, out string error))
            {
                throw new ArgumentException(error, nameof(properties));
            }

            _properties = properties.Copy();
            NotifyChanged();
        }

        public bool Validate(out string error)
        {
            return SkillTreeConfigValidation.ValidateNode(_properties, out error);
        }

        internal void BindToTree(SkillTreeConfig tree, int id)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (_owner != null && _owner != tree)
                throw new InvalidOperationException($"Node '{name}' already belongs to tree '{_owner.name}'.");
            if (Id != 0 && Id != id)
                throw new InvalidOperationException($"Node '{name}' already has stable ID {Id}.");
            _properties.AssignIdentifier(id);
            _owner = tree;
            NotifyChanged();
        }
    }
}
