using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private readonly List<SkillTreeNodeIconRenderer> _availableIconRenderers = new();
        private readonly Dictionary<int, SkillTreeIconView> _nodeIcons = new();
        private readonly List<int> _retiredIcons = new();
        private SkillTreeNodeIconRenderer _fallbackIconRenderer;

        public IReadOnlyList<SkillTreeNodeIconRenderer> NodeIconRenderers => _availableIconRenderers;

        public SkillTreeNodeIconRenderer GetNodeIconRenderer(SkillTreeNode node)
        {
            if (node?.IconRenderer != null) return node.IconRenderer;
            foreach (var renderer in _availableIconRenderers)
                if (renderer != null) return renderer;
            RebuildIconRenderers();
            return _fallbackIconRenderer;
        }

        public void SetNodeIconRenderers(IEnumerable<SkillTreeNodeIconRenderer> renderers)
        {
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));
            var incoming = new List<SkillTreeNodeIconRenderer>(renderers);
            bool changed = incoming.Count != _availableIconRenderers.Count;
            for (int index = 0; index < incoming.Count && !changed; index++)
                changed = incoming[index] != _availableIconRenderers[index];
            if (!changed) return;
            _availableIconRenderers.Clear();
            _availableIconRenderers.AddRange(incoming);
            SetNodeVisualsDirty();
        }

        private void RebuildIconRenderers()
        {
            if (_fallbackIconRenderer != null) return;
            _fallbackIconRenderer = ScriptableObject.CreateInstance<SkillTreeNodeIconRenderer>();
            _fallbackIconRenderer.hideFlags = HideFlags.HideAndDontSave;
        }

        private void RefreshNodeIcons()
        {
            _retiredIcons.Clear();
            foreach (var pair in _nodeIcons)
                if (!_nodesById.ContainsKey(pair.Key)) _retiredIcons.Add(pair.Key);
            foreach (int id in _retiredIcons)
            {
                _nodeIcons[id].Dispose();
                _nodeIcons.Remove(id);
            }
            foreach (var node in _nodes)
            {
                if (!_nodeIcons.TryGetValue(node.Id, out var view))
                {
                    view = new SkillTreeIconView(transform);
                    _nodeIcons.Add(node.Id, view);
                }
                if (!IsShown(node) || !IsNodeInViewport(node))
                {
                    view.SetActive(false);
                    continue;
                }
                float reveal = RevealOpacity(node);
                view.SetNode(node, GetNodeIconRenderer(node), GetOpacity(node) * reveal, reveal);
                view.SetActive(true);
                var rect = view.Graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = rectTransform.pivot;
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = ToCanvasPoint(node.Position);
                rect.sizeDelta = Vector2.one * (GetBaseRadius(node) * 2.8f * _zoom);
                rect.localScale = Vector3.one * node.IconScale;
                rect.localRotation = Quaternion.Euler(0f, 0f, node.IconRotation);
            }
        }

        private void OrderNodeIcons(ref int sibling)
        {
            foreach (var node in _nodes)
            {
                if (!_nodeIcons.TryGetValue(node.Id, out var view) || view.Graphic == null) continue;
                if (view.Graphic.transform.GetSiblingIndex() != sibling)
                    view.Graphic.transform.SetSiblingIndex(sibling);
                sibling++;
            }
        }

        protected override void OnDestroy()
        {
            foreach (var view in _nodeDecorations.Values) view.Dispose();
            _nodeDecorations.Clear();
            foreach (var view in _nodeIcons.Values) view.Dispose();
            _nodeIcons.Clear();
            if (_fallbackIconRenderer != null)
            {
                if (Application.isPlaying) Destroy(_fallbackIconRenderer);
                else DestroyImmediate(_fallbackIconRenderer);
            }
            base.OnDestroy();
        }
    }
}
