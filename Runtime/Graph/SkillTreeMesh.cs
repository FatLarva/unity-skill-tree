using UnityEngine;

namespace Game.SkillTree
{
    internal static class SkillTreeMesh
    {
        private const int MaximumVertices = 64_000;

        internal static void AddQuad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            if (mesh.currentVertCount + 4 > MaximumVertices)
            {
                return;
            }

            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        internal static void AddFilledDiamond(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            AddQuad(mesh, center + Vector2.up * radius, center + Vector2.right * radius,
                center - Vector2.up * radius, center - Vector2.right * radius, tint);
        }

        internal static void AddDisc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius,
            Color centerColor, Color edgeColor, int sides)
        {
            if (mesh.currentVertCount + sides + 2 > MaximumVertices)
            {
                return;
            }

            int first = mesh.currentVertCount;
            mesh.AddVert(center, centerColor, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                mesh.AddVert(center + offset, edgeColor, Vector2.zero);
                if (i > 0)
                {
                    mesh.AddTriangle(first, first + i, first + i + 1);
                }
            }
        }
    }
}
