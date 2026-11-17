using UnityEngine;
using UnityTools;

namespace Game.SkillTree.LargeSample
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Icon Renderers/Large Sample Arrow", fileName = "ArrowIconRenderer")]
    public sealed class SkillTreeDemoIconRenderer : SkillTreeNodeIconRenderer
    {
        public override string DisplayName => "Arrow";

        public override void DrawIcon(
            UnityEngine.UI.VertexHelper mesh,
            SkillTreeNode node,
            SkillTreeNodeIconContext context)
        {
            var center = context.Center;
            var radius = context.Radius;
            var tint = node.Color.WithAlpha(node.Color.a * context.Opacity);
            var index = mesh.currentVertCount;

            mesh.AddVert(center + new Vector2(radius, 0), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(0, -radius * 0.75f), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(0, radius * 0.75f), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);

            index = mesh.currentVertCount;
            mesh.AddVert(center + new Vector2(-radius, -radius * 0.25f), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(0, -radius * 0.25f), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(0, radius * 0.25f), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(-radius, radius * 0.25f), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 2, index + 1);
            mesh.AddTriangle(index, index + 3, index + 2);
        }
    }
}


