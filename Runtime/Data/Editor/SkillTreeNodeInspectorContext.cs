using System;

namespace Game.SkillTree.Data
{
    public sealed class SkillTreeNodeInspectorContext : SkillTreeInspectorContext
    {
        public SkillNodeConfig Node { get; private set; }
        public SkillNodeLayoutConfig Layout { get; private set; }
        public event Action NodeChanged;

        public void SetNode(SkillNodeConfig node, SkillNodeLayoutConfig layout)
        {
            if (Node == node && Layout == layout) return;
            Node = node;
            Layout = layout;
            name = node != null ? $"Skill · {node.Title}" : "Skill properties";
            NodeChanged?.Invoke();
        }
    }
}
