using System;
using System.Collections.Generic;
using Game.SkillTree.Data;

namespace Game.SkillTree.Samples
{
    /// <summary>Optional Editor conveniences for the sample; not needed by game integration.</summary>
    internal sealed class SkillTreeDemoAuthoring : IDisposable
    {
        private readonly SkillTreeConfig _config;
        private readonly SkillTreePresenter _presenter;
        private readonly SkillTreeWindow _window;
        private readonly Dictionary<int, SkillTreeNodeLayout> _openingLayout = new();
        private bool _dirty;
        private bool _wasPreviewing;

        public SkillTreeDemoAuthoring(SkillTreeConfig config, SkillTreePresenter presenter, SkillTreeWindow window)
        {
            _config = config;
            _presenter = presenter;
            _window = window;
            foreach (var layout in config.Layout.Nodes) _openingLayout.Add(layout.NodeId, layout.Properties);
            _window.SetHistoryActions(Undo, Redo);
            _window.ResetLayoutRequested += ResetLayout;
            _window.DataRequested += OpenNodeData;
            _window.PropertiesRequested += OpenProperties;
            _window.ArrangementRequested += OpenArrangement;
            _window.EditingClosed += CloseEditing;
            _window.LayoutChanged += SaveLayout;
            _window.EditRejected += ShowFeedback;
            SkillTreeConfigAsset.Changed += OnAssetChanged;
            _window.SetFeedback("Edit enables graph authoring. Properties and Arrange open the Inspector. Edits support Undo.");
        }

        public void Tick()
        {
            bool previewing = SkillTreeAssetEditing.IsPreviewing(_window.Graph);
            if (_dirty)
            {
                _dirty = false;
                if (previewing) CloseEditing();
                _presenter.Refresh();
                _wasPreviewing = false;
            }
            else if (previewing != _wasPreviewing)
            {
                // Preserve the preview's positions while disabling purchase interactions.
                _window.UpdateContent(Array.Empty<SkillTreeNode>(), _presenter.Wallet, !previewing);
                _wasPreviewing = previewing;
            }
        }

        private void OnAssetChanged(SkillTreeConfigAsset asset)
        {
            // Events replace per-frame scanning of every node in the large sample.
            // Refresh is coalesced until the next frame, after serialized edits finish.
            _dirty = true;
        }

        private void SaveLayout()
        {
            SkillTreeAssetEditing.SaveLayout(_config.Layout, _window.Graph.Nodes);
        }

        private void ResetLayout()
        {
            CloseEditing();
            foreach (var node in _window.Graph.Nodes)
                if (_openingLayout.TryGetValue(node.Id, out var layout)) layout.ApplyTo(node);
            _window.Graph.CommitLayoutChanges();
            _window.Graph.Fit();
            _window.SetFeedback("Restored this session's opening layout. Skill progress is unchanged.");
        }

        private void OpenNodeData()
        {
            var node = _config.FindNode(_window.Graph.SelectedId);
            if (node == null) return;
            CloseEditing();
            SkillTreeAssetEditing.OpenNode(node);
        }

        private void OpenProperties(int id) => SkillTreeAssetEditing.OpenProperties(_config, id, _window.Graph);
        private void OpenArrangement() => SkillTreeAssetEditing.OpenArrangement(_config, _window.Graph);
        private void CloseEditing() => SkillTreeAssetEditing.CloseContext(_window.Graph);
        private static void Undo() => SkillTreeAssetEditing.UndoLastEdit();
        private static void Redo() => SkillTreeAssetEditing.RedoLastEdit();
        private void ShowFeedback(string message) => _window.SetFeedback(message);

        public void Dispose()
        {
            SkillTreeConfigAsset.Changed -= OnAssetChanged;
            _window.ResetLayoutRequested -= ResetLayout;
            _window.DataRequested -= OpenNodeData;
            _window.PropertiesRequested -= OpenProperties;
            _window.ArrangementRequested -= OpenArrangement;
            _window.EditingClosed -= CloseEditing;
            _window.LayoutChanged -= SaveLayout;
            _window.EditRejected -= ShowFeedback;
            CloseEditing();
        }
    }
}
