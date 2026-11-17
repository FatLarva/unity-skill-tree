using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private bool IsNodeInViewport(SkillTreeNode node)
        {
            // Include the rotated icon's corners, halo and both rings so a
            // node stays active until all of its decoration has left the view.
            float radius = Mathf.Max(GetBaseRadius(node) * 1.4f * Mathf.Sqrt(2f), GetHaloRadius(node)) * node.IconScale;
            if (node.MaxLevel > 0) radius = Mathf.Max(radius, GetProgressRingRadius(node) + 5f);
            if (_selectedId == node.Id) radius = Mathf.Max(radius, GetSelectionRadius(node) + 1f);
            return IntersectsViewport(ToCanvasPoint(node.Position), Vector2.one * (radius * _zoom + 2f));
        }

        private bool IntersectsViewport(Vector2 center, Vector2 halfSize)
        {
            Rect viewport = rectTransform.rect;
            return center.x + halfSize.x >= viewport.xMin && center.x - halfSize.x <= viewport.xMax &&
                center.y + halfSize.y >= viewport.yMin && center.y - halfSize.y <= viewport.yMax;
        }

        private static float GetHaloRadius(SkillTreeNode node)
        {
            return node.Kind switch
            {
                SkillTreeNodeKind.Root => 74f,
                SkillTreeNodeKind.Skill => 31f,
                _ => 16f
            };
        }
    }
}
