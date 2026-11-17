using System;
using System.Collections.Generic;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    internal sealed class SkillTreeRevealEffects
    {
        internal const int TravelVertexBudget = 1400;
        internal const int PurchaseVertexBudget = 2600;

        private const int MaximumVertices = 64_000;
        private const int GlowSides = 12;
        private const int PurchaseSparkCount = 40;
        private const int PurchaseRingSides = 64;

        private readonly List<SkillTreeStroke.Point> _prefix = new(129);
        private readonly List<SkillTreeStroke.Point> _tail = new(129);
        private readonly List<float> _distances = new(129);
        private readonly SkillTreeStroke.Point[] _spark = new SkillTreeStroke.Point[2];

        internal IReadOnlyList<SkillTreeStroke.Point> Prefix(IReadOnlyList<SkillTreeStroke.Point> curve, float progress)
        {
            _prefix.Clear();
            if (curve.Count == 0)
            {
                return _prefix;
            }

            float length = Measure(curve);
            _prefix.Add(curve[0]);
            float distance = Mathf.Clamp01(progress) * length;
            if (distance > 0f)
            {
                for (int i = 1; i < curve.Count && _distances[i] < distance; i++)
                {
                    _prefix.Add(curve[i]);
                }

                _prefix.Add(Sample(curve, distance));
            }

            return _prefix;
        }

        internal void DrawTravel(UnityEngine.UI.VertexHelper mesh, IReadOnlyList<SkillTreeStroke.Point> curve,
            float progress, float timeProgress, float zoom, Color start, Color end, SkillTreeStroke.PixelSpace pixels, float seed)
        {
            float length = Measure(curve);
            float headDistance = length * progress;
            var head = Sample(curve, headDistance);
            Color tint = Color.Lerp(start, end, head.Progress);
            float tailLength = Mathf.Min(length, 72f * zoom);
            float tailStart = Mathf.Max(0f, headDistance - tailLength);
            BuildTail(curve, tailStart, headDistance);

            if (_tail.Count > 1)
            {
                Color first = Color.Lerp(start, end, Sample(curve, tailStart).Progress);
                first = first.WithAlpha(0f);
                SkillTreeStroke.Ribbon(mesh, _tail, 4f * zoom, first, tint.WithAlpha(tint.a * 0.22f), pixels);
                SkillTreeStroke.Ribbon(mesh, _tail, 1.25f * zoom, first, Brighten(tint, 0.4f).WithAlpha(tint.a * 0.9f), pixels);
            }

            DrawTailSparks(mesh, curve, headDistance, tailLength, zoom, start, end, pixels, seed);

            if (timeProgress < 0.18f)
            {
                float radius = (5f + timeProgress * 30f) * zoom;
                Color color = start.WithAlpha(start.a * Mathf.Clamp01((1f - timeProgress / 0.18f) * 0.3f));
                Glow(mesh, curve[0].Position, radius, color, pixels);
            }

            Glow(mesh, head.Position, 7f * zoom, tint.WithAlpha(tint.a * 0.5f), pixels);
            SoftDisc(mesh, head.Position, 1.65f * zoom, Brighten(tint, 0.8f), pixels);

            Color bright = Brighten(tint, 0.65f).WithAlpha(tint.a * 0.7f);
            Vector2 horizontal = Vector2.right * (3.5f * zoom);
            Vector2 vertical = Vector2.up * (3.5f * zoom);
            Spark(mesh, head.Position - horizontal, head.Position + horizontal, 0.6f * zoom, bright, pixels);
            Spark(mesh, head.Position - vertical, head.Position + vertical, 0.6f * zoom, bright, pixels);
        }

        internal void DrawArrival(UnityEngine.UI.VertexHelper mesh, Vector2 center, float progress, float zoom,
            Color color, SkillTreeStroke.PixelSpace pixels, float seed)
        {
            if (progress >= 1f)
            {
                return;
            }

            float remaining = 1f - progress;
            float fade = remaining * remaining;
            float expansion = 1f - Mathf.Pow(remaining, 3f);
            Glow(mesh, center, (5f + expansion * 26f) * zoom, color.WithAlpha(color.a * Mathf.Clamp01(fade * 0.3f)), pixels);
            Glow(mesh, center, (7f + progress * 5f) * zoom, Brighten(color, 0.6f).WithAlpha(color.a * Mathf.Clamp01(fade * 0.5f)), pixels);
            SoftDisc(mesh, center, (2.4f - progress * 1.3f) * zoom, Brighten(color, 0.85f).WithAlpha(color.a * Mathf.Clamp01(fade)), pixels);

            for (int i = 0; i < 10; i++)
            {
                float angle = (i + Hash(seed, i) * 0.65f) * Mathf.PI * 0.2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = (2f + expansion * (18f + Hash(seed, i + 13) * 15f)) * zoom;
                Vector2 point = center + direction * radius;
                float streak = (2f + Hash(seed, i + 23) * 4f) * (1f - progress * 0.5f) * zoom;
                float brightness = 0.35f + Hash(seed, i + 33) * 0.4f;
                float opacity = fade * (0.65f + Hash(seed, i + 43) * 0.35f);
                Color tint = Brighten(color, brightness).WithAlpha(color.a * Mathf.Clamp01(opacity));

                Spark(mesh, point - direction * streak, point, 0.9f * zoom, tint, pixels);
                SoftDisc(mesh, point, (0.85f + Hash(seed, i + 53) * 0.65f) * zoom, tint, pixels);
            }
        }

        internal static float PurchaseRadius(float nodeRadius) => Mathf.Max(8f, nodeRadius) + 54f;

        internal void DrawPurchase(UnityEngine.UI.VertexHelper mesh, Vector2 center, float progress, float zoom,
            float nodeRadius, Color color, SkillTreeStroke.PixelSpace pixels, float seed)
        {
            if (progress < 0f || progress >= 1f || color.a <= 0f)
            {
                return;
            }

            float radius = Mathf.Max(8f, nodeRadius);
            float remaining = 1f - progress;
            float expansion = 1f - remaining * remaining * remaining;
            float fade = remaining * remaining;
            float ringRadius = (radius * 0.75f + expansion * (radius * 0.25f + 29f)) * zoom;
            SoftRing(mesh, center, ringRadius, (2f + progress * 5f) * zoom,
                color.WithAlpha(color.a * fade * 0.85f), pixels);

            float flash = Mathf.Clamp01(1f - progress / 0.25f);
            Glow(mesh, center, (radius + expansion * 15f) * zoom, color.WithAlpha(color.a * fade * 0.35f), pixels);
            Glow(mesh, center, radius * 0.75f * zoom,
                Brighten(color, 0.35f).WithAlpha(color.a * flash * 0.6f), pixels);
            SoftDisc(mesh, center, (2f + flash * 2f) * zoom,
                Brighten(color, 0.65f).WithAlpha(color.a * flash), pixels);

            for (int i = 0; i < PurchaseSparkCount; i++)
            {
                float delay = Hash(seed, i + 101) * 0.07f;
                float lifetime = (1f - delay) * (0.58f + Hash(seed, i + 151) * 0.42f);
                float time = (progress - delay) / lifetime;
                if (time <= 0f || time >= 1f)
                {
                    continue;
                }

                float angle = (i + Hash(seed, i + 201) * 0.85f + seed) * Mathf.PI * 2f / PurchaseSparkCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float rest = 1f - time;
                float travel = 1f - rest * rest * rest;
                float distance = radius * 0.65f + travel * (17f + Hash(seed, i + 251) * 29f);
                Vector2 point = center + direction * (distance * zoom);
                float opacity = Mathf.Clamp01(time / 0.035f) * rest * (0.7f + Hash(seed, i + 301) * 0.3f);
                Color tint = Brighten(color, 0.12f + Hash(seed, i + 351) * 0.4f)
                    .WithAlpha(color.a * opacity);
                float streak = (4f + Hash(seed, i + 401) * 7f) * (1f - time * 0.75f) * zoom;
                float size = (0.7f + Hash(seed, i + 451) * 0.9f) * (1f - time * 0.45f) * zoom;

                Glow(mesh, point, size * 2.4f, tint.WithAlpha(tint.a * 0.28f), pixels);
                Spark(mesh, point - direction * streak, point, size, tint, pixels);
                SoftDisc(mesh, point, size, tint, pixels);
            }
        }

        private static void SoftRing(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius,
            float halfWidth, Color tint, SkillTreeStroke.PixelSpace pixels)
        {
            if (tint.a <= 0f || mesh.currentVertCount + (PurchaseRingSides + 1) * 3 > MaximumVertices)
            {
                return;
            }

            int first = mesh.currentVertCount;
            Color transparent = tint.WithAlpha(0f);
            for (int i = 0; i <= PurchaseRingSides; i++)
            {
                float angle = i * Mathf.PI * 2f / PurchaseRingSides;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float edge = halfWidth + 0.5f / pixels.PixelsPerUnit(center, direction);
                mesh.AddVert(center + direction * Mathf.Max(0f, radius - edge), transparent, Vector2.zero);
                mesh.AddVert(center + direction * radius, tint, Vector2.zero);
                mesh.AddVert(center + direction * (radius + edge), transparent, Vector2.zero);
                if (i == 0)
                {
                    continue;
                }

                int previous = first + (i - 1) * 3;
                int current = previous + 3;
                for (int band = 0; band < 2; band++)
                {
                    mesh.AddTriangle(previous + band, current + band, current + band + 1);
                    mesh.AddTriangle(previous + band, current + band + 1, previous + band + 1);
                }
            }
        }

        private void DrawTailSparks(UnityEngine.UI.VertexHelper mesh, IReadOnlyList<SkillTreeStroke.Point> curve,
            float headDistance, float tailLength, float zoom, Color start, Color end, SkillTreeStroke.PixelSpace pixels, float seed)
        {
            if (tailLength <= 0.00001f)
            {
                return;
            }

            for (int i = 0; i < 7; i++)
            {
                float behind = (i + 0.4f + Hash(seed, i) * 0.5f) / 7.5f * tailLength;
                float distance = headDistance - behind;
                if (distance <= 0f)
                {
                    continue;
                }

                var point = Sample(curve, distance);
                float brightness = (1f - behind / tailLength) * (0.65f + Hash(seed, i + 11) * 0.35f);
                Color color = Brighten(Color.Lerp(start, end, point.Progress), 0.55f);
                color = color.WithAlpha(color.a * Mathf.Clamp01(brightness));
                float streak = (3f + Hash(seed, i + 21) * 4f) * zoom;
                var before = Sample(curve, Mathf.Max(0f, distance - streak));

                Spark(mesh, before.Position, point.Position, 0.85f * zoom, color, pixels);
                Glow(mesh, point.Position, (2f + Hash(seed, i + 31)) * zoom, color.WithAlpha(color.a * 0.32f), pixels);
            }
        }

        private float Measure(IReadOnlyList<SkillTreeStroke.Point> curve)
        {
            _distances.Clear();
            float distance = 0f;
            _distances.Add(distance);
            for (int i = 1; i < curve.Count; i++)
            {
                distance += Vector2.Distance(curve[i - 1].Position, curve[i].Position);
                _distances.Add(distance);
            }

            return distance;
        }

        private SkillTreeStroke.Point Sample(IReadOnlyList<SkillTreeStroke.Point> curve, float distance)
        {
            if (distance <= 0f || curve.Count == 1)
            {
                return curve[0];
            }

            int last = curve.Count - 1;
            if (distance >= _distances[last])
            {
                return curve[last];
            }

            int low = 1;
            int high = last;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (_distances[middle] < distance)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            int before = low - 1;
            float fraction = (distance - _distances[before]) / Mathf.Max(0.000001f, _distances[low] - _distances[before]);
            return new SkillTreeStroke.Point(
                Vector2.Lerp(curve[before].Position, curve[low].Position, fraction),
                Mathf.Lerp(curve[before].Progress, curve[low].Progress, fraction));
        }

        private void BuildTail(IReadOnlyList<SkillTreeStroke.Point> curve, float from, float to)
        {
            _tail.Clear();
            float length = to - from;
            if (length < 0.000001f)
            {
                return;
            }

            _tail.Add(new SkillTreeStroke.Point(Sample(curve, from).Position, 0f));
            for (int i = 1; i < curve.Count; i++)
            {
                float distance = _distances[i];
                if (distance > from && distance < to)
                {
                    _tail.Add(new SkillTreeStroke.Point(curve[i].Position, (distance - from) / length));
                }
            }

            _tail.Add(new SkillTreeStroke.Point(Sample(curve, to).Position, 1f));
        }

        private void Spark(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to,
            float width, Color tint, SkillTreeStroke.PixelSpace pixels)
        {
            if ((to - from).sqrMagnitude < 0.00000001f)
            {
                return;
            }

            _spark[0] = new SkillTreeStroke.Point(from, 0f);
            _spark[1] = new SkillTreeStroke.Point(to, 1f);
            SkillTreeStroke.Ribbon(mesh, _spark, width, tint.WithAlpha(tint.a * 0.3f), tint, pixels);
        }

        private static void Glow(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius,
            Color tint, SkillTreeStroke.PixelSpace pixels)
        {
            if (tint.a <= 0f || mesh.currentVertCount + GlowSides + 2 > MaximumVertices)
            {
                return;
            }

            radius += 0.5f / pixels.PixelsPerUnit(center, Vector2.right);
            SkillTreeMesh.AddDisc(mesh, center, radius, tint, tint.WithAlpha(0f), GlowSides);
        }

        private static void SoftDisc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius,
            Color tint, SkillTreeStroke.PixelSpace pixels)
        {
            if (tint.a <= 0f || mesh.currentVertCount + 1 + (GlowSides + 1) * 2 > MaximumVertices)
            {
                return;
            }

            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            Color transparent = tint.WithAlpha(0f);
            for (int i = 0; i <= GlowSides; i++)
            {
                float angle = i * Mathf.PI * 2f / GlowSides;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float halfPixel = 0.5f / pixels.PixelsPerUnit(center, normal);
                mesh.AddVert(center + normal * Mathf.Max(0f, radius - halfPixel), tint, Vector2.zero);
                mesh.AddVert(center + normal * (radius + halfPixel), transparent, Vector2.zero);

                if (i > 0)
                {
                    int previous = first + 1 + (i - 1) * 2;
                    int current = previous + 2;
                    mesh.AddTriangle(first, previous, current);
                    mesh.AddTriangle(previous, previous + 1, current + 1);
                    mesh.AddTriangle(previous, current + 1, current);
                }
            }
        }

        private static Color Brighten(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, amount).WithAlpha(color.a);
        }

        private static float Hash(float seed, int index)
        {
            unchecked
            {
                uint value = (uint)BitConverter.SingleToInt32Bits(seed) + (uint)index * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0xFFFFFFu) / 16777215f;
            }
        }
    }
}
