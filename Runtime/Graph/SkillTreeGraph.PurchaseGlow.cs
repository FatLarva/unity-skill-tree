using System;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private bool _allowPurchase = true;
        private float _purchaseGlowPeriod = 2.4f;
        private float _purchaseGlowMinOpacity = 0.18f;
        private float _purchaseGlowMaxOpacity = 0.45f;
        private float _purchaseGlowOpacity = 0.18f;

        public bool AllowPurchase
        {
            get => _allowPurchase;
            set
            {
                if (_allowPurchase == value) return;
                _allowPurchase = value;
                SetNodeVisualsDirty();
            }
        }

        public void SetPurchaseGlow(float period, float minOpacity, float maxOpacity)
        {
            if (!float.IsFinite(period)) throw new ArgumentOutOfRangeException(nameof(period));
            if (!float.IsFinite(minOpacity)) throw new ArgumentOutOfRangeException(nameof(minOpacity));
            if (!float.IsFinite(maxOpacity)) throw new ArgumentOutOfRangeException(nameof(maxOpacity));
            _purchaseGlowPeriod = Mathf.Max(0.1f, period);
            _purchaseGlowMinOpacity = Mathf.Clamp01(minOpacity);
            _purchaseGlowMaxOpacity = Mathf.Clamp(maxOpacity, _purchaseGlowMinOpacity, 1f);
            UpdatePurchaseGlow(Time.unscaledTimeAsDouble);
        }

        private bool HasPurchaseGlow(SkillTreeNode node) => !Editable && _allowPurchase && node.CanPurchase;

        private void DrawPurchaseGlows()
        {
            foreach (var node in _nodes)
            {
                if (!HasPurchaseGlow(node) || !IsShown(node) || !IsNodeInViewport(node)) continue;
                float reveal = RevealOpacity(node);
                var context = new SkillTreeNodeIconContext(ToCanvasPoint(node.Position), GetBaseRadius(node) * _zoom,
                    _zoom, GetOpacity(node) * reveal, reveal, _pixels);
                DrawNodeHalo(GeometryFor(42, SkillTreeGeometryPage.LayerKind.PurchaseGlow), node, context, 1f);
            }
        }

        private void UpdatePurchaseGlow(double unscaledTime)
        {
            float phase = (float)(unscaledTime % _purchaseGlowPeriod / _purchaseGlowPeriod);
            float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            _purchaseGlowOpacity = Mathf.Lerp(_purchaseGlowMinOpacity, _purchaseGlowMaxOpacity, wave);
            // Only the renderer tint changes. Meshes, icons and labels stay cached.
            for (int index = 0; index < _usedPages; index++)
            {
                var page = _geometryPages[index];
                if (page.Layer == SkillTreeGeometryPage.LayerKind.PurchaseGlow)
                    page.canvasRenderer.SetAlpha(_purchaseGlowOpacity);
            }
        }
    }
}
