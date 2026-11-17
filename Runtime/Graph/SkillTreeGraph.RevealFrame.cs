using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private const float RevealFramePadding = 24f;
        private const float MinimumFrameZoom = 0.0001f;

        private readonly List<int> _newRevealIds = new();
        private readonly List<int> _newRevealSources = new();
        private readonly HashSet<int> _revealFrameSources = new();
        private readonly HashSet<int> _revealFrameTargetIds = new();
        private readonly List<SkillTreeNode> _revealFrameTargets = new();
        private bool _revealFrameActive;
        private float _revealFrameDuration = 0.35f;
        private float _revealFrameActiveDuration;
        private float _revealFrameElapsed;
        private float _revealFrameStartZoom;
        private float _revealFrameTargetZoom;
        private Vector2 _revealFrameStartCenter;
        private Vector2 _revealFrameTargetPan;
        private float _minimumViewZoom = MinimumZoom;

        public float RevealFrameDuration
        {
            get => _revealFrameDuration;
            set
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _revealFrameDuration = Mathf.Max(0f, value);
            }
        }

        private void BeginRevealFrameUpdate(IEnumerable<SkillTreeNode> incoming)
        {
            _newRevealIds.Clear();
            _newRevealSources.Clear();
            foreach (var node in incoming)
                if (_nodesById.TryGetValue(node.Id, out var previous) && node.Level > previous.Level)
                    _newRevealSources.Add(node.Id);
        }

        private void PrepareRevealFrame()
        {
            Rect viewport = rectTransform.rect;
            var available = new Rect(viewport.xMin + RevealFramePadding, viewport.yMin + RevealFramePadding,
                viewport.width - RevealFramePadding * 2f, viewport.height - RevealFramePadding * 2f);
            if (Editable || !isActiveAndEnabled || _revealDuration <= 0f || available.width <= 1f || available.height <= 1f)
            {
                CancelRevealFrame();
                return;
            }

            bool needsFrame = _revealFrameActive;
            foreach (int id in _newRevealIds)
            {
                if (!_nodesById.TryGetValue(id, out var node) || !node.Visible || !_reveals.ContainsKey(id)) continue;
                Rect bounds = GetRevealNodeBounds(node, _zoom);
                bounds.position += _pan;
                needsFrame |= !ContainsRevealBounds(available, bounds);
            }
            if (!needsFrame) return;

            if (!_revealFrameActive) _revealFrameSources.Clear();
            foreach (int id in _newRevealIds)
                if (_reveals.TryGetValue(id, out var reveal)) reveal.WaitingForFrame = true;
            foreach (int id in _newRevealSources) _revealFrameSources.Add(id);
            if (_newRevealIds.Count > 0 && _newRevealSources.Count == 0 && _selectedId != 0)
                _revealFrameSources.Add(_selectedId);
            _newRevealIds.Clear();
            _newRevealSources.Clear();

            _revealFrameTargets.Clear();
            _revealFrameTargetIds.Clear();
            foreach (var reveal in _reveals.Values)
            {
                if (!reveal.WaitingForFrame || !_nodesById.TryGetValue(reveal.NodeId, out var node) || !node.Visible) continue;
                AddRevealFrameTarget(node.Id);
                for (int edge = 0; edge < node.IncomingConnectionCount; edge++)
                    AddRevealFrameTarget(node.GetIncomingConnection(edge).ParentId);
            }
            if (_revealFrameTargets.Count == 0)
            {
                CancelRevealFrame();
                return;
            }
            foreach (int id in _revealFrameSources) AddRevealFrameTarget(id);

            float targetZoom = _zoom;
            Rect targetBounds = GetRevealFrameBounds(targetZoom);
            bool includeLabels = true;
            Rect smallestBounds = GetRevealFrameBounds(Mathf.Min(MinimumFrameZoom, _zoom));
            if (smallestBounds.width > available.width || smallestBounds.height > available.height)
            {
                // Fully compensated text can be wider than the viewport at
                // every zoom. Fit the nodes instead of shrinking them to dots.
                includeLabels = false;
                targetBounds = GetRevealFrameBounds(targetZoom, includeLabels);
            }
            if (targetBounds.width > available.width || targetBounds.height > available.height)
            {
                float low = Mathf.Min(MinimumFrameZoom, _zoom);
                // Keep the search interval stable across repeated snapshots
                // while the camera is moving, avoiding numerical retargets.
                float high = MaximumZoom;
                for (int iteration = 0; iteration < 20; iteration++)
                {
                    float candidate = (low + high) * 0.5f;
                    Rect bounds = GetRevealFrameBounds(candidate, includeLabels);
                    if (bounds.width <= available.width && bounds.height <= available.height) low = candidate;
                    else high = candidate;
                }
                targetZoom = low;
                targetBounds = GetRevealFrameBounds(targetZoom, includeLabels);
            }
            Vector2 targetPan = available.center - targetBounds.center;
            if (_revealFrameActive && Mathf.Abs(targetZoom / _revealFrameTargetZoom - 1f) < 0.0001f &&
                (targetPan - _revealFrameTargetPan).sqrMagnitude < 0.01f) return;

            CancelWheelZoom();
            _minimumViewZoom = Mathf.Min(_minimumViewZoom, targetZoom);
            _revealFrameActive = true;
            _revealFrameActiveDuration = _revealFrameDuration;
            _revealFrameElapsed = 0f;
            _revealFrameStartZoom = _zoom;
            _revealFrameStartCenter = (viewport.center - _pan) / _zoom;
            _revealFrameTargetZoom = targetZoom;
            _revealFrameTargetPan = targetPan;
            if (_revealFrameActiveDuration == 0f)
            {
                _zoom = targetZoom;
                _pan = targetPan;
                CancelRevealFrame();
                Refresh();
            }
        }

        private void AddRevealFrameTarget(int id)
        {
            if (!(id == 0) && _nodesById.TryGetValue(id, out var node) && node.Visible &&
                _revealFrameTargetIds.Add(id)) _revealFrameTargets.Add(node);
        }

        private Rect GetRevealFrameBounds(float zoom, bool includeLabels = true)
        {
            Rect first = GetRevealNodeBounds(_revealFrameTargets[0], zoom, includeLabels);
            Vector2 minimum = first.min;
            Vector2 maximum = first.max;
            for (int index = 1; index < _revealFrameTargets.Count; index++)
            {
                Rect bounds = GetRevealNodeBounds(_revealFrameTargets[index], zoom, includeLabels);
                minimum = Vector2.Min(minimum, bounds.min);
                maximum = Vector2.Max(maximum, bounds.max);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private Rect GetRevealNodeBounds(SkillTreeNode node, float zoom, bool includeLabel = true)
        {
            Vector2 center = new Vector2(node.Position.x, -node.Position.y) * zoom;
            float radius = Mathf.Max(GetHaloRadius(node), GetBaseRadius(node) * 1.4f * Mathf.Sqrt(2f)) * node.IconScale;
            if (node.MaxLevel > 0) radius = Mathf.Max(radius, GetProgressRingRadius(node) + 5f);
            if (_selectedId == node.Id) radius = Mathf.Max(radius, GetSelectionRadius(node) + 1f);
            Vector2 extent = Vector2.one * (radius * zoom + 2f);
            Vector2 minimum = center - extent;
            Vector2 maximum = center + extent;
            if (includeLabel && _showNodeLabels && _labels.TryGetValue(node.Id, out var label) && label != null)
            {
                float textScale = Mathf.Pow(zoom, 1f - _textZoomCompensation);
                Vector2 labelCenter = center;
                labelCenter.y += node.Kind == SkillTreeNodeKind.Root
                    ? -(GetLabelRadius(node) * zoom + 21f * textScale)
                    : GetLabelRadius(node) * zoom + 18f * textScale;
                Rect rect = label.rectTransform.rect;
                Vector2 labelExtent = new Vector2(Mathf.Max(rect.width, label.preferredWidth),
                    Mathf.Max(rect.height, label.preferredHeight)) * (textScale * 0.5f) + Vector2.one * 2f;
                minimum = Vector2.Min(minimum, labelCenter - labelExtent);
                maximum = Vector2.Max(maximum, labelCenter + labelExtent);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static bool ContainsRevealBounds(Rect viewport, Rect bounds)
        {
            return bounds.xMin >= viewport.xMin && bounds.xMax <= viewport.xMax &&
                bounds.yMin >= viewport.yMin && bounds.yMax <= viewport.yMax;
        }

        // Returning true also skips reveal time on the finishing frame, so a
        // long frame cannot consume the appearance before it has been rendered.
        private bool AdvanceRevealFrame(float unscaledDelta)
        {
            if (!_revealFrameActive) return false;
            if (!float.IsFinite(unscaledDelta) || unscaledDelta <= 0f) return true;
            _revealFrameElapsed = Mathf.Min(_revealFrameActiveDuration, _revealFrameElapsed + unscaledDelta);
            float progress = Mathf.SmoothStep(0f, 1f, _revealFrameElapsed / _revealFrameActiveDuration);
            Vector2 viewportCenter = rectTransform.rect.center;
            Vector2 targetCenter = (viewportCenter - _revealFrameTargetPan) / _revealFrameTargetZoom;
            _zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(_revealFrameStartZoom), Mathf.Log(_revealFrameTargetZoom), progress));
            _pan = viewportCenter - Vector2.Lerp(_revealFrameStartCenter, targetCenter, progress) * _zoom;
            if (_revealFrameElapsed >= _revealFrameActiveDuration)
            {
                _zoom = _revealFrameTargetZoom;
                _pan = _revealFrameTargetPan;
                CancelRevealFrame();
            }
            Refresh();
            return true;
        }

        private bool CancelRevealFrame()
        {
            bool active = _revealFrameActive;
            _revealFrameActive = false;
            _revealFrameSources.Clear();
            _revealFrameTargets.Clear();
            _revealFrameTargetIds.Clear();
            _newRevealIds.Clear();
            _newRevealSources.Clear();
            foreach (var reveal in _reveals.Values) reveal.WaitingForFrame = false;
            return active;
        }
    }
}
