using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Icon Renderers/Star", fileName = "StarIconRenderer")]
    public sealed class SkillTreeStarIconRenderer : SkillTreeNodeIconRenderer
    {
        private readonly SkillTreeStroke.Point[] _outline = new SkillTreeStroke.Point[10];

        public override string DisplayName => "Star";
        public override int MaximumVertexCount => 64;

        public override void DrawIcon(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node, SkillTreeNodeIconContext context)
        {
            Color tint = node.Color.WithAlpha(node.Color.a * context.Opacity);
            for (int index = 0; index < _outline.Length; index++)
            {
                float angle = Mathf.PI * 0.5f + index * Mathf.PI * 2f / _outline.Length;
                float radius = context.Radius * (index % 2 == 0 ? 1f : 0.45f);
                _outline[index] = new SkillTreeStroke.Point(
                    context.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 0f);
            }

            SkillTreeStroke.Ribbon(mesh, _outline, context.Zoom, tint, tint, context.Pixels, closed: true);
            SkillTreeMesh.AddDisc(mesh, context.Center, context.Radius * 0.25f, tint, tint.WithAlpha(0f), 12);
        }
    }
}
