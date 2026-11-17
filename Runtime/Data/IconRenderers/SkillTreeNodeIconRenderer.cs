using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    public readonly struct SkillTreeNodeIconContext
    {
        public Vector2 Center { get; }
        public float Radius { get; }
        public float Zoom { get; }
        public float Opacity { get; }
        public float RevealOpacity { get; }

        internal SkillTreeStroke.PixelSpace Pixels { get; }

        internal SkillTreeNodeIconContext(Vector2 center, float radius, float zoom, float opacity,
            float revealOpacity, SkillTreeStroke.PixelSpace pixels)
        {
            Center = center;
            Radius = radius;
            Zoom = zoom;
            Opacity = opacity;
            RevealOpacity = revealOpacity;
            Pixels = pixels;
        }
    }

    [CreateAssetMenu(menuName = "Game/Skill Tree/Icon Renderers/Diamond", fileName = "DiamondIconRenderer")]
    public class SkillTreeNodeIconRenderer : ScriptableObject
    {
        public virtual string DisplayName => "Diamond";

        /// <summary>Upper bound for vertices appended by DrawIcon, excluding graph decorations.</summary>
        public virtual int MaximumVertexCount => 128;

        /// <summary>Creates an icon owned by the caller. Override to return an Image or another Graphic.</summary>
        public virtual UnityEngine.UI.Graphic CreateIcon(Transform parent)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(SkillTreeIcon));
            iconObject.transform.SetParent(parent, false);
            var icon = iconObject.GetComponent<SkillTreeIcon>();
            icon.raycastTarget = false;
            return icon;
        }

        /// <summary>Updates a previously created icon. The caller controls its RectTransform.</summary>
        public virtual void UpdateIcon(UnityEngine.UI.Graphic graphic, SkillTreeNode node,
            float opacity = 1f, float revealOpacity = 1f)
        {
            if (graphic is SkillTreeIcon icon)
            {
                icon.SetNode(node, this, opacity, revealOpacity);
            }
            else if (graphic != null)
            {
                graphic.color = node == null ? Color.clear : node.Color.WithAlpha(node.Color.a * opacity);
            }
        }

        /// <summary>
        /// Draws into the default SkillTreeIcon's own mesh, using icon-local coordinates.
        /// Override CreateIcon and UpdateIcon when the icon uses a different Graphic.
        /// </summary>
        public virtual void DrawIcon(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node, SkillTreeNodeIconContext context)
        {
            DrawDefaultIcon(mesh, node, context);
        }

        internal static void TransformVertices(UnityEngine.UI.VertexHelper mesh, int firstVertex,
            Vector2 center, float scale, float rotation)
        {
            if (Mathf.Approximately(scale, 1f) && Mathf.Approximately(rotation, 0f))
            {
                return;
            }

            float radians = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            var vertex = default(UIVertex);
            for (int index = firstVertex; index < mesh.currentVertCount; index++)
            {
                mesh.PopulateUIVertex(ref vertex, index);
                Vector2 offset = ((Vector2)vertex.position - center) * scale;
                vertex.position = center + new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                mesh.SetUIVertex(vertex, index);
            }
        }

        internal static void DrawDefaultIcon(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node, SkillTreeNodeIconContext context)
        {
            Vector2 center = context.Center;
            float zoom = context.Zoom;
            Color color = node.Color;
            Color tint = color.WithAlpha(color.a * context.Opacity);

            if (node.Kind == SkillTreeNodeKind.Root)
            {
                Color outer = color.WithAlpha(color.a * 0.45f * context.RevealOpacity);
                Color fill = color.WithAlpha(color.a * 0.65f * context.RevealOpacity);
                SkillTreeStroke.Diamond(mesh, center, 27f * zoom, outer, context.Pixels, zoom);
                SkillTreeStroke.Diamond(mesh, center, context.Radius, tint, context.Pixels, zoom);
                SkillTreeMesh.AddFilledDiamond(mesh, center, 16f * zoom, fill);

                Vector2 horizontal = Vector2.right * context.Radius;
                Vector2 vertical = Vector2.up * context.Radius;
                SkillTreeStroke.Line(mesh, center - horizontal, center + horizontal, 0.6f * zoom, tint, context.Pixels);
                SkillTreeStroke.Line(mesh, center - vertical, center + vertical, 0.6f * zoom, tint, context.Pixels);
            }
            else
            {
                bool isSkill = node.Kind == SkillTreeNodeKind.Skill;
                bool singleLevel = isSkill && node.MaxLevel == 1;
                if (isSkill)
                {
                    Color outer = color.WithAlpha(color.a * 0.3f * context.Opacity);
                    SkillTreeStroke.Diamond(mesh, center, 14f * zoom, outer, context.Pixels, zoom);
                }

                SkillTreeStroke.Diamond(mesh, center, context.Radius, tint, context.Pixels, zoom);
                if (singleLevel && node.Level > 0)
                {
                    Color outline = color.WithAlpha(color.a * 0.85f * context.Opacity);
                    Color fill = color.WithAlpha(color.a * 0.8f * context.Opacity);
                    SkillTreeStroke.Diamond(mesh, center, 13f * zoom, outline, context.Pixels, zoom);
                    SkillTreeMesh.AddFilledDiamond(mesh, center, 7f * zoom, fill);
                }
                else if (isSkill && !singleLevel)
                {
                    Color fill = color.WithAlpha(color.a * 0.28f * context.Opacity);
                    SkillTreeMesh.AddFilledDiamond(mesh, center, 5f * zoom, fill);
                }
            }

            float centerRadius = node.Kind == SkillTreeNodeKind.Root ? 2.5f : 1.5f;
            SkillTreeMesh.AddDisc(mesh, center, centerRadius * zoom, tint, tint, 10);
        }
    }
}
