using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.SkillTree
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SkillTreeWindow : SkillTreeView
    {
        private const float ZoomStep = 1.2f;
        private const string BrowseHint = "Drag to pan. Scroll or pinch to zoom.";
        private const string EditHint = "Shift-drag moves a subtree. Right-click reparents; Alt-right-click toggles another visual parent. Click a connection to edit its handles.";

        private static readonly Color Muted = new(0.58f, 0.66f, 0.57f);
        private static readonly Color Gold = new(0.93f, 0.76f, 0.48f);
        private static readonly Color EmptyRank = new(0.22f, 0.32f, 0.25f);
        private static readonly Color LockedRequirement = new(0.83f, 0.61f, 0.47f);

        [SerializeField] private TextMeshProUGUI _wallet;
        [SerializeField] private UnityEngine.UI.Graphic _walletIcon;
        [SerializeField] private TextMeshProUGUI _heading;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private TextMeshProUGUI _hint;
        [SerializeField] private TextMeshProUGUI _feedback;
        [SerializeField] private TextMeshProUGUI _zoomLabel;
        [SerializeField] private TextMeshProUGUI _category;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _rank;
        [SerializeField] private TextMeshProUGUI _requirement;
        [SerializeField] private TextMeshProUGUI _nextRank;
        [SerializeField] private TextMeshProUGUI _note;
        [SerializeField] private TextMeshProUGUI _purchaseLabel;
        [SerializeField] private TextMeshProUGUI _editLabel;
        [SerializeField] private UnityEngine.UI.Image[] _rankPips;
        [SerializeField] private UnityEngine.UI.Button _purchaseButton;
        [SerializeField] private UnityEngine.UI.Button _editButton;
        [SerializeField] private UnityEngine.UI.Button _resetButton;
        [SerializeField] private UnityEngine.UI.Button _fitButton;
        [SerializeField] private UnityEngine.UI.Button _zoomOutButton;
        [SerializeField] private UnityEngine.UI.Button _zoomInButton;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private UnityEngine.UI.Button _closeBottomButton;
        [SerializeField] private UnityEngine.UI.Button _nodeDataButton;
        [SerializeField] private UnityEngine.UI.Button _propertiesButton;
        [SerializeField] private UnityEngine.UI.Button _arrangeButton;
        [SerializeField] private UnityEngine.UI.Button _addNodeButton;
        [SerializeField] private UnityEngine.UI.Button _undoButton;
        [SerializeField] private UnityEngine.UI.Button _redoButton;
        [SerializeField] private RectTransform _detailsIcon;
        private SkillTreeIconView _detailsIconView;
        [SerializeField] private UnityEngine.UI.ScrollRect _descriptionScroll;

        private bool _subscribed;
        private bool _allowPurchase = true;
        [SerializeField] private bool _allowEditing = true;
        [SerializeField] private string _currencyLabel = "Points";
        private int _walletValue;
        private Action _undo;
        private Action _redo;
        private Action _addNode;

        public event Action ResetLayoutRequested;
        public event Action LayoutChanged;
        public event Action<string> EditRejected;
        public event Action DataRequested;
        public event Action<int> PropertiesRequested;
        public event Action ArrangementRequested;
        public event Action EditingClosed;

        public override void SetContent(IEnumerable<SkillTreeNode> nodes, int treasury, bool allowPurchase = true)
        {
            _walletValue = treasury;
            _allowPurchase = allowPurchase;
            _graph.AllowPurchase = allowPurchase;
            _graph.SetNodes(nodes);
            UpdateWalletLabel();
            ShowDetails(FindSelectedNode());
        }

        public void SetHistoryActions(Action undo, Action redo)
        {
            _undo = undo;
            _redo = redo;
            UpdateEditingControls();
        }

        public void SetAddNodeAction(Action addNode)
        {
            _addNode = addNode;
            UpdateEditingControls();
        }

        public override void UpdateContent(IEnumerable<SkillTreeNode> changes, int treasury, bool allowPurchase = true)
        {
            bool walletChanged = _walletValue != treasury || _allowPurchase != allowPurchase;
            _walletValue = treasury;
            _allowPurchase = allowPurchase;
            _graph.AllowPurchase = allowPurchase;
            if (walletChanged)
            {
                UpdateWalletLabel();
            }

            _graph.UpdateNodes(changes);
            if (walletChanged)
            {
                ShowDetails(FindSelectedNode());
            }
        }

        private void UpdateWalletLabel() =>
            _wallet.text = _walletIcon != null ? _walletValue.ToString() : $"{_currencyLabel}: {_walletValue}";

        public void SetWalletVisible(bool visible)
        {
            _wallet.gameObject.SetActive(visible);
            if (_walletIcon != null) _walletIcon.gameObject.SetActive(visible);
        }

        public void OpenNodeProperties()
        {
            if (!_graph.Editable)
            {
                return;
            }

            var node = FindSelectedNode();
            if (node == null)
            {
                SetFeedback("Select a node to edit its properties.");
                return;
            }

            EditingClosed?.Invoke();
            PropertiesRequested?.Invoke(node.Id);
        }

        public void OpenArrangement()
        {
            if (!_graph.Editable)
            {
                return;
            }

            EditingClosed?.Invoke();
            ArrangementRequested?.Invoke();
        }

        public void SetHeading(string title, string subtitle)
        {
            _heading.text = title;
            _subtitle.text = subtitle;
        }

        public override void SetFeedback(string value)
        {
            _feedback.text = value;
        }

        public override void Open(int selectedId = 0, bool fitView = false)
        {
            base.Open(selectedId, fitView);
            ShowDetails(FindSelectedNode());
        }

        public void SetEditingAllowed(bool allowed)
        {
            _allowEditing = allowed;
            if (!allowed) _graph.Editable = false;
            UpdateEditingControls();
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            (UnityEngine.Object Value, string Name)[] required =
            {
                (_wallet, nameof(_wallet)), (_heading, nameof(_heading)), (_subtitle, nameof(_subtitle)),
                (_hint, nameof(_hint)), (_feedback, nameof(_feedback)), (_zoomLabel, nameof(_zoomLabel)),
                (_category, nameof(_category)), (_title, nameof(_title)), (_description, nameof(_description)),
                (_rank, nameof(_rank)), (_requirement, nameof(_requirement)), (_nextRank, nameof(_nextRank)),
                (_note, nameof(_note)), (_purchaseLabel, nameof(_purchaseLabel)), (_editLabel, nameof(_editLabel)),
                (_purchaseButton, nameof(_purchaseButton)), (_editButton, nameof(_editButton)),
                (_resetButton, nameof(_resetButton)), (_fitButton, nameof(_fitButton)),
                (_zoomOutButton, nameof(_zoomOutButton)), (_zoomInButton, nameof(_zoomInButton)),
                (_closeButton, nameof(_closeButton)), (_closeBottomButton, nameof(_closeBottomButton)),
                (_nodeDataButton, nameof(_nodeDataButton)), (_propertiesButton, nameof(_propertiesButton)),
                (_arrangeButton, nameof(_arrangeButton)), (_undoButton, nameof(_undoButton)),
                (_redoButton, nameof(_redoButton)), (_detailsIcon, nameof(_detailsIcon)),
                (_descriptionScroll, nameof(_descriptionScroll))
            };
            foreach (var reference in required)
            {
                if (reference.Value != null) continue;
                error = $"Skill tree window '{name}' has no {reference.Name} reference. Assign it in the prefab Inspector.";
                return false;
            }
            if (_rankPips == null || _rankPips.Length == 0)
            {
                error = $"Skill tree window '{name}' needs a rank pip template in Rank Pips.";
                return false;
            }
            for (int index = 0; index < _rankPips.Length; index++)
            {
                if (_rankPips[index] != null) continue;
                error = $"Skill tree window '{name}' has an empty Rank Pips reference at index {index}.";
                return false;
            }
            return true;
        }

        protected virtual void OnEnable()
        {
            if (!Validate(out string error))
            {
                Debug.LogError(error, this);
                return;
            }

            _detailsIconView ??= new SkillTreeIconView(_detailsIcon);
            Subscribe();
            UpdateEditingControls();
        }

        protected virtual void OnDisable()
        {
            EditingClosed?.Invoke();
            if (!_subscribed)
            {
                return;
            }

            _graph.Selected -= ShowDetails;
            _graph.ViewChanged -= UpdateZoomLabel;
            _graph.LayoutChanged -= NotifyLayoutChanged;
            _graph.EditRejected -= NotifyEditRejected;

            _purchaseButton.onClick.RemoveListener(RequestPurchase);
            _closeButton.onClick.RemoveListener(RequestClose);
            _closeBottomButton.onClick.RemoveListener(RequestClose);
            _fitButton.onClick.RemoveListener(Fit);
            _zoomOutButton.onClick.RemoveListener(ZoomOut);
            _zoomInButton.onClick.RemoveListener(ZoomIn);
            _editButton.onClick.RemoveListener(ToggleEditing);
            _resetButton.onClick.RemoveListener(RequestResetLayout);
            _nodeDataButton.onClick.RemoveListener(RequestData);
            _propertiesButton.onClick.RemoveListener(OpenNodeProperties);
            _arrangeButton.onClick.RemoveListener(OpenArrangement);
            if (_addNodeButton != null)
            {
                _addNodeButton.onClick.RemoveListener(AddNode);
            }
            _undoButton.onClick.RemoveListener(Undo);
            _redoButton.onClick.RemoveListener(Redo);
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _graph.Selected += ShowDetails;
            _graph.ViewChanged += UpdateZoomLabel;
            _graph.LayoutChanged += NotifyLayoutChanged;
            _graph.EditRejected += NotifyEditRejected;

            _purchaseButton.onClick.AddListener(RequestPurchase);
            _closeButton.onClick.AddListener(RequestClose);
            _closeBottomButton.onClick.AddListener(RequestClose);
            _fitButton.onClick.AddListener(Fit);
            _zoomOutButton.onClick.AddListener(ZoomOut);
            _zoomInButton.onClick.AddListener(ZoomIn);
            _editButton.onClick.AddListener(ToggleEditing);
            _resetButton.onClick.AddListener(RequestResetLayout);
            _nodeDataButton.onClick.AddListener(RequestData);
            _propertiesButton.onClick.AddListener(OpenNodeProperties);
            _arrangeButton.onClick.AddListener(OpenArrangement);
            if (_addNodeButton != null)
            {
                _addNodeButton.onClick.AddListener(AddNode);
            }
            _undoButton.onClick.AddListener(Undo);
            _redoButton.onClick.AddListener(Redo);
            _subscribed = true;
            UpdateZoomLabel();
        }

        private SkillTreeNode FindSelectedNode()
        {
            foreach (var node in _graph.Nodes)
            {
                if (node.Id == _graph.SelectedId && (_graph.Editable || node.Visible))
                {
                    return node;
                }
            }

            return null;
        }

        private void RequestPurchase()
        {
            var node = FindSelectedNode();
            if (_allowPurchase && node != null && node.CanPurchase)
            {
                base.RequestPurchase(node.Id);
            }
        }

        private void RequestResetLayout() => ResetLayoutRequested?.Invoke();
        private void RequestData() => DataRequested?.Invoke();
        private void NotifyLayoutChanged() => LayoutChanged?.Invoke();
        private void NotifyEditRejected(string message) => EditRejected?.Invoke(message);
        private void Fit() => _graph.Fit();
        private void ZoomOut() => _graph.ZoomBy(1f / ZoomStep);
        private void ZoomIn() => _graph.ZoomBy(ZoomStep);

        private void AddNode()
        {
            if (_graph.Editable)
            {
                _addNode?.Invoke();
            }
        }

        private void Undo()
        {
            _undo?.Invoke();
        }

        private void Redo()
        {
            _redo?.Invoke();
        }

        private void UpdateZoomLabel()
        {
            _zoomLabel.text = $"{Mathf.RoundToInt(_graph.Zoom * 100f)}%";
        }

        private void ToggleEditing()
        {
            EditingClosed?.Invoke();
            _graph.Editable = !_graph.Editable;
            UpdateEditingControls();
            if (!_graph.Editable)
            {
                NotifyLayoutChanged();
            }
        }

        private void UpdateEditingControls()
        {
            _editButton.gameObject.SetActive(_allowEditing);
            _resetButton.gameObject.SetActive(_allowEditing);
            _editLabel.text = _graph.Editable ? "Save Tree" : "Edit Tree";
            _nodeDataButton.gameObject.SetActive(_graph.Editable);
            _propertiesButton.gameObject.SetActive(_graph.Editable);
            _arrangeButton.gameObject.SetActive(_graph.Editable);
            if (_addNodeButton != null)
            {
                _addNodeButton.gameObject.SetActive(_graph.Editable && _addNode != null);
            }
            _undoButton.gameObject.SetActive(_graph.Editable && _undo != null);
            _redoButton.gameObject.SetActive(_graph.Editable && _redo != null);
            _subtitle.gameObject.SetActive(!_graph.Editable);
            _hint.text = _graph.Editable ? EditHint : BrowseHint;
        }

        protected virtual void ShowDetails(SkillTreeNode node)
        {
            var hasSelection = node != null;
            _propertiesButton.interactable = hasSelection;
            _nodeDataButton.interactable = hasSelection;
            _detailsIcon.gameObject.SetActive(hasSelection);
            if (!hasSelection)
            {
                ClearDetails();
                return;
            }

            _category.text = node.Category;
            _category.color = node.Color;
            // A presenter can populate an inactive prefab before OnEnable has run.
            _detailsIconView ??= new SkillTreeIconView(_detailsIcon);
            _detailsIconView.SetNode(node, _graph.GetNodeIconRenderer(node));
            var iconRect = _detailsIconView.Graphic.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.Euler(0f, 0f, node.IconRotation);
            _title.text = string.IsNullOrEmpty(node.Title) ? node.Label : node.Title;
            _description.text = node.Description;
            _requirement.text = node.Requirement;
            _requirement.color = node.State == SkillTreeNodeState.Locked ? LockedRequirement : Muted;

            ShowPurchaseDetails(node);
            _descriptionScroll.StopMovement();
            _descriptionScroll.verticalNormalizedPosition = 1f;
        }

        private void ClearDetails()
        {
            _category.text = string.Empty;
            _title.text = "Select a node";
            _description.text = string.Empty;
            _rank.text = string.Empty;
            _requirement.text = string.Empty;
            _nextRank.text = string.Empty;
            _note.text = string.Empty;
            _purchaseButton.gameObject.SetActive(false);
            foreach (var pip in _rankPips)
            {
                pip.gameObject.SetActive(false);
            }
        }

        private void ShowPurchaseDetails(SkillTreeNode node)
        {
            var isRanked = node.MaxLevel > 0 && node.Kind is SkillTreeNodeKind.Root or SkillTreeNodeKind.Skill;
            if (isRanked)
            {
                EnsureRankPips(node.MaxLevel);
            }
            var isSingleRank = isRanked && node.MaxLevel == 1;
            var isMaxed = isRanked && node.Level >= node.MaxLevel;

            if (!isRanked)
            {
                _rank.text = "Progression";
            }
            else if (isSingleRank)
            {
                _rank.text = isMaxed ? "Learned" : "Unlearned";
            }
            else
            {
                _rank.text = $"Rank {node.Level} / {node.MaxLevel}";
            }

            for (int index = 0; index < _rankPips.Length; index++)
            {
                var pip = _rankPips[index];
                pip.gameObject.SetActive(isRanked && !isSingleRank && index < node.MaxLevel);
                pip.color = index < node.Level ? node.Color : EmptyRank;
            }

            _nextRank.text = isRanked ? GetNextRankText(node, isSingleRank, isMaxed) : string.Empty;
            _purchaseButton.gameObject.SetActive(isRanked);
            _purchaseButton.interactable = _allowPurchase && node.CanPurchase;
            _purchaseLabel.text = isMaxed ? "Learned" : node.Cost == 0 ? "Learn for free" : $"Learn for {node.Cost}";
            _note.text = isRanked && !_allowPurchase ? "Purchases are currently unavailable." : string.Empty;
        }

        private void EnsureRankPips(int count)
        {
            // The numeric rank remains readable for unusually large level arrays.
            count = Mathf.Clamp(count, 0, 20);
            if (count <= _rankPips.Length)
            {
                return;
            }

            int previousCount = _rankPips.Length;
            Array.Resize(ref _rankPips, count);
            for (int index = previousCount; index < count; index++)
            {
                var pip = Instantiate(_rankPips[0], _rankPips[0].transform.parent, false);
                pip.name = $"Rank{index + 1}";
                pip.raycastTarget = false;
                _rankPips[index] = pip;
            }
        }

        private string GetNextRankText(SkillTreeNode node, bool isSingleRank, bool isMaxed)
        {
            if (isMaxed)
            {
                return isSingleRank ? "Learned" : "Fully learned";
            }

            var heading = isSingleRank ? "Cost" : $"Next rank: {node.Level + 1}";
            if (node.Cost == 0)
            {
                return $"{heading}\n<size=30>Free</size>";
            }

            var currency = _currencyLabel.ToLowerInvariant();
            var text = $"{heading}\n<size=30>{node.Cost} {currency}</size>";
            if (_walletValue < node.Cost && node.State != SkillTreeNodeState.Locked)
            {
                text += $"\n<color=#D59A7B>Need {(long)node.Cost - _walletValue} more {currency}</color>";
            }

            return text;
        }

    }
}
