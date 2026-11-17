using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Unscaled seconds for a spark to reach a newly visible node. 0 skips travel.")]
        private float _revealTravelDuration = 0.5f;

        private readonly Dictionary<int, RevealState> _reveals = new();
        private readonly List<int> _revealIds = new();
        private readonly SkillTreeRevealEffects _revealEffects = new();

        public AnimationCurve RevealTravelCurve { get; set; }

        public float RevealTravelDuration
        {
            get => _revealTravelDuration;
            set
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _revealTravelDuration = Mathf.Clamp(value, 0f, 2f);
            }
        }

        private void UpdateReveals(
            IReadOnlyList<SkillTreeNode> incoming,
            IReadOnlyDictionary<int, SkillTreeNode> incomingById)
        {
            BeginRevealFrameUpdate(incoming);
            bool animate = !_suppressNextReveal && isActiveAndEnabled && !Editable && _revealDuration > 0f;
            if (!animate)
            {
                CancelRevealFrame();
                _reveals.Clear();
            }

            foreach (var node in incoming)
            {
                if (!node.Visible)
                {
                    _reveals.Remove(node.Id);
                }
                else if (animate && _previousVisibility.TryGetValue(node.Id, out bool wasVisible) && !wasVisible)
                {
                    bool parentShown = !(node.ParentId == 0) &&
                        incomingById.TryGetValue(node.ParentId, out var parent) && parent.Visible;
                    float travel = parentShown ? _revealTravelDuration : 0f;
                    _reveals[node.Id] = new RevealState(node.Id, travel);
                    _newRevealIds.Add(node.Id);
                }
            }

            _revealIds.Clear();
            foreach (int id in _reveals.Keys)
            {
                if (!incomingById.ContainsKey(id))
                {
                    _revealIds.Add(id);
                }
            }

            foreach (int id in _revealIds)
            {
                _reveals.Remove(id);
            }

            _previousVisibility.Clear();
            foreach (var node in incoming)
            {
                _previousVisibility.Add(node.Id, node.Visible);
            }

            _suppressNextReveal = false;
        }

        private RevealState GetRevealingParent(RevealState reveal)
        {
            if (_nodesById.TryGetValue(reveal.NodeId, out var node) &&
                !(node.ParentId == 0) && _reveals.TryGetValue(node.ParentId, out var parent))
            {
                return parent;
            }

            return null;
        }

        private bool WaitingForParent(RevealState reveal)
        {
            var parent = GetRevealingParent(reveal);
            return parent != null && parent.Elapsed < parent.Travel;
        }

        private float GetParentTravelDelay(RevealState reveal)
        {
            float delay = 0f;
            var parent = GetRevealingParent(reveal);
            for (int depth = 0; parent != null && depth < _reveals.Count; depth++)
            {
                delay += Mathf.Max(0f, parent.Travel - parent.PreviousElapsed);
                parent = GetRevealingParent(parent);
            }

            return delay;
        }

        private static float TravelTimeProgress(RevealState reveal)
        {
            return reveal.Travel <= 0f ? 1f : Mathf.Clamp01(reveal.Elapsed / reveal.Travel);
        }

        private float TravelProgress(RevealState reveal)
        {
            float time = TravelTimeProgress(reveal);
            if (time <= 0f || time >= 1f) return time;

            float cubicEaseOut = 1f - Mathf.Pow(1f - time, 3f);
            float progress = RevealTravelCurve != null && RevealTravelCurve.length > 0
                ? RevealTravelCurve.Evaluate(time)
                : cubicEaseOut;
            return float.IsFinite(progress) ? Mathf.Clamp01(progress) : cubicEaseOut;
        }

        private float ArrivalProgress(RevealState reveal)
        {
            return _revealDuration <= 0f ? 1f : Mathf.Clamp01((reveal.Elapsed - reveal.Travel) / _revealDuration);
        }

        private float RevealOpacity(SkillTreeNode node)
        {
            return _reveals.TryGetValue(node.Id, out var reveal) ? Mathf.SmoothStep(0f, 1f, ArrivalProgress(reveal)) : 1f;
        }

        private static float RevealSeed(int id)
        {
            uint seed = unchecked((uint)id * 2654435761u);

            return (seed % 10_000) / 10_000f;
        }

        private void DrawRevealEffects()
        {
            foreach (var pair in _reveals)
            {
                var reveal = pair.Value;
                if (WaitingForParent(reveal) || !_nodesById.TryGetValue(pair.Key, out var node) || !IsShown(node))
                {
                    continue;
                }

                if (reveal.Elapsed < reveal.Travel)
                {
                    var parent = GetParent(node);
                    if (parent != null && IsShown(parent) && reveal.Path.Count > 0)
                    {
                        float progress = TravelProgress(reveal);
                        if (!IsRevealTravelInViewport(reveal.Path, progress)) continue;

                        var style = node.Connection;
                        Color startColor = style.UseNodeColors ? parent.Color : style.StartColor;
                        Color endColor = style.UseNodeColors ? node.Color : style.EndColor;
                        _revealEffects.DrawTravel(GeometryFor(2500, SkillTreeGeometryPage.LayerKind.Overlay), reveal.Path,
                            progress, TravelTimeProgress(reveal), _zoom,
                            startColor, endColor, _pixels, RevealSeed(node.Id));
                    }
                }
                else
                {
                    Vector2 center = ToCanvasPoint(node.Position);
                    if (!IntersectsViewport(center, Vector2.one * GetRevealViewportPadding(40f))) continue;

                    _revealEffects.DrawArrival(GeometryFor(2500, SkillTreeGeometryPage.LayerKind.Overlay), center, ArrivalProgress(reveal), _zoom,
                        node.Color, _pixels, RevealSeed(node.Id));
                }
            }
        }

        private bool IsRevealTravelInViewport(IReadOnlyList<SkillTreeStroke.Point> path, float progress)
        {
            var prefix = _revealEffects.Prefix(path, progress);
            if (prefix.Count == 0) return false;

            Vector2 minimum = prefix[0].Position;
            Vector2 maximum = minimum;
            for (int i = 1; i < prefix.Count; i++)
            {
                minimum = Vector2.Min(minimum, prefix[i].Position);
                maximum = Vector2.Max(maximum, prefix[i].Position);
            }

            // Include the head, the initial glow and straight sparks joining
            // points of the travelled path. A zero-progress prefix is one point.
            Vector2 halfSize = (maximum - minimum) * 0.5f + Vector2.one * GetRevealViewportPadding(10.4f);
            return IntersectsViewport((minimum + maximum) * 0.5f, halfSize);
        }

        private float GetRevealViewportPadding(float radius)
        {
            float pixelsPerUnit = Mathf.Max(0.001f, Mathf.Min(_pixelScale.x, _pixelScale.y));
            return radius * _zoom + 2f / pixelsPerUnit;
        }

        private void AdvanceReveals(float unscaledDelta)
        {
            if (_revealFrameActive || _reveals.Count == 0 || !float.IsFinite(unscaledDelta) || unscaledDelta <= 0f)
            {
                return;
            }

            _revealIds.Clear();
            _revealIds.AddRange(_reveals.Keys);
            foreach (int id in _revealIds)
            {
                var reveal = _reveals[id];
                reveal.PreviousElapsed = reveal.Elapsed;
            }

            foreach (int id in _revealIds)
            {
                var reveal = _reveals[id];
                reveal.Elapsed += Mathf.Max(0f, unscaledDelta - GetParentTravelDelay(reveal));
            }

            foreach (int id in _revealIds)
            {
                var reveal = _reveals[id];
                if (reveal.Elapsed >= reveal.Travel + _revealDuration)
                {
                    _reveals.Remove(id);
                }

                if (_nodesById.TryGetValue(id, out var node) && _labels.TryGetValue(id, out var label) && label != null)
                {
                    RefreshLabelColor(node, label);
                }
            }

            SetVerticesDirty();
        }

        private sealed class RevealState
        {
            public int NodeId { get; }
            public float Travel { get; }
            public List<SkillTreeStroke.Point> Path { get; } = new(MaxCurvePoints);
            public float Elapsed { get; set; }
            public float PreviousElapsed { get; set; }
            public bool WaitingForFrame { get; set; }

            public RevealState(int nodeId, float travel)
            {
                NodeId = nodeId;
                Travel = travel;
            }
        }
    }
}
