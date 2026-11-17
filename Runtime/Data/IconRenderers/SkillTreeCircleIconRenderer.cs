using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Icon Renderers/Circle", fileName = "CircleIconRenderer")]
    public sealed class SkillTreeCircleIconRenderer : SkillTreeNodeIconRenderer
    {
        private readonly SkillTreeStroke.Point[] _outline = new SkillTreeStroke.Point[32];

        public override string DisplayName => "Circle";
        public override int MaximumVertexCount => 160;

        public override void DrawIcon(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node, SkillTreeNodeIconContext context)
        {
            Color tint = node.Color.WithAlpha(node.Color.a * context.Opacity);
            for (int index = 0; index < _outline.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _outline.Length;
                _outline[index] = new SkillTreeStroke.Point(
                    context.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * context.Radius, 0f);
            }

            SkillTreeStroke.Ribbon(mesh, _outline, context.Zoom, tint, tint, context.Pixels, closed: true);
            SkillTreeMesh.AddDisc(mesh, context.Center, context.Radius * 0.55f,
                tint.WithAlpha(tint.a * 0.3f), tint.WithAlpha(0f), 24);
        }
    }
}
