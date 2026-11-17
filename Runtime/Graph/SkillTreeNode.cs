using System;
using UnityEngine;

namespace Game.SkillTree
{
    public enum SkillTreeNodeKind
    {
        Root,
        Branch,
        Skill
    }

    public enum SkillTreeNodeState
    {
        Unlocked,
        Available,
        Locked,
        Disabled
    }

    public enum SkillTreeIconMode
    {
        GraphicIcon,
        Sprite
    }

    public sealed class SkillTreeNode
    {
        private SkillTreeVisualConnection[] _additionalParents = Array.Empty<SkillTreeVisualConnection>();

        public int Id { get; }
        public int ParentId { get; set; }
        public string Label { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Requirement { get; set; }
        public Vector2 Position { get; set; }
        public Color Color { get; set; } = new Color(0.93f, 0.75f, 0.47f);
        public SkillTreeIconMode IconMode { get; set; }
        public SkillTreeNodeIconRenderer IconRenderer { get; set; }
        public Sprite IconSprite { get; set; }
        public float IconScale { get; set; } = 1f;
        public float IconRotation { get; set; }
        public SkillTreeConnectionStyle Connection = SkillTreeConnectionStyle.Default;
        public SkillTreeVisualConnection[] AdditionalParents
        {
            get => _additionalParents;
            set => _additionalParents = value ?? Array.Empty<SkillTreeVisualConnection>();
        }
        public SkillTreeNodeKind Kind { get; set; } = SkillTreeNodeKind.Skill;
        public SkillTreeNodeState State { get; set; } = SkillTreeNodeState.Available;
        public int Level { get; set; }
        public int MaxLevel { get; set; } = 3;
        public int Cost { get; set; }
        public bool CanPurchase { get; set; }
        public bool Visible { get; set; } = true;
        public int IncomingConnectionCount => 1 + (AdditionalParents?.Length ?? 0);

        public SkillTreeVisualConnection GetIncomingConnection(int index) => index == 0
            ? new SkillTreeVisualConnection(ParentId, Connection)
            : AdditionalParents[index - 1];

        public SkillTreeNode(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("A skill tree node needs an ID.", nameof(id));
            }

            Id = id;
        }

        public SkillTreeNode Clone()
        {
            var copy = (SkillTreeNode)MemberwiseClone();
            copy.AdditionalParents = AdditionalParents == null ? Array.Empty<SkillTreeVisualConnection>() : (SkillTreeVisualConnection[])AdditionalParents.Clone();
            return copy;
        }

        public bool HasSameParents(SkillTreeNode other)
        {
            if (other == null || ParentId != other.ParentId || (AdditionalParents?.Length ?? 0) != (other.AdditionalParents?.Length ?? 0))
            {
                return false;
            }
            for (int index = 0; index < (AdditionalParents?.Length ?? 0); index++)
            {
                if (AdditionalParents[index].ParentId != other.AdditionalParents[index].ParentId) return false;
            }
            return true;
        }

        public bool HasSameAdditionalConnections(SkillTreeNode other)
        {
            if (other == null || (AdditionalParents?.Length ?? 0) != (other.AdditionalParents?.Length ?? 0)) return false;
            for (int index = 0; index < (AdditionalParents?.Length ?? 0); index++)
            {
                if (!AdditionalParents[index].Equals(other.AdditionalParents[index])) return false;
            }
            return true;
        }

        public bool HasSameValues(SkillTreeNode other)
        {
            return other != null && Id == other.Id && ParentId == other.ParentId && Label == other.Label &&
                   Title == other.Title && Description == other.Description && Category == other.Category &&
                   Requirement == other.Requirement && Position == other.Position && Color == other.Color &&
                   IconMode == other.IconMode && IconRenderer == other.IconRenderer &&
                   IconSprite == other.IconSprite && IconScale == other.IconScale &&
                   IconRotation == other.IconRotation && Connection.Equals(other.Connection) && HasSameAdditionalConnections(other) && Kind == other.Kind &&
                   State == other.State && Level == other.Level && MaxLevel == other.MaxLevel && Cost == other.Cost &&
                   CanPurchase == other.CanPurchase && Visible == other.Visible;
        }
    }
}
