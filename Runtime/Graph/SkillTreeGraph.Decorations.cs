using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private readonly Dictionary<int, SkillTreeDecorationView> _nodeDecorations = new();
        private readonly List<int> _retiredDecorations = new();
        private SkillTreeNodeDecorationRenderer _decorationRenderer;
        private int _decorationRevision;

        public SkillTreeNodeDecorationRenderer DecorationRenderer
        {
            get => _decorationRenderer;
            set
            {
                if (_decorationRenderer == value) return;
                _decorationRenderer = value;
                SetNodeVisualsDirty();
            }
        }

        private void RefreshDecorationRenderer()
        {
            int revision = _decorationRenderer != null ? _decorationRenderer.Revision : 0;
            if (_decorationRevision == revision) return;
            _decorationRevision = revision;
            SetNodeVisualsDirty();
        }

        private void RefreshNodeDecorations()
        {
            _retiredDecorations.Clear();
            foreach (var pair in _nodeDecorations)
                if (_decorationRenderer == null || !_nodesById.ContainsKey(pair.Key)) _retiredDecorations.Add(pair.Key);
            foreach (int id in _retiredDecorations)
            {
                _nodeDecorations[id].Dispose();
                _nodeDecorations.Remove(id);
            }
            if (_decorationRenderer == null) return;

            foreach (var node in _nodes)
            {
                bool shown = IsShown(node) && IsNodeInViewport(node);
                if (!_nodeDecorations.TryGetValue(node.Id, out var view))
                {
                    if (!shown) continue;
                    view = new SkillTreeDecorationView(transform);
                    _nodeDecorations.Add(node.Id, view);
                }
                if (!shown)
                {
                    view.SetActive(false);
                    continue;
                }
                float reveal = RevealOpacity(node);
                var context = new SkillTreeNodeDecorationContext(GetProgressRingRadius(node), GetSelectionRadius(node),
                    GetOpacity(node) * reveal, reveal, _selectedId == node.Id);
                view.SetNode(node, _decorationRenderer, context);
                view.SetActive(true);
                var rect = view.Graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = rectTransform.pivot;
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = ToCanvasPoint(node.Position);
                rect.sizeDelta = Vector2.one * (context.SelectionRadius * 2f);
                rect.localScale = Vector3.one * _zoom;
            }
        }

        private void OrderNodeDecorations(ref int sibling)
        {
            foreach (var node in _nodes)
            {
                if (!_nodeDecorations.TryGetValue(node.Id, out var view) || view.Graphic == null) continue;
                if (view.Graphic.transform.GetSiblingIndex() != sibling)
                    view.Graphic.transform.SetSiblingIndex(sibling);
                sibling++;
            }
        }
    }
}
