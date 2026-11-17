using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    /// <summary>Prefab contract around the package graph. Games own the surrounding UI.</summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class SkillTreeView : MonoBehaviour
    {
        [SerializeField] protected SkillTreeGraph _graph;

        public SkillTreeGraph Graph => _graph;
        public event Action<int> PurchaseRequested;
        public event Action CloseRequested;

        public abstract void SetContent(IEnumerable<SkillTreeNode> nodes, int wallet, bool allowPurchase = true);
        public abstract void UpdateContent(IEnumerable<SkillTreeNode> changes, int wallet, bool allowPurchase = true);
        public abstract void SetFeedback(string message);

        public virtual void Open(int selectedId = 0, bool fitView = false)
        {
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (fitView) _graph.Fit();
            if (selectedId > 0) _graph.Select(selectedId);
            _graph.FocusInput();
        }

        public virtual void Close() => gameObject.SetActive(false);

        public virtual bool Validate(out string error)
        {
            error = _graph == null ? $"Skill tree view '{name}' has no Graph reference." : null;
            return error == null;
        }

        protected void RequestPurchase(int nodeId) => PurchaseRequested?.Invoke(nodeId);
        protected void RequestClose() => CloseRequested?.Invoke();
    }
}
