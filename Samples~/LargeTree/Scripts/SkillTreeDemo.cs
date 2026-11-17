using System;
using Game.SkillTree.Data;
using UnityEngine;

namespace Game.SkillTree.LargeSample
{
    /// <summary>The game integration: instantiate the configured prefab and handle purchase requests.</summary>
    public sealed class SkillTreeDemo : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private SkillTreeConfig _config;
        [SerializeField] private string _title = "Large Skill Tree - 600 nodes";
        [SerializeField] private bool _overrideInitialPoints;
        [SerializeField, Min(0)] private int _initialPoints = 24;

        private SkillTreeDemoProgression _progression;
        private SkillTreePresenter _presenter;
        private SkillTreeDemoAuthoring _authoring;

        public SkillTreeConfig Config => _config;
        public SkillTreeView View => _presenter?.View;
        public SkillTreeDemoProgression Progression => _progression;
        public int Points => _progression?.Wallet ?? 0;

        private void Start()
        {
            if (_container == null || _config == null)
            {
                Debug.LogError("Assign the sample container and skill tree config.", this);
                enabled = false;
                return;
            }
            if (!_config.Validate(out string error))
            {
                Debug.LogError(error, _config);
                enabled = false;
                return;
            }
            try
            {
                _progression = new SkillTreeDemoProgression(_config, _overrideInitialPoints ? _initialPoints : null);
                _presenter = SkillTreePresenter.Create(_config, _container, _progression, _progression,
                    () => _progression.Wallet);
                _presenter.PurchaseRequested += Purchase;
                if (_presenter.View is SkillTreeWindow window)
                {
                    window.SetHeading(_title, "Select a skill to inspect its requirements and purchase a level.");
                    window.SetEditingAllowed(SkillTreeAssetEditing.Available);
                    if (SkillTreeAssetEditing.Available)
                        _authoring = new SkillTreeDemoAuthoring(_config, _presenter, window);
                }
                _presenter.Open();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update() => _authoring?.Tick();

        private void Purchase(int id)
        {
            if (SkillTreeAssetEditing.IsPreviewing(View.Graph)) return;
            bool purchased = _progression.TryPurchase(id, out string message);
            // Refresh projects progress, requirements and wallet changes. The presenter submits
            // only changed snapshots; callers do not build graph nodes or clone them manually.
            if (purchased) _presenter.Refresh();
            View.SetFeedback(message);
        }

        [ContextMenu("Open Skill Tree")]
        public void Open()
        {
            if (_presenter == null) return;
            _presenter.Refresh();
            _presenter.Open();
        }

        public void Close() => _presenter?.Close();

        public void ChangePoints(int amount)
        {
            if (_presenter == null || SkillTreeAssetEditing.IsPreviewing(View.Graph)) return;
            if (_progression.ChangePoints(amount)) _presenter.Refresh();
        }

        [ContextMenu("Add 100 Test Points")]
        private void AddTestPoints() => ChangePoints(100);

        [ContextMenu("Reset Test Progress")]
        public void ResetProgress()
        {
            if (_presenter == null) return;
            SkillTreeAssetEditing.CloseContext(View.Graph);
            _progression.Reset();
            _presenter.Refresh();
            View.SetFeedback("Test progress and applied sample effects reset.");
        }

        private void OnDestroy()
        {
            _authoring?.Dispose();
            if (_presenter == null) return;
            _presenter.PurchaseRequested -= Purchase;
            _presenter.Dispose();
        }
    }
}

