using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Icon Renderers/Sun", fileName = "SunIconRenderer")]
    public sealed class SkillTreeSunIconRenderer : SkillTreeNodeIconRenderer
    {
        private const int DiscSides = 32;
        private const int RayCount = 8;
        private readonly SkillTreeStroke.Point[] _rim = new SkillTreeStroke.Point[DiscSides];

        public override string DisplayName => "Sun";
        public override int MaximumVertexCount => DiscSides * 5 + 2 + RayCount * 16;

        public override void DrawIcon(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node, SkillTreeNodeIconContext context)
        {
            Color tint = node.Color.WithAlpha(node.Color.a * context.Opacity);
            Color center = Color.Lerp(tint, Color.white.WithAlpha(tint.a), 0.42f);
            float discRadius = context.Radius * 0.53f;
            SkillTreeMesh.AddDisc(mesh, context.Center, discRadius, center, tint, DiscSides);

            for (int index = 0; index < DiscSides; index++)
            {
                float angle = index * Mathf.PI * 2f / DiscSides;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                _rim[index] = new SkillTreeStroke.Point(context.Center + direction * discRadius, 0f);
            }
            SkillTreeStroke.Ribbon(mesh, _rim, context.Radius * 0.025f, tint, tint, context.Pixels, closed: true);

            for (int index = 0; index < RayCount; index++)
            {
                float angle = index * Mathf.PI * 2f / RayCount;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float reach = (index % 2 == 0 ? 1.03f : 0.90f) * context.Radius;
                SkillTreeStroke.Line(mesh, context.Center + direction * (context.Radius * 0.72f),
                    context.Center + direction * reach, context.Radius * 0.13f, tint, context.Pixels);
            }
        }
    }
}
