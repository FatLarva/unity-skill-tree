using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private static float GetProgressRingRadius(SkillTreeNode node)
        {
            // Leave room for the corners of both sprite and custom Graphic icons.
            return GetRadius(node) * 1.4f * Mathf.Sqrt(2f) + 3f;
        }

        private static float GetSelectionRadius(SkillTreeNode node) =>
            node.MaxLevel > 0 ? GetProgressRingRadius(node) + 7f : GetRadius(node) + 8f;

        private static float GetLabelRadius(SkillTreeNode node) =>
            node.MaxLevel > 0 ? GetProgressRingRadius(node) + 4f : GetRadius(node);
    }
}
