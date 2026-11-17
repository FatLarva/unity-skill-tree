using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Layout", fileName = "SkillTreeLayout")]
    public sealed class SkillTreeLayoutConfig : SkillTreeConfigAsset
    {
        [SerializeField] private SkillTreeView _viewPrefab;
        public SkillTreeView ViewPrefab => _viewPrefab;

        public void ConfigureViewPrefab(SkillTreeView prefab)
        {
            _viewPrefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            NotifyChanged();
        }

        [SerializeField]
        private bool _showNodeLabels = true;

        [SerializeField, Min(0f)]
        [Tooltip("Empty space on each side, in Canvas units, when opening or fitting the tree. Smaller values zoom in further.")]
        private float _fitPadding = 32f;

        [SerializeField, Range(0.25f, 8f)]
        [InspectorName("Curve Tolerance (Pixels)")]
        [Tooltip("Adaptive Bezier flatness threshold in screen pixels. Larger values use fewer triangles. " +
            "Curves are capped at 128 segments, so this is not a strict error guarantee.")]
        private float _curveTolerancePixels = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Unscaled seconds to frame newly visible nodes before their reveal. 0 frames them instantly.")]
        private float _revealFrameDuration = 0.35f;

        [SerializeField]
        [Tooltip("Reveal spark movement: X is normalized travel time (0–1), Y is distance along the connection (0–1).")]
        private AnimationCurve _revealTravelCurve = new(
            new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        [SerializeField]
        private List<SkillNodeLayoutConfig> _nodes = new();

        [SerializeField]
        private List<SkillTreeNodeIconRenderer> _iconRenderers = new();

        [SerializeField, Tooltip("Factory for node progress rings and selection Graphics.")]
        private SkillTreeNodeDecorationRenderer _decorationRenderer;

        [Header("Purchase Glow")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Length of one pulse in seconds.")]
        private float _purchaseGlowPeriod = 2.4f;

        [SerializeField, Range(0f, 1f)]
        private float _purchaseGlowMinOpacity = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float _purchaseGlowMaxOpacity = 0.45f;

        [Header("Purchase Effect")]
        [SerializeField, Min(0f)]
        [Tooltip("Unscaled seconds for the spark burst and expanding halo after buying a skill level. 0 disables the effect.")]
        private float _purchaseEffectDuration = 0.85f;

        public bool ShowNodeLabels => _showNodeLabels;
        public float FitPadding => _fitPadding;
        public float CurveTolerancePixels => _curveTolerancePixels;
        public float RevealFrameDuration => _revealFrameDuration;
        public AnimationCurve RevealTravelCurve => _revealTravelCurve;
        public IReadOnlyList<SkillNodeLayoutConfig> Nodes => _nodes;
        public IReadOnlyList<SkillTreeNodeIconRenderer> IconRenderers => _iconRenderers;
        public SkillTreeNodeDecorationRenderer DecorationRenderer => _decorationRenderer;
        public float PurchaseGlowPeriod => _purchaseGlowPeriod;
        public float PurchaseGlowMinOpacity => _purchaseGlowMinOpacity;
        public float PurchaseGlowMaxOpacity => _purchaseGlowMaxOpacity;
        public float PurchaseEffectDuration => _purchaseEffectDuration;

        public void Configure(IEnumerable<SkillNodeLayoutConfig> nodes, IEnumerable<SkillTreeNodeIconRenderer> iconRenderers = null)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            var incoming = new List<SkillNodeLayoutConfig>();
            var identifiers = new HashSet<int>();
            foreach (var node in nodes)
            {
                if (node == null || !identifiers.Add(node.NodeId))
                {
                    throw new ArgumentException("A layout needs separate configs with unique node identifiers.", nameof(nodes));
                }
                if (!node.Validate(out string error))
                {
                    throw new ArgumentException(error, nameof(nodes));
                }
                incoming.Add(node);
            }

            _nodes = incoming;
            if (iconRenderers != null)
            {
                var unique = new HashSet<SkillTreeNodeIconRenderer>();
                var renderers = new List<SkillTreeNodeIconRenderer>();
                foreach (var renderer in iconRenderers)
                {
                    if (renderer != null && unique.Add(renderer)) renderers.Add(renderer);
                }
                _iconRenderers = renderers;
            }
            NotifyChanged();
        }

        public SkillNodeLayoutConfig Find(int nodeId)
        {
            foreach (var node in _nodes)
            {
                if (node != null && node.NodeId == nodeId)
                {
                    return node;
                }
            }
            return null;
        }

        public bool ApplyTo(SkillTreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            var config = Find(node.Id);
            if (config == null)
            {
                return false;
            }
            config.ApplyTo(node);
            return true;
        }

        public bool Matches(IReadOnlyList<SkillTreeNode> nodes)
        {
            if (nodes == null)
            {
                return false;
            }
            foreach (var node in nodes)
            {
                if (node == null || Find(node.Id) is not { } config || !config.Matches(node))
                {
                    return false;
                }
            }
            return true;
        }

        public void Capture(IReadOnlyList<SkillTreeNode> nodes)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            var identifiers = new HashSet<int>();
            var targets = new SkillNodeLayoutConfig[nodes.Count];
            for (int index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null || !identifiers.Add(node.Id) || Find(node.Id) is not { } config)
                {
                    throw new ArgumentException("Every changed node needs an existing, unique layout config.", nameof(nodes));
                }
                if (!SkillTreeConfigValidation.ValidateNodeLayout(new SkillTreeNodeLayout(node), out string error))
                {
                    throw new ArgumentException(error, nameof(nodes));
                }
                targets[index] = config;
            }

            bool changed = false;
            for (int index = 0; index < nodes.Count; index++)
            {
                if (targets[index].Matches(nodes[index]))
                {
                    continue;
                }
                targets[index].Capture(nodes[index]);
                changed = true;
            }
            if (changed)
            {
                NotifyChanged();
            }
        }

        public void CopyPropertiesTo(List<SkillTreeNodeLayout> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            destination.Clear();
            foreach (var node in _nodes)
            {
                if (node != null)
                {
                    destination.Add(node.Properties);
                }
            }
        }

        protected override void OnValidate()
        {
            _fitPadding = float.IsFinite(_fitPadding) ? Mathf.Max(0f, _fitPadding) : 32f;
            _curveTolerancePixels = float.IsFinite(_curveTolerancePixels)
                ? Mathf.Clamp(_curveTolerancePixels, 0.25f, 8f)
                : 0.25f;
            _revealFrameDuration = float.IsFinite(_revealFrameDuration)
                ? Mathf.Max(0f, _revealFrameDuration)
                : 0.35f;
            _purchaseGlowPeriod = float.IsFinite(_purchaseGlowPeriod) ? Mathf.Max(0.1f, _purchaseGlowPeriod) : 2.4f;
            _purchaseGlowMinOpacity = float.IsFinite(_purchaseGlowMinOpacity)
                ? Mathf.Clamp01(_purchaseGlowMinOpacity)
                : 0.18f;
            _purchaseGlowMaxOpacity = Mathf.Clamp(
                float.IsFinite(_purchaseGlowMaxOpacity) ? _purchaseGlowMaxOpacity : 0.45f, _purchaseGlowMinOpacity, 1f);
            _nodes ??= new List<SkillNodeLayoutConfig>();
            _iconRenderers ??= new List<SkillTreeNodeIconRenderer>();
            base.OnValidate();
        }
    }
}
