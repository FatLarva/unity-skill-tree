using System;
using System.Collections.Generic;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private const int StarCount = 150;
        private const int MaxCurvePoints = 129;
        private const int GeometryBudget = 62_000;

        private static readonly Vector3[] Stars = CreateStars();

        private readonly List<SkillTreeStroke.Point> _curve = new(MaxCurvePoints);
        private readonly Dictionary<int, Vector2> _outgoingDirections = new();
        private SkillTreeStroke.PixelSpace _pixels;
        private Vector2 _pixelScale;

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            _pixels = new SkillTreeStroke.PixelSpace(this);
            _pixelScale = _pixels.Scale;

            mesh.Clear();
            DrawBackground(mesh, rectTransform.rect);
        }

        private void RefreshPixelScale()
        {
            Vector2 scale = new SkillTreeStroke.PixelSpace(this).Scale;
            if ((scale - _pixelScale).sqrMagnitude > 0.000001f)
            {
                _pixelScale = scale;
                SetVerticesDirty();
            }
        }

        private static void DrawBackground(UnityEngine.UI.VertexHelper mesh, Rect rect)
        {
            SkillTreeMesh.AddQuad(mesh,
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), Color.black);

            for (int i = 0; i < Stars.Length; i++)
            {
                Vector3 star = Stars[i];
                var position = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, star.x), Mathf.Lerp(rect.yMin, rect.yMax, star.y));
                var color = new Color(0.73f, 0.74f, 0.58f, star.z);
                SkillTreeMesh.AddDisc(mesh, position, i % 9 == 0 ? 1f : 0.55f, color, Color.clear, 8);
            }
        }

        private void DrawConnections()
        {
            UpdateOutgoingDirections();
            foreach (var node in _nodes)
            {
                if (!IsShown(node)) continue;
                for (int edge = 0; edge < node.IncomingConnectionCount; edge++)
                {
                    var parent = GetConnectionParent(node, edge);
                    if (parent == null || !IsShown(parent)) continue;
                    bool revealing = _reveals.TryGetValue(node.Id, out var reveal);
                    if (!revealing || !WaitingForParent(reveal))
                    {
                        DrawConnection(null, parent, node, edge,
                            edge == 0 ? reveal : null, MaxCurvePoints, 3);
                    }
                }
            }
        }

        private void DrawConnection(UnityEngine.UI.VertexHelper mesh, SkillTreeNode parent, SkillTreeNode node,
            int edge, RevealState reveal, int pointBudget, int strokePasses)
        {
            TryGetConnectionHandles(node, edge, out var start, out var end);
            Vector2 a = ToCanvasPoint(parent.Position);
            Vector2 b = ToCanvasPoint(parent.Position + start);
            Vector2 c = ToCanvasPoint(node.Position + end);
            Vector2 d = ToCanvasPoint(node.Position);
            var style = node.GetIncomingConnection(edge).Connection;
            bool travelling = reveal != null && reveal.Elapsed < reveal.Travel;
            Rect viewport = GetConnectionViewport(5f * style.Width / 1.2f * _zoom);
            bool hullVisible = ConnectionHullIntersectsViewport(a, b, c, d, viewport);
            if (!travelling && !hullVisible) return;

            SkillTreeStroke.Bezier(_curve, a, b, c, d, _pixels, pointBudget, _curveTolerancePixels);
            if (travelling)
            {
                // The travelling glow uses the complete path even when the
                // connection itself is outside the viewport.
                reveal.Path.Clear();
                reveal.Path.AddRange(_curve);
            }

            IReadOnlyList<SkillTreeStroke.Point> path = travelling ? _revealEffects.Prefix(_curve, TravelProgress(reveal)) : _curve;
            if (!hullVisible || strokePasses < 1 || !ConnectionPathIntersectsViewport(path, viewport)) return;

            // Reserve only a visible connection's actual tessellation. A reveal
            // prefix may contain one additional interpolated endpoint.
            mesh ??= ConnectionGeometryFor((path.Count * 4 + 8) * Mathf.Min(strokePasses, 3));
            float opacity = GetOpacity(node) * RevealOpacity(parent) * (edge == 0 ? 1f : RevealOpacity(node));
            Color startColor = style.UseNodeColors ? parent.Color : style.StartColor;
            Color endColor = style.UseNodeColors ? node.Color : style.EndColor;
            float widthScale = style.Width / 1.2f;
            if (strokePasses >= 3)
            {
                DrawConnectionStroke(mesh, path, 5f * widthScale, startColor, endColor, opacity * 0.045f);
            }
            if (strokePasses >= 2)
            {
                DrawConnectionStroke(mesh, path, 2.5f * widthScale, startColor, endColor, opacity * 0.17f);
            }
            if (strokePasses >= 1)
            {
                DrawConnectionStroke(mesh, path, style.Width, startColor, endColor, opacity * 0.9f);
            }
        }

        private void DrawConnectionStroke(UnityEngine.UI.VertexHelper mesh, IReadOnlyList<SkillTreeStroke.Point> path,
            float width, Color start, Color end, float opacity)
        {
            SkillTreeStroke.Ribbon(mesh, path, width * _zoom,
                start.WithAlpha(start.a * opacity), end.WithAlpha(end.a * opacity), _pixels);
        }

        private Vector2 GetTangent(SkillTreeNode node)
        {
            var parent = GetParent(node);
            if (parent == null || !IsShown(parent))
            {
                return Vector2.zero;
            }

            Vector2 incoming = (node.Position - parent.Position).normalized;
            if (_outgoingDirections.TryGetValue(node.Id, out Vector2 outgoing) && outgoing != Vector2.zero)
            {
                return (incoming * 0.72f + outgoing.normalized * 0.28f).normalized;
            }

            return incoming;
        }

        private void DrawNodeDecorations()
        {
            foreach (var node in _nodes)
            {
                if (!IsShown(node) || !IsNodeInViewport(node))
                {
                    continue;
                }

                if (HasPurchaseGlow(node)) continue;
                Vector2 center = ToCanvasPoint(node.Position);
                float radius = GetBaseRadius(node) * _zoom;
                float reveal = RevealOpacity(node);
                var context = new SkillTreeNodeIconContext(center, radius, _zoom, GetOpacity(node) * reveal, reveal, _pixels);
                DrawNodeHalo(GeometryFor(42), node, context, 0.13f);
            }
        }

        private void DrawNodeHalo(UnityEngine.UI.VertexHelper mesh, SkillTreeNode node,
            SkillTreeNodeIconContext context, float opacity)
        {
            Color haloColor = node.Color.WithAlpha(node.Color.a * opacity * context.Opacity);
            SkillTreeMesh.AddDisc(mesh, context.Center, GetHaloRadius(node) * _zoom * node.IconScale,
                haloColor, Color.clear, 40);
        }

        private static Vector3[] CreateStars()
        {
            var stars = new Vector3[StarCount];
            var random = new System.Random(792);
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new Vector3(
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    0.12f + (float)random.NextDouble() * 0.13f);
            }

            return stars;
        }
    }
}
