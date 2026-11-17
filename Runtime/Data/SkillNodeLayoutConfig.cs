using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Node Layout", fileName = "SkillNodeLayout")]
    public sealed class SkillNodeLayoutConfig : SkillTreeConfigAsset
    {
        [SerializeField]
        private SkillTreeNodeLayout _properties = SkillTreeNodeLayout.CreateDefault();

        public SkillTreeNodeLayout Properties => _properties;
        public int NodeId => _properties.NodeId;
        public int ParentId => _properties.ParentId;
        public Vector2 Position => _properties.Position;
        public Color Color => _properties.Color;
        public SkillTreeIconMode IconMode => _properties.IconMode;
        public SkillTreeNodeIconRenderer IconRenderer => _properties.IconRenderer;
        public Sprite IconSprite => _properties.IconSprite;
        public float IconScale => _properties.IconScale;
        public float IconRotation => _properties.IconRotation;
        public SkillTreeConnectionStyle Connection => _properties.Connection;
        public System.Collections.Generic.IReadOnlyList<SkillTreeVisualConnection> AdditionalParents => _properties.AdditionalParents;

        public void Configure(SkillTreeNodeLayout properties)
        {
            if (!SkillTreeConfigValidation.ValidateNodeLayout(properties, out string error))
            {
                throw new ArgumentException(error, nameof(properties));
            }

            _properties = properties.Copy();
            NotifyChanged();
        }

        public void Capture(SkillTreeNode node)
        {
            if (_properties.Matches(node))
            {
                return;
            }
            Configure(new SkillTreeNodeLayout(node));
        }

        public bool Matches(SkillTreeNode node) => _properties.Matches(node);

        public void ApplyTo(SkillTreeNode node) => _properties.ApplyTo(node);

        public bool Validate(out string error)
        {
            return SkillTreeConfigValidation.ValidateNodeLayout(_properties, out error);
        }
    }
}
