using System;
using System.Collections.Generic;
using Game.SkillTree.Requirements;
using UnityEngine;

namespace Game.SkillTree.Data
{
    /// <summary>
    /// Connects authored configs and game-owned state to a prefab view. Call Refresh after game
    /// data changes; the presenter computes snapshots and only submits changed nodes to the graph.
    /// It never spends currency, changes progress, applies effects, or saves the game.
    /// </summary>
    public sealed class SkillTreePresenter : IDisposable
    {
        private readonly ISkillLevelReader _levels;
        private readonly Func<int> _getWallet;
        private readonly SkillRequirementEvaluator _requirements;
        private readonly Dictionary<int, SkillTreeNode> _snapshots = new();
        private readonly Dictionary<int, SkillNodeLayoutConfig> _appearance = new();
        private readonly Dictionary<int, int> _infoIndices = new();
        private readonly List<SkillTreeNodeInfo> _nodeInfo = new();
        private readonly List<SkillTreeNode> _incoming = new();
        private readonly List<SkillTreeNode> _changes = new();
        private readonly List<int> _order = new();
        private SkillTreeLayoutConfig _observedLayout;
        private int _layoutRevision;
        private string _lastError;
        private bool _ownsView;
        private bool _disposed;
        private bool _initialized;

        public SkillTreeConfig Config { get; }
        public SkillTreeView View { get; }
        public IReadOnlyList<SkillTreeNodeInfo> Nodes { get; }
        public int Wallet { get; private set; }

        /// <summary>A request only. The game must recheck, perform the purchase, then Refresh.</summary>
        public event Action<int> PurchaseRequested;

        /// <summary>Raised after Refresh so custom UI can read Nodes and Wallet.</summary>
        public event Action Refreshed;

        public SkillTreePresenter(SkillTreeConfig config, SkillTreeView view,
            ISkillLevelReader levels, object requirementContext, Func<int> getWallet)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            View = view != null ? view : throw new ArgumentNullException(nameof(view));
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
            _getWallet = getWallet ?? throw new ArgumentNullException(nameof(getWallet));
            if (!view.Validate(out string error))
            {
                Debug.LogError(error, view);
                throw new ArgumentException(error, nameof(view));
            }
            Nodes = _nodeInfo.AsReadOnly();
            _requirements = new SkillRequirementEvaluator(config, requirementContext);
            View.PurchaseRequested += ForwardPurchase;
            View.CloseRequested += Close;
            try
            {
                Refresh();
            }
            catch
            {
                View.PurchaseRequested -= ForwardPurchase;
                View.CloseRequested -= Close;
                throw;
            }
        }

        public static SkillTreePresenter Create(SkillTreeConfig config, RectTransform parent,
            ISkillLevelReader levels, object requirementContext, Func<int> getWallet)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (config.Layout == null || config.Layout.ViewPrefab == null)
            {
                string error = $"Skill tree '{config.name}' needs a view prefab in its layout config.";
                Debug.LogError(error, config);
                throw new InvalidOperationException(error);
            }
            var view = UnityEngine.Object.Instantiate(config.Layout.ViewPrefab, parent, false);
            try
            {
                return new SkillTreePresenter(config, view, levels, requirementContext, getWallet)
                {
                    _ownsView = true
                };
            }
            catch
            {
                DestroyView(view);
                throw;
            }
        }

        public SkillTreeNodeInfo GetNodeInfo(int id) => _infoIndices.TryGetValue(id, out int index)
            ? _nodeInfo[index] : throw new KeyNotFoundException($"No node with ID {id} in '{Config.name}'.");

        public bool TryGetNodeInfo(int id, out SkillTreeNodeInfo info)
        {
            if (_infoIndices.TryGetValue(id, out int index))
            {
                info = _nodeInfo[index];
                return true;
            }
            info = default;
            return false;
        }

        public bool Refresh(bool allowPurchase = true)
        {
            ThrowIfDisposed();
            Wallet = _getWallet();
            if (Wallet < 0) throw new InvalidOperationException("The skill tree wallet reader returned a negative balance.");
            if (!Config.Validate(out string error)) return ReportError(error);
            if (!_requirements.Validate(out error)) return ReportError(error, false);

            var layout = Config.Layout;
            if (_observedLayout != layout || _layoutRevision != layout.Revision)
            {
                ApplyAppearance(View.Graph, layout);
                _observedLayout = layout;
                _layoutRevision = layout.Revision;
            }
            _appearance.Clear();
            foreach (var appearance in layout.Nodes) _appearance.Add(appearance.NodeId, appearance);

            bool structureChanged = !_initialized || _order.Count != Config.Nodes.Count;
            _incoming.Clear();
            _changes.Clear();
            _nodeInfo.Clear();
            _infoIndices.Clear();
            int index = 0;
            foreach (var definition in Config.Nodes)
            {
                if (!structureChanged && _order[index] != definition.Id) structureChanged = true;
                var info = Project(definition, allowPurchase);
                _infoIndices.Add(definition.Id, _nodeInfo.Count);
                _nodeInfo.Add(info);
                var node = CreateNode(info);
                _appearance[definition.Id].ApplyTo(node);
                _incoming.Add(node);
                if (!_snapshots.TryGetValue(node.Id, out var previous) || !previous.HasSameValues(node))
                    _changes.Add(node);
                index++;
            }

            if (structureChanged)
            {
                // Graph editing may mutate its nodes. Keep the presenter's comparison snapshots private.
                var presented = new SkillTreeNode[_incoming.Count];
                for (int i = 0; i < presented.Length; i++) presented[i] = _incoming[i].Clone();
                View.SetContent(presented, Wallet, allowPurchase);
            }
            else
            {
                var presented = new SkillTreeNode[_changes.Count];
                for (int i = 0; i < presented.Length; i++) presented[i] = _changes[i].Clone();
                View.UpdateContent(presented, Wallet, allowPurchase);
            }
            _snapshots.Clear();
            _order.Clear();
            foreach (var node in _incoming)
            {
                _snapshots.Add(node.Id, node);
                _order.Add(node.Id);
            }
            _initialized = true;
            if (_lastError != null) View.SetFeedback(string.Empty);
            _lastError = null;
            Refreshed?.Invoke();
            return true;
        }

        public void Open(int selectedId = 0, bool fitView = true)
        {
            ThrowIfDisposed();
            View.Open(selectedId, fitView);
        }

        public void Close()
        {
            if (!_disposed && View != null) View.Close();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (View == null) return;
            View.PurchaseRequested -= ForwardPurchase;
            View.CloseRequested -= Close;
            SkillTreeAssetEditing.CloseContext(View.Graph);
            if (_ownsView) DestroyView(View);
        }

        private SkillTreeNodeInfo Project(SkillNodeConfig definition, bool allowPurchase)
        {
            int level = _levels.GetLevel(definition);
            if (level < 0 || level > definition.MaxLevel)
                throw new InvalidOperationException($"Progress for skill '{definition.name}' must be between 0 and {definition.MaxLevel}.");
            bool ranked = definition.IsRanked;
            bool maxed = level >= definition.MaxLevel;
            bool available = !ranked || maxed || _requirements.MeetsRequirements(definition, level);
            bool visible = _requirements.IsVisible(definition, level);
            int cost = ranked && !maxed ? definition.Levels[level].Cost : 0;
            bool canPurchase = allowPurchase && ranked && !maxed && available && visible && Wallet >= cost;
            var state = !ranked || level > 0 ? SkillTreeNodeState.Unlocked
                : available ? SkillTreeNodeState.Available : SkillTreeNodeState.Locked;
            string requirement = maxed ? (ranked ? "Fully learned." : string.Empty)
                : definition.Levels[level].GetRequirementText("level");
            return new SkillTreeNodeInfo(definition, level, cost, visible, canPurchase, state,
                definition.GetDescriptionForProgress(level), requirement);
        }

        private static SkillTreeNode CreateNode(SkillTreeNodeInfo info)
        {
            var definition = info.Definition;
            return new SkillTreeNode(info.Id)
            {
                Label = definition.IsRanked ? $"{definition.Label} · {info.Level}/{info.MaxLevel}" : definition.Label,
                Title = info.Title,
                Description = info.Description,
                Category = definition.Category,
                Requirement = info.Requirement,
                Kind = definition.Kind,
                State = info.State,
                Level = info.Level,
                MaxLevel = info.MaxLevel,
                Cost = info.Cost,
                CanPurchase = info.CanPurchase,
                Visible = info.Visible
            };
        }

        private static void ApplyAppearance(SkillTreeGraph graph, SkillTreeLayoutConfig layout)
        {
            graph.ShowNodeLabels = layout.ShowNodeLabels;
            graph.FitPadding = layout.FitPadding;
            graph.DecorationRenderer = layout.DecorationRenderer;
            graph.CurveTolerancePixels = layout.CurveTolerancePixels;
            graph.RevealFrameDuration = layout.RevealFrameDuration;
            graph.RevealTravelCurve = layout.RevealTravelCurve;
            graph.PurchaseEffectDuration = layout.PurchaseEffectDuration;
            graph.SetPurchaseGlow(layout.PurchaseGlowPeriod, layout.PurchaseGlowMinOpacity, layout.PurchaseGlowMaxOpacity);
            graph.SetNodeIconRenderers(layout.IconRenderers);
        }

        private bool ReportError(string error, bool log = true)
        {
            if (log && _lastError != error) Debug.LogError(error, Config);
            _lastError = error;
            View.UpdateContent(Array.Empty<SkillTreeNode>(), Wallet, false);
            View.SetFeedback(error);
            return false;
        }

        private void ForwardPurchase(int id) => PurchaseRequested?.Invoke(id);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SkillTreePresenter));
        }

        private static void DestroyView(SkillTreeView view)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(view.gameObject);
            else UnityEngine.Object.DestroyImmediate(view.gameObject);
        }
    }
}
