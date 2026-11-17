using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private const float WheelZoomStep = 0.08f;
        private const float WheelZoomSharpness = 18f;

        private readonly Dictionary<int, Vector2> _pointers = new();
        private readonly List<int> _dragNodeIds = new();

        private int _draggingId;
        private int _draggingHandle = -1;
        private bool _dragged;
        private bool _movedNode;
        private bool _wheelZoomActive;
        private float _wheelZoomTarget;
        private Vector2 _wheelZoomAnchor;

        public void FocusInput()
        {
            EventSystem.current?.SetSelectedGameObject(gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            FocusInput();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CancelRevealFrame()) Refresh();
            CancelWheelZoom();
            Vector2 point = ToLocalPoint(eventData);
            _pointers[eventData.pointerId] = point;
            _dragged = false;
            _movedNode = false;
            bool canEdit = Editable && eventData.button == PointerEventData.InputButton.Left;
            _draggingHandle = canEdit ? HitHandle(point) : -1;
            _draggingId = canEdit ? _draggingHandle >= 0 ? _selectedId : Hit(point) : 0;
            if (_draggingId != 0 && _draggingId != _selectedId)
            {
                Select(_draggingId);
            }
            PrepareDragNodes();
            FocusInput();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointers.Remove(eventData.pointerId);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (CancelRevealFrame()) Refresh();
            CancelWheelZoom();
            _dragged = true;
            PrepareDragNodes();
        }

        public void OnDrag(PointerEventData eventData)
        {
            int pointerId = eventData.pointerId;
            Vector2 point = ToLocalPoint(eventData);
            Vector2 before = _pointers.TryGetValue(pointerId, out Vector2 previous) ? previous : point;

            if (_pointers.Count >= 2)
            {
                ZoomFromPinch(pointerId, before, point);
            }
            else if (Editable && _draggingId != 0 && _nodesById.TryGetValue(_draggingId, out var node))
            {
                Vector2 canvasDelta = (point - before) / _zoom;
                Vector2 treeDelta = new Vector2(canvasDelta.x, -canvasDelta.y);
                _movedNode |= _draggingHandle >= 0 ? DragHandle(node, _draggingHandle, treeDelta) : ApplyDragGroup(_dragNodeIds, treeDelta);
                Refresh();
            }
            else
            {
                PanBy(point - before);
            }

            _pointers[pointerId] = point;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool layoutChanged = _movedNode;
            _movedNode = false;
            _draggingId = 0;
            _draggingHandle = -1;
            _dragNodeIds.Clear();
            _pointers.Remove(eventData.pointerId);
            if (layoutChanged)
            {
                CommitLayoutChanges();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragged)
            {
                return;
            }

            Vector2 point = ToLocalPoint(eventData);
            if (Editable && HitHandle(point) >= 0)
            {
                return;
            }

            int id = Hit(point);
            int connectionParent = 0;
            if (id == 0 && Editable && eventData.button == PointerEventData.InputButton.Left)
            {
                id = HitConnection(point, out connectionParent);
            }
            if (id == 0)
            {
                return;
            }

            if (Editable && eventData.button == PointerEventData.InputButton.Right && _selectedId != 0)
            {
                try
                {
                    if (Modifiers.ToggleAdditionalParent) ToggleVisualParent(_selectedId, id);
                    else ReparentNode(_selectedId, id);
                }
                catch (System.ArgumentException exception)
                {
                    EditRejected?.Invoke(exception.Message);
                }
            }
            else
            {
                if (connectionParent != 0) SelectConnection(id, connectionParent);
                else Select(id);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_pointers.Count != 0) return;
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
            Vector2 ticks = module != null
                ? module.ConvertPointerEventScrollDeltaToTicks(eventData.scrollDelta)
                : eventData.scrollDelta;
            if (!float.IsFinite(ticks.y) || Mathf.Approximately(ticks.y, 0f)) return;

            if (CancelRevealFrame()) Refresh();

            float currentTarget = _wheelZoomActive ? _wheelZoomTarget : _zoom;
            // A reversal responds from the current view instead of first finishing
            // the previous movement in the opposite direction.
            if (Mathf.Sign(ticks.y) != Mathf.Sign(currentTarget - _zoom)) currentTarget = _zoom;
            _wheelZoomTarget = Mathf.Clamp(currentTarget * Mathf.Exp(Mathf.Clamp(ticks.y, -20f, 20f) * WheelZoomStep),
                _minimumViewZoom, MaximumZoom);
            _wheelZoomAnchor = ToLocalPoint(eventData);
            _wheelZoomActive = !Mathf.Approximately(_wheelZoomTarget, _zoom);
            eventData.Use();
        }

        private void AdvanceWheelZoom(float unscaledDelta)
        {
            if (!_wheelZoomActive || !float.IsFinite(unscaledDelta) || unscaledDelta <= 0f) return;
            float blend = 1f - Mathf.Exp(-WheelZoomSharpness * unscaledDelta);
            float next = Mathf.Exp(Mathf.Lerp(Mathf.Log(_zoom), Mathf.Log(_wheelZoomTarget), blend));
            if (Mathf.Abs(Mathf.Log(next / _wheelZoomTarget)) < 0.001f)
            {
                next = _wheelZoomTarget;
                _wheelZoomActive = false;
            }
            ApplyZoomAt(_wheelZoomAnchor, next);
        }

        private void CancelWheelZoom() => _wheelZoomActive = false;

        public void OnMove(AxisEventData eventData)
        {
            if (_nodes.Count == 0)
            {
                return;
            }

            int index = Mathf.Max(0, _nodes.FindIndex(node => node.Id == _selectedId));
            Vector2 direction = eventData.moveVector;
            if (Editable)
            {
                CollectDragNodes(_nodes[index].Id, Modifiers.MoveSubtree);
                if (ApplyDragGroup(_dragNodeIds, new Vector2(direction.x, -direction.y) * 8f))
                {
                    CommitLayoutChanges();
                }
            }
            else
            {
                SelectNeighbor(index, direction.x + direction.y > 0f ? 1 : -1);
            }

            eventData.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Select(_selectedId);
        }

        private void ClearInput()
        {
            CancelRevealFrame();
            CancelWheelZoom();
            _pointers.Clear();
            _dragNodeIds.Clear();
            _draggingId = 0;
            _draggingHandle = -1;
            _dragged = false;
            _movedNode = false;
        }

        private void ZoomFromPinch(int pointerId, Vector2 before, Vector2 point)
        {
            foreach (var otherPointer in _pointers)
            {
                if (otherPointer.Key == pointerId)
                {
                    continue;
                }

                float previousDistance = Vector2.Distance(before, otherPointer.Value);
                if (previousDistance > 1f)
                {
                    float distance = Vector2.Distance(point, otherPointer.Value);
                    ZoomAt((point + otherPointer.Value) * 0.5f, _zoom * distance / previousDistance);
                }

                break;
            }
        }

        public void MoveSubtree(int id, Vector2 position)
        {
            if (id <= 0 || !_nodesById.TryGetValue(id, out var node)) throw new System.ArgumentException("Choose an existing node.");
            if (!IsFinite(position)) throw new System.ArgumentOutOfRangeException(nameof(position));
            CollectDragNodes(id, true);
            if (ApplyDragGroup(_dragNodeIds, position - node.Position)) CommitLayoutChanges();
        }

        private void PrepareDragNodes()
        {
            CollectDragNodes(_draggingId, _draggingHandle < 0 && Modifiers.MoveSubtree);
        }

        private void CollectDragNodes(int rootId, bool subtree)
        {
            _dragNodeIds.Clear();
            if (rootId == 0) return;
            _dragNodeIds.Add(rootId);
            if (!subtree) return;
            foreach (var node in _nodes)
            {
                for (var parent = GetParent(node); parent != null; parent = GetParent(parent))
                {
                    if (parent.Id != rootId) continue;
                    _dragNodeIds.Add(node.Id);
                    break;
                }
            }
        }

        private bool ApplyDragGroup(IReadOnlyList<int> ids, Vector2 delta)
        {
            if (ids.Count == 0 || !IsFinite(delta)) return false;
            var minimum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var maximum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            foreach (int id in ids)
            {
                var point = _nodesById[id].Position;
                minimum = Vector2.Max(minimum, new Vector2(-PositionLimit - point.x, -PositionLimit - point.y));
                maximum = Vector2.Min(maximum, new Vector2(PositionLimit - point.x, PositionLimit - point.y));
            }
            delta = new Vector2(Mathf.Clamp(delta.x, minimum.x, maximum.x), Mathf.Clamp(delta.y, minimum.y, maximum.y));
            if (delta.sqrMagnitude < 0.000001f) return false;
            foreach (int id in ids) _nodesById[id].Position += delta;
            return true;
        }

        private void SelectNeighbor(int index, int direction)
        {
            for (int offset = 1; offset <= _nodes.Count; offset++)
            {
                int neighborIndex = (index + offset * direction + _nodes.Count) % _nodes.Count;
                var neighbor = _nodes[neighborIndex];
                if (IsShown(neighbor))
                {
                    Select(neighbor.Id);
                    return;
                }
            }
        }

        private Vector2 ToLocalPoint(PointerEventData eventData)
        {
            var owner = canvas != null ? canvas.rootCanvas : null;
            Camera camera = eventData.enterEventCamera ?? eventData.pressEventCamera;
            if (owner != null)
            {
                if (owner.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    camera = null;
                }
                else if (owner.worldCamera != null)
                {
                    camera = owner.worldCamera;
                }
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, camera, out var point);
            return point;
        }

        private int Hit(Vector2 point)
        {
            int id = 0;
            float closestDistance = float.PositiveInfinity;
            foreach (var node in _nodes)
            {
                if (!IsShown(node))
                {
                    continue;
                }

                float distance = Vector2.Distance(point, ToCanvasPoint(node.Position));
                float hitRadius = Mathf.Max(14f, (GetRadius(node) * 1.3f + 5f) * _zoom);
                if (distance <= hitRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    id = node.Id;
                }
            }

            return id;
        }
    }
}
