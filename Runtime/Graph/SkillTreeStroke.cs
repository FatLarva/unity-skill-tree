using System.Collections.Generic;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    internal static class SkillTreeStroke
    {
        private const int MaximumVertices = 64_000;
        private const float MinimumSegmentLengthSquared = 0.00000001f;
        private const float HalfPixel = 0.5f;

        private static readonly Point[] Segment = new Point[2];
        private static readonly Point[] DiamondCorners = new Point[4];

        internal readonly struct Point
        {
            public Vector2 Position { get; }
            public float Progress { get; }

            public Point(Vector2 position, float progress)
            {
                Position = position;
                Progress = progress;
            }
        }

        internal readonly struct PixelSpace
        {
            private readonly RectTransform _transform;
            private readonly Camera _camera;

            public PixelSpace(UnityEngine.UI.Graphic graphic)
            {
                _transform = graphic.rectTransform;
                var canvas = graphic.canvas;
                _camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && _camera == null)
                {
                    _camera = Camera.main;
                }
            }

            public Vector2 Scale => new(PixelsPerUnit(Vector2.zero, Vector2.right), PixelsPerUnit(Vector2.zero, Vector2.up));

            public Vector2 Project(Vector2 point)
            {
                return RectTransformUtility.WorldToScreenPoint(_camera, _transform.TransformPoint(point));
            }

            public float PixelsPerUnit(Vector2 point, Vector2 direction)
            {
                return Mathf.Max(0.001f, Vector2.Distance(Project(point), Project(point + direction)));
            }
        }

        internal static void Bezier(List<Point> points, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            PixelSpace pixels, int maximumPoints, float tolerancePixels)
        {
            points.Clear();
            points.Add(new Point(a, 0f));
            int depth = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, maximumPoints - 1), 2f)), 0, 7);
            float toleranceSquared = tolerancePixels * tolerancePixels;
            Subdivide(points, a, b, c, d, 0f, 1f, pixels, depth, toleranceSquared);
        }

        internal static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b,
            float width, Color tint, PixelSpace pixels)
        {
            if ((b - a).sqrMagnitude < MinimumSegmentLengthSquared)
            {
                return;
            }

            Segment[0] = new Point(a, 0f);
            Segment[1] = new Point(b, 1f);
            Ribbon(mesh, Segment, width, tint, tint, pixels);
        }

        internal static void Diamond(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius,
            Color tint, PixelSpace pixels, float width)
        {
            DiamondCorners[0] = new Point(center + Vector2.up * radius, 0f);
            DiamondCorners[1] = new Point(center + Vector2.right * radius, 0f);
            DiamondCorners[2] = new Point(center - Vector2.up * radius, 0f);
            DiamondCorners[3] = new Point(center - Vector2.right * radius, 0f);
            Ribbon(mesh, DiamondCorners, width, tint, tint, pixels, closed: true);
        }

        internal static void Ribbon(UnityEngine.UI.VertexHelper mesh, IReadOnlyList<Point> points, float width,
            Color first, Color last, PixelSpace pixels, bool closed = false)
        {
            int count = points.Count;
            if (count < 2 || width <= 0f || mesh.currentVertCount + count * 4 + 8 > MaximumVertices)
            {
                return;
            }

            int start = mesh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Point point = points[i];
                int previousIndex = i > 0 ? i - 1 : closed ? count - 1 : i;
                int nextIndex = i < count - 1 ? i + 1 : closed ? 0 : i;
                Vector2 incoming = point.Position - points[previousIndex].Position;
                Vector2 outgoing = points[nextIndex].Position - point.Position;
                if (incoming.sqrMagnitude < MinimumSegmentLengthSquared)
                {
                    incoming = outgoing;
                }

                if (outgoing.sqrMagnitude < MinimumSegmentLengthSquared)
                {
                    outgoing = incoming;
                }

                incoming.Normalize();
                outgoing.Normalize();
                var normal = new Vector2(-outgoing.y, outgoing.x);
                Vector2 join = normal + new Vector2(-incoming.y, incoming.x);
                if (join.sqrMagnitude < 0.000001f)
                {
                    join = normal;
                }

                join.Normalize();
                float miter = Mathf.Min(2.5f, 1f / Mathf.Max(0.4f, Vector2.Dot(join, normal)));
                float halfPixel = HalfPixel / pixels.PixelsPerUnit(point.Position, join);
                float inner = Mathf.Max(0f, width * 0.5f - halfPixel);
                float outer = width * 0.5f + halfPixel;
                Color tint = Color.Lerp(first, last, point.Progress);
                tint.a *= Mathf.Min(1f, width / Mathf.Max(0.0001f, inner + outer));
                Color transparent = tint.WithAlpha(0f);

                mesh.AddVert(point.Position - join * (outer * miter), transparent, Vector2.zero);
                mesh.AddVert(point.Position - join * (inner * miter), tint, Vector2.zero);
                mesh.AddVert(point.Position + join * (inner * miter), tint, Vector2.zero);
                mesh.AddVert(point.Position + join * (outer * miter), transparent, Vector2.zero);
            }

            int edges = closed ? count : count - 1;
            for (int i = 0; i < edges; i++)
            {
                Connect(mesh, start + i * 4, start + (i + 1) % count * 4);
            }

            if (!closed)
            {
                Vector2 firstDirection = (points[0].Position - points[1].Position).normalized;
                Vector2 lastDirection = (points[count - 1].Position - points[count - 2].Position).normalized;
                AddCap(mesh, start, points[0].Position, firstDirection, pixels, first);
                AddCap(mesh, start + (count - 1) * 4, points[count - 1].Position, lastDirection, pixels, last);
            }
        }

        private static void Subdivide(List<Point> points, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float start, float end, PixelSpace pixels, int depth, float toleranceSquared)
        {
            Vector2 first = pixels.Project(a);
            Vector2 last = pixels.Project(d);
            if (depth == 0 || (DistanceSquared(pixels.Project(b), first, last) <= toleranceSquared &&
                DistanceSquared(pixels.Project(c), first, last) <= toleranceSquared))
            {
                if ((d - points[^1].Position).sqrMagnitude > MinimumSegmentLengthSquared)
                {
                    points.Add(new Point(d, end));
                }

                return;
            }

            Vector2 ab = (a + b) * 0.5f;
            Vector2 bc = (b + c) * 0.5f;
            Vector2 cd = (c + d) * 0.5f;
            Vector2 abc = (ab + bc) * 0.5f;
            Vector2 bcd = (bc + cd) * 0.5f;
            Vector2 middle = (abc + bcd) * 0.5f;
            float midTime = (start + end) * 0.5f;

            Subdivide(points, a, ab, abc, middle, start, midTime, pixels, depth - 1, toleranceSquared);
            Subdivide(points, middle, bcd, cd, d, midTime, end, pixels, depth - 1, toleranceSquared);
        }

        private static float DistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float projection = Mathf.Clamp01(Vector2.Dot(point - a, delta) / Mathf.Max(0.000001f, delta.sqrMagnitude));
            return (point - a - delta * projection).sqrMagnitude;
        }

        private static void Connect(UnityEngine.UI.VertexHelper mesh, int a, int b)
        {
            for (int strip = 0; strip < 3; strip++)
            {
                mesh.AddTriangle(a + strip, a + strip + 1, b + strip + 1);
                mesh.AddTriangle(a + strip, b + strip + 1, b + strip);
            }
        }

        private static void AddCap(UnityEngine.UI.VertexHelper mesh, int edge, Vector2 point, Vector2 direction,
            PixelSpace pixels, Color tint)
        {
            int start = mesh.currentVertCount;
            Vector2 offset = direction * (HalfPixel / pixels.PixelsPerUnit(point, direction));
            Color transparent = tint.WithAlpha(0f);
            var vertex = default(UIVertex);
            for (int i = 0; i < 4; i++)
            {
                mesh.PopulateUIVertex(ref vertex, edge + i);
                mesh.AddVert(vertex.position + (Vector3)offset, transparent, Vector2.zero);
            }

            Connect(mesh, edge, start);
        }
    }
}
