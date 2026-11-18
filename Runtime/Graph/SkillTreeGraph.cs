using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityTools;

namespace Game.SkillTree
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class SkillTreeGraph : UnityEngine.UI.MaskableGraphic, IPointerDownHandler,
        IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
        IScrollHandler, IMoveHandler, ISubmitHandler, IPointerEnterHandler
    {
        private const float MinimumZoom = 0.3f;
        private const float MaximumZoom = 2.5f;
        private const float PositionLimit = SkillTreeLayout.MaxCoordinate;

        [SerializeField]
        private TMP_FontAsset _font;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 scales labels with the graph. 1 keeps their screen size constant.")]
        private float _textZoomCompensation = 0.25f;

        private float _curveTolerancePixels = 0.25f;
        private float _fitPadding = 32f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Unscaled seconds for a newly visible node to appear after its branch animation. 0 disables the reveal.")]
        private float _revealDuration = 0.24f;

        private readonly List<SkillTreeNode> _nodes = new();
        private readonly Dictionary<int, SkillTreeNode> _nodesById = new();
        private readonly Dictionary<int, int> _nodeIndices = new();
        private readonly Dictionary<int, TextMeshProUGUI> _labels = new();
        private readonly Dictionary<int, bool> _previousVisibility = new();
        private readonly List<int> _retiredLabels = new();

        private Vector2 _pan;
        private float _zoom = 1f;
        private int _selectedId;
        private bool _editable;
        private bool _showNodeLabels = true;
        private bool _suppressNextReveal = true;
#if UNITY_EDITOR
        private bool _refreshAfterValidation;
#endif

        public bool Editable
        {
            get => _editable;
            set
            {
                if (_editable == value)
                {
                    return;
                }

                _editable = value;
                CancelRevealFrame();
                _reveals.Clear();
                _suppressNextReveal = false;
                Refresh();
            }
        }

        public float Zoom => _zoom;
        public float FitPadding
        {
            get => _fitPadding;
            set
            {
                if (!float.IsFinite(value) || value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _fitPadding = value;
            }
        }
        public Vector2 Pan => _pan;
        public Vector2 ViewCenter
        {
            get
            {
                Vector2 center = (rectTransform.rect.center - _pan) / _zoom;
                return new Vector2(center.x, -center.y);
            }
        }
        public int SelectedId => _selectedId;
        public IReadOnlyList<SkillTreeNode> Nodes => _nodes;

        public bool ShowNodeLabels
        {
            get => _showNodeLabels;
            set
            {
                if (_showNodeLabels == value)
                {
                    return;
                }

                _showNodeLabels = value;
                float textScale = Mathf.Pow(_zoom, 1f - _textZoomCompensation);
                foreach (var node in _nodes)
                {
                    RefreshLabelTransform(node, textScale);
                }
            }
        }

        public float RevealDuration
        {
            get => _revealDuration;
            set
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _revealDuration = Mathf.Clamp(value, 0f, 2f);
                if (_revealDuration == 0f)
                {
                    CancelRevealFrame();
                    _reveals.Clear();
                    Refresh();
                }
            }
        }

        public float TextZoomCompensation
        {
            get => _textZoomCompensation;
            set
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                float compensation = Mathf.Clamp01(value);
                if (compensation == _textZoomCompensation)
                {
                    return;
                }

                _textZoomCompensation = compensation;
                Refresh();
            }
        }

        public float CurveTolerancePixels
        {
            get => _curveTolerancePixels;
            set
            {
                if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
                float tolerance = Mathf.Clamp(value, 0.25f, 8f);
                if (_curveTolerancePixels == tolerance) return;
                _curveTolerancePixels = tolerance;
                SetVerticesDirty();
            }
        }

        public event Action<SkillTreeNode> Selected;
        public event Action LayoutChanged;
        public event Action<string> EditRejected;
        public event Action ViewChanged;

        public void SetFont(TMP_FontAsset font)
        {
            _font = font;
            foreach (var label in _labels.Values)
            {
                if (label != null)
                {
                    label.font = font;
                }
            }
        }

        public void SetNodeIconRenderer(SkillTreeNodeIconRenderer renderer)
        {
            SetNodeIconRenderers(renderer == null ? Array.Empty<SkillTreeNodeIconRenderer>() : new[] { renderer });
        }

        public void SetNodes(IEnumerable<SkillTreeNode> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var incoming = new List<SkillTreeNode>();
            var incomingById = new Dictionary<int, SkillTreeNode>();
            foreach (var node in values)
            {
                if (node == null || (node.Id == 0) || !incomingById.TryAdd(node.Id, node))
                {
                    throw new ArgumentException("Each skill tree node needs a unique non-empty ID.", nameof(values));
                }

                ValidateAppearance(node);

                incoming.Add(node);
            }

            ValidateHierarchy(incoming, incomingById);
            UpdateReveals(incoming, incomingById);

            _nodes.Clear();
            _nodes.AddRange(incoming);
            _nodesById.Clear();
            _nodeIndices.Clear();
            for (int index = 0; index < incoming.Count; index++)
            {
                var node = incoming[index];
                _nodesById.Add(node.Id, node);
                _nodeIndices.Add(node.Id, index);
            }

            RefreshLabels();
            PrepareRevealFrame();
            Refresh();
        }

        public void Select(int id)
        {
            if (id == 0 || !_nodesById.TryGetValue(id, out var node) || !IsShown(node))
            {
                return;
            }

            _selectedId = id;
            _selectedConnectionParentId = node.ParentId;
            Refresh();
            Selected?.Invoke(node);
        }

        public void Fit() => Fit(_fitPadding);

        public void Fit(float padding)
        {
            if (!float.IsFinite(padding) || padding < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(padding));
            }
            CancelRevealFrame();
            CancelWheelZoom();
            UpdateOutgoingDirections();
            var first = _nodes.Find(IsShown);
            if (first == null)
            {
                return;
            }

            Vector2 min = first.Position;
            Vector2 max = min;
            foreach (var node in _nodes)
            {
                if (IsShown(node))
                {
                    float extent = Mathf.Max(GetRadius(node) * 1.3f, GetSelectionRadius(node) + 1f);
                    min = Vector2.Min(min, node.Position - Vector2.one * extent);
                    max = Vector2.Max(max, node.Position + Vector2.one * extent);
                    if (Editable)
                    {
                        for (int edge = 0; edge < node.IncomingConnectionCount; edge++)
                        {
                            if (!TryGetConnectionHandles(node, edge, out var start, out var end)) continue;
                            Vector2 startPoint = GetConnectionParent(node, edge).Position + start;
                            Vector2 endPoint = node.Position + end;
                            min = Vector2.Min(min, Vector2.Min(startPoint, endPoint));
                            max = Vector2.Max(max, Vector2.Max(startPoint, endPoint));
                        }
                    }
                }
            }

            Vector2 available = Vector2.Max(Vector2.one, rectTransform.rect.size - Vector2.one * (padding * 2f));
            Vector2 span = max - min;
            float fit = Mathf.Min(available.x / Mathf.Max(1f, span.x), available.y / Mathf.Max(1f, span.y));
            _zoom = Mathf.Clamp(fit, _minimumViewZoom, MaximumZoom);
            Vector2 middle = (min + max) * 0.5f;
            _pan = rectTransform.rect.center + new Vector2(-middle.x, middle.y) * _zoom;
            Refresh();
        }

        public void ZoomBy(float factor)
        {
            ZoomAt(Vector2.zero, _zoom * factor);
        }

        public void ZoomAt(Vector2 anchor, float value)
        {
            CancelRevealFrame();
            CancelWheelZoom();
            ApplyZoomAt(anchor, value);
        }

        private void ApplyZoomAt(Vector2 anchor, float value)
        {
            float next = Mathf.Clamp(value, _minimumViewZoom, MaximumZoom);
            _pan = anchor + (next / _zoom) * (_pan - anchor);
            _zoom = next;
            Refresh();
        }

        public void PanBy(Vector2 canvasDelta)
        {
            CancelRevealFrame();
            CancelWheelZoom();
            _pan += canvasDelta;
            Refresh();
        }

        public void MoveNode(int id, Vector2 position)
        {
            if (!IsFinite(position) || Mathf.Abs(position.x) > PositionLimit || Mathf.Abs(position.y) > PositionLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            if (id == 0 || !_nodesById.TryGetValue(id, out var node))
            {
                throw new ArgumentException($"Unknown skill tree node: {id}.", nameof(id));
            }

            node.Position = position;
            CommitLayoutChanges();
        }

        public void ReparentNode(int id, int parentId)
        {
            var node = RequireParentEndpoints(id, parentId);
            if (node.ParentId == parentId) return;
            var candidate = node.Clone();
            int promoted = FindConnectionIndex(candidate, parentId);
            if (promoted > 0) candidate.Connection = candidate.GetIncomingConnection(promoted).Connection;
            candidate.ParentId = parentId;
            var additional = new List<SkillTreeVisualConnection>(candidate.AdditionalParents);
            additional.RemoveAll(connection => connection.ParentId == parentId);
            candidate.AdditionalParents = additional.ToArray();
            ValidateParentChange(candidate);
            node.ParentId = parentId;
            node.AdditionalParents = candidate.AdditionalParents;
            _selectedConnectionParentId = parentId;
            CommitLayoutChanges();
        }

        public void SetNodeColor(int id, Color tint, bool includeDescendants = false)
        {
            if (!float.IsFinite(tint.r) || !float.IsFinite(tint.g) ||
                !float.IsFinite(tint.b) || !float.IsFinite(tint.a))
            {
                throw new ArgumentException("Node colors must be finite.", nameof(tint));
            }

            if (id == 0 || !_nodesById.TryGetValue(id, out var node))
            {
                throw new ArgumentException($"Unknown skill tree node: {id}.", nameof(id));
            }

            node.Color = tint;
            if (includeDescendants)
            {
                foreach (var descendant in _nodes)
                {
                    for (var parent = GetParent(descendant); parent != null; parent = GetParent(parent))
                    {
                        if (parent.Id == id)
                        {
                            descendant.Color = tint;
                            break;
                        }
                    }
                }
            }

            CommitLayoutChanges();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.preWillRenderCanvases += PrepareDirtyGeometry;
            RebuildIconRenderers();
            CancelRevealFrame();
            _reveals.Clear();
            // Nodes populated while inactive are already a baseline. Reopening the view
            // must not swallow the next purchase's visibility changes.
            _suppressNextReveal = _nodes.Count == 0;
            ClearPurchaseEffects();
            Refresh();
            FocusInput();
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= PrepareDirtyGeometry;
            base.OnDisable();
            HideGeneratedGraphics();
            ClearInput();
            _reveals.Clear();
            _suppressNextReveal = true;
            ClearPurchaseEffects();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            CancelWheelZoom();
            if (_revealFrameActive) PrepareRevealFrame();
            Refresh();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _textZoomCompensation = Mathf.Clamp01(_textZoomCompensation);
            _revealDuration = Mathf.Clamp(_revealDuration, 0f, 2f);
            _revealTravelDuration = Mathf.Clamp(_revealTravelDuration, 0f, 2f);
            _refreshAfterValidation = true;
        }
#endif

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (_refreshAfterValidation)
            {
                _refreshAfterValidation = false;
                if (_revealDuration == 0f)
                {
                    CancelRevealFrame();
                    _reveals.Clear();
                }

                RebuildIconRenderers();
                Refresh();
            }
#endif
            RefreshPixelScale();
            RefreshDecorationRenderer();
            AdvanceWheelZoom(Time.unscaledDeltaTime);
            if (!AdvanceRevealFrame(Time.unscaledDeltaTime)) AdvanceReveals(Time.unscaledDeltaTime);
            AdvancePurchaseEffects(Time.unscaledDeltaTime);
            UpdatePurchaseGlow(Time.unscaledTimeAsDouble);
        }

        private static void ValidateHierarchy(
            List<SkillTreeNode> nodes,
            Dictionary<int, SkillTreeNode> nodesById)
        {
            var remaining = new Dictionary<int, int>();
            var children = new Dictionary<int, List<int>>();
            var available = new Queue<int>();
            foreach (var node in nodes)
            {
                var parents = new HashSet<int>();
                for (int index = 0; index < node.IncomingConnectionCount; index++)
                {
                    int parentId = node.GetIncomingConnection(index).ParentId;
                    if (index == 0 && (parentId == 0)) continue;
                    if ((parentId == 0) || !nodesById.ContainsKey(parentId))
                        throw new ArgumentException($"Unknown parent '{parentId}' for '{node.Id}'.");
                    if (parentId == node.Id || !parents.Add(parentId))
                        throw new ArgumentException($"Node '{node.Id}' needs distinct parents and cannot connect to itself.");
                    if (!children.TryGetValue(parentId, out var descendants))
                    {
                        descendants = new List<int>();
                        children.Add(parentId, descendants);
                    }
                    descendants.Add(node.Id);
                }
                remaining.Add(node.Id, parents.Count);
                if (parents.Count == 0) available.Enqueue(node.Id);
            }
            int visited = 0;
            while (available.Count > 0)
            {
                int id = available.Dequeue();
                visited++;
                if (!children.TryGetValue(id, out var descendants)) continue;
                foreach (int child in descendants)
                {
                    if (--remaining[child] == 0) available.Enqueue(child);
                }
            }
            if (visited != nodes.Count) throw new ArgumentException("Visual connections cannot contain cycles.");
        }

        private void RefreshLabels()
        {
            _retiredLabels.Clear();
            foreach (var pair in _labels)
            {
                if (_nodesById.ContainsKey(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                    Destroy(pair.Value.gameObject);
                }

                _retiredLabels.Add(pair.Key);
            }

            foreach (int id in _retiredLabels)
            {
                _labels.Remove(id);
            }

            foreach (var node in _nodes)
            {
                RefreshNodeLabel(node);
            }
        }

        private void RefreshNodeLabel(SkillTreeNode node)
        {
            if (!_labels.TryGetValue(node.Id, out var label) || label == null)
            {
                var labelObject = new GameObject($"Label_{node.Id}", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(transform, false);
                label = labelObject.GetComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 13f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = rectTransform.pivot;
                label.rectTransform.pivot = Vector2.one * 0.5f;
                label.rectTransform.sizeDelta = new Vector2(180f, 26f);
                _labels[node.Id] = label;
            }

            if (label.font != _font && _font != null)
            {
                label.font = _font;
            }

            if (label.text != node.Label)
            {
                label.text = node.Label;
            }
        }

        private void Refresh()
        {
            int previousSelection = _selectedId;
            SkillTreeNode selected = null;
            if (_selectedId == 0 || !_nodesById.TryGetValue(_selectedId, out selected) || !IsShown(selected))
            {
                selected = _nodes.Find(IsShown);
                _selectedId = selected?.Id ?? 0;
            }

            SetVerticesDirty();
            float textScale = Mathf.Pow(_zoom, 1f - _textZoomCompensation);
            foreach (var node in _nodes)
            {
                RefreshLabelTransform(node, textScale);
            }

            ViewChanged?.Invoke();
            if (previousSelection != _selectedId)
            {
                Selected?.Invoke(selected);
            }
        }

        private void RefreshLabelTransform(SkillTreeNode node, float textScale)
        {
            if (!_labels.TryGetValue(node.Id, out var label) || label == null)
            {
                return;
            }

            Vector2 point = ToCanvasPoint(node.Position);
            point.y += node.Kind == SkillTreeNodeKind.Root
                ? -(GetLabelRadius(node) * _zoom + 21f * textScale)
                : GetLabelRadius(node) * _zoom + 18f * textScale;
            bool shown = _showNodeLabels && IsShown(node);
            if (shown)
            {
                Rect bounds = label.rectTransform.rect;
                // TMP caches preferred sizes until its text or layout changes.
                Vector2 halfSize = new Vector2(Mathf.Max(bounds.width, label.preferredWidth),
                    Mathf.Max(bounds.height, label.preferredHeight)) * (textScale * 0.5f);
                shown = IntersectsViewport(point, halfSize);
            }
            if (label.gameObject.activeSelf != shown)
            {
                label.gameObject.SetActive(shown);
            }

            if (!shown)
            {
                return;
            }

            RefreshLabelColor(node, label);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = rectTransform.pivot;
            label.rectTransform.localScale = Vector3.one * textScale;
            label.rectTransform.anchoredPosition = point;
        }

        private void RefreshLabelColor(SkillTreeNode node, TextMeshProUGUI label)
        {
            Color tint = Color.Lerp(new Color(0.5f, 0.57f, 0.5f), node.Color, GetOpacity(node));
            label.color = tint.WithAlpha(tint.a * RevealOpacity(node));
        }

        private bool IsShown(SkillTreeNode node) => Editable || node.Visible &&
            (!_reveals.TryGetValue(node.Id, out var reveal) || !reveal.WaitingForFrame);

        private SkillTreeNode GetParent(SkillTreeNode node)
        {
            return !(node.ParentId == 0) && _nodesById.TryGetValue(node.ParentId, out var parent)
                ? parent
                : null;
        }

        private Vector2 ToCanvasPoint(Vector2 point)
        {
            return new Vector2(point.x, -point.y) * _zoom + _pan;
        }

        private static float GetRadius(SkillTreeNode node)
        {
            return GetBaseRadius(node) * node.IconScale;
        }

        private static float GetBaseRadius(SkillTreeNode node)
        {
            return node.Kind switch
            {
                SkillTreeNodeKind.Root => 22f,
                SkillTreeNodeKind.Branch => 5.7f,
                _ => 10f
            };
        }

        private static float GetOpacity(SkillTreeNode node)
        {
            return node.State switch
            {
                SkillTreeNodeState.Locked => 0.26f,
                SkillTreeNodeState.Disabled => 0.12f,
                SkillTreeNodeState.Available => 0.75f,
                _ => 1f
            };
        }

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}
