using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private readonly List<PurchaseEffect> _purchaseEffects = new();
        private readonly List<SkillTreeGeometryPage> _purchaseEffectPages = new();
        private float _purchaseEffectDuration = 0.85f;
        private bool _purchaseEffectsDirty;

        public float PurchaseEffectDuration
        {
            get => _purchaseEffectDuration;
            set
            {
                if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
                _purchaseEffectDuration = Mathf.Max(0f, value);
                if (_purchaseEffectDuration == 0f) ClearPurchaseEffects();
            }
        }

        public void PlayPurchaseEffect(int id)
        {
            if (!isActiveAndEnabled || _purchaseEffectDuration <= 0f || (id == 0) ||
                !_nodesById.TryGetValue(id, out var node) || !IsShown(node))
            {
                return;
            }
            var effect = new PurchaseEffect
            {
                NodeId = id,
                Duration = _purchaseEffectDuration,
                Seed = RevealSeed(id) + node.Level * 0.173f
            };
            for (int index = 0; index < _purchaseEffects.Count; index++)
            {
                if (_purchaseEffects[index].NodeId != id) continue;
                _purchaseEffects[index] = effect;
                _purchaseEffectsDirty = true;
                return;
            }
            _purchaseEffects.Add(effect);
            _purchaseEffectsDirty = true;
        }

        private void AdvancePurchaseEffects(float unscaledDelta)
        {
            if (_purchaseEffects.Count == 0 || !float.IsFinite(unscaledDelta) || unscaledDelta <= 0f) return;
            for (int index = _purchaseEffects.Count - 1; index >= 0; index--)
            {
                var effect = _purchaseEffects[index];
                effect.Elapsed += unscaledDelta;
                if (effect.Elapsed >= effect.Duration || !_nodesById.TryGetValue(effect.NodeId, out var node) || !node.Visible)
                    _purchaseEffects.RemoveAt(index);
                else
                    _purchaseEffects[index] = effect;
            }
            _purchaseEffectsDirty = true;
        }

        private void PreparePurchaseEffects()
        {
            _purchaseEffectsDirty = false;
            int usedPages = 0;
            SkillTreeGeometryPage page = null;
            foreach (var effect in _purchaseEffects)
            {
                if (!_nodesById.TryGetValue(effect.NodeId, out var node) || !IsShown(node)) continue;
                Vector2 center = ToCanvasPoint(node.Position);
                float radius = GetBaseRadius(node) * node.IconScale;
                float padding = GetRevealViewportPadding(SkillTreeRevealEffects.PurchaseRadius(radius));
                if (!IntersectsViewport(center, Vector2.one * padding)) continue;

                if (page == null || page.Buffer.currentVertCount + SkillTreeRevealEffects.PurchaseVertexBudget > GeometryBudget)
                {
                    page?.Commit();
                    if (usedPages == _purchaseEffectPages.Count)
                    {
                        var child = new GameObject("Purchase Effects", typeof(RectTransform), typeof(SkillTreeGeometryPage));
                        child.hideFlags = HideFlags.DontSave;
                        child.transform.SetParent(transform, false);
                        var created = child.GetComponent<SkillTreeGeometryPage>();
                        created.raycastTarget = false;
                        _purchaseEffectPages.Add(created);
                    }
                    page = _purchaseEffectPages[usedPages++];
                    var rect = page.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = rectTransform.pivot;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    page.material = material;
                    page.maskable = maskable;
                    page.gameObject.SetActive(true);
                    page.transform.SetAsLastSibling();
                    page.Begin(SkillTreeGeometryPage.LayerKind.Overlay, 1f);
                }
                _revealEffects.DrawPurchase(page.Buffer, center, effect.Elapsed / effect.Duration, _zoom, radius,
                    node.Color, _pixels, effect.Seed);
            }
            page?.Commit();
            for (int index = usedPages; index < _purchaseEffectPages.Count; index++)
                _purchaseEffectPages[index].gameObject.SetActive(false);
        }

        private void ClearPurchaseEffects()
        {
            _purchaseEffects.Clear();
            _purchaseEffectsDirty = false;
            foreach (var page in _purchaseEffectPages)
                if (page != null) page.gameObject.SetActive(false);
        }

        private struct PurchaseEffect
        {
            public int NodeId;
            public float Elapsed;
            public float Duration;
            public float Seed;
        }
    }
}
