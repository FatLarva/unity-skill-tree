using System;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private const float HandleRadius = 6f;
        private static readonly Color HandleColor = new(0.6f, 0.85f, 1f);

        public void CommitLayoutChanges()
        {
            RefreshLayoutPreview();
            LayoutChanged?.Invoke();
        }

        public void RefreshLayoutPreview()
        {
            ValidateHierarchy(_nodes, _nodesById);
            foreach (var node in _nodes)
            {
                ValidateAppearance(node);
            }

            int previousSelection = _selectedId;
            RefreshLabels();
            Refresh();
            if (previousSelection == _selectedId)
            {
                Selected?.Invoke(_selectedId != 0 && _nodesById.TryGetValue(_selectedId, out var node) ? node : null);
            }

        }

        public void SetConnectionMode(int nodeId, SkillTreeConnectionMode mode)
        {
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node)) throw new ArgumentException("Choose an existing node.");
            SetConnectionMode(nodeId, node.ParentId, mode);
        }

        public void SetConnectionMode(int nodeId, int parentId, SkillTreeConnectionMode mode)
        {
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node) ||
                !Enum.IsDefined(typeof(SkillTreeConnectionMode), mode))
                throw new ArgumentException("Choose an existing node and a valid connection mode.");
            int index = FindConnectionIndex(node, parentId);
            if (index < 0) throw new ArgumentException("Choose an existing visual connection.");
            var style = node.GetIncomingConnection(index).Connection;
            if (style.Mode == mode) return;
            GetConnectionHandles(nodeId, parentId, out var start, out var end);
            style.Mode = mode;
            style.StartHandle = start;
            style.EndHandle = mode == SkillTreeConnectionMode.Mirrored ? -start : end;
            SetConnectionStyle(node, index, style);
            CommitLayoutChanges();
        }

        public bool GetConnectionHandles(int nodeId, out Vector2 start, out Vector2 end)
        {
            start = end = Vector2.zero;
            return nodeId != 0 && _nodesById.TryGetValue(nodeId, out var node) &&
                   GetConnectionHandles(nodeId, node.ParentId, out start, out end);
        }

        public bool GetConnectionHandles(int nodeId, int parentId, out Vector2 start, out Vector2 end)
        {
            start = end = Vector2.zero;
            UpdateOutgoingDirections();
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node)) return false;
            int index = FindConnectionIndex(node, parentId);
            return index >= 0 && TryGetConnectionHandles(node, index, out start, out end);
        }

        private bool TryGetConnectionHandles(SkillTreeNode node, out Vector2 start, out Vector2 end) =>
            TryGetConnectionHandles(node, 0, out start, out end);

        private bool TryGetConnectionHandles(SkillTreeNode node, int index, out Vector2 start, out Vector2 end)
        {
            start = end = Vector2.zero;
            var parent = GetConnectionParent(node, index);
            if (parent == null) return false;
            var style = node.GetIncomingConnection(index).Connection;
            if (style.Mode != SkillTreeConnectionMode.Auto)
            {
                start = style.StartHandle;
                end = style.Mode == SkillTreeConnectionMode.Mirrored ? -start : style.EndHandle;
                return true;
            }

            Vector2 direction = (node.Position - parent.Position).normalized;
            Vector2 startTangent = GetTangent(parent);
            Vector2 endTangent = index == 0 ? GetTangent(node) : direction;
            if (parent.Kind == SkillTreeNodeKind.Root)
            {
                float bend = 0.46f * direction.x;
                float cos = Mathf.Cos(bend);
                float sin = Mathf.Sin(bend);
                startTangent = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
            }
            else if (startTangent.sqrMagnitude < 0.000001f)
            {
                startTangent = direction;
            }
            float length = Mathf.Min(Vector2.Distance(parent.Position, node.Position) * 0.43f, 150f);
            start = startTangent * length;
            end = -endTangent * length;
            return true;
        }

        private void UpdateOutgoingDirections()
        {
            _outgoingDirections.Clear();
            foreach (var node in _nodes)
            {
                if (!IsShown(node)) continue;
                for (int index = 0; index < node.IncomingConnectionCount; index++)
                {
                    var parent = GetConnectionParent(node, index);
                    if (parent == null || !IsShown(parent)) continue;
                    _outgoingDirections.TryGetValue(parent.Id, out var direction);
                    _outgoingDirections[parent.Id] = direction + (node.Position - parent.Position).normalized;
                }
            }
        }

        private void DrawConnectionHandles(UnityEngine.UI.VertexHelper mesh)
        {
            if (!Editable || _selectedId == 0 || !_nodesById.TryGetValue(_selectedId, out var node)) return;
            int index = SelectedConnectionIndex(node);
            if (!TryGetConnectionHandles(node, index, out var start, out var end)) return;
            var parentNode = GetConnectionParent(node, index);
            Vector2 parent = ToCanvasPoint(parentNode.Position);
            Vector2 child = ToCanvasPoint(node.Position);
            Vector2 startPoint = ToCanvasPoint(parentNode.Position + start);
            Vector2 endPoint = ToCanvasPoint(node.Position + end);
            Color lineColor = HandleColor.WithAlpha(0.7f);
            SkillTreeStroke.Line(mesh, parent, startPoint, 1f, lineColor, _pixels);
            SkillTreeStroke.Line(mesh, child, endPoint, 1f, lineColor, _pixels);
            DrawHandle(mesh, startPoint);
            DrawHandle(mesh, endPoint);
        }

        private void DrawHandle(UnityEngine.UI.VertexHelper mesh, Vector2 position)
        {
            SkillTreeMesh.AddDisc(mesh, position, HandleRadius + 2f, Color.black, Color.black, 16);
            SkillTreeMesh.AddDisc(mesh, position, HandleRadius, HandleColor, HandleColor, 16);
        }

        private int HitHandle(Vector2 point)
        {
            if (_selectedId == 0 || !_nodesById.TryGetValue(_selectedId, out var node)) return -1;
            int index = SelectedConnectionIndex(node);
            if (!GetConnectionHandles(node.Id, node.GetIncomingConnection(index).ParentId, out var start, out var end)) return -1;
            float first = Vector2.Distance(point, ToCanvasPoint(GetConnectionParent(node, index).Position + start));
            float last = Vector2.Distance(point, ToCanvasPoint(node.Position + end));
            if (Mathf.Min(first, last) > HandleRadius + 5f) return -1;
            return first <= last ? 0 : 1;
        }

        private int HitConnection(Vector2 point, out int parentId)
        {
            UpdateOutgoingDirections();
            var pixels = new SkillTreeStroke.PixelSpace(this);
            int closestId = 0;
            parentId = 0;
            float closest = 8f * 8f;
            foreach (var node in _nodes)
            {
                if (!IsShown(node)) continue;
                for (int edge = 0; edge < node.IncomingConnectionCount; edge++)
                {
                    var parent = GetConnectionParent(node, edge);
                    if (parent == null || !IsShown(parent) || !TryGetConnectionHandles(node, edge, out var start, out var end)) continue;
                    SkillTreeStroke.Bezier(_curve, ToCanvasPoint(parent.Position), ToCanvasPoint(parent.Position + start),
                        ToCanvasPoint(node.Position + end), ToCanvasPoint(node.Position), pixels, MaxCurvePoints, _curveTolerancePixels);
                    for (int index = 1; index < _curve.Count; index++)
                    {
                        Vector2 first = _curve[index - 1].Position;
                        Vector2 segment = _curve[index].Position - first;
                        float progress = Mathf.Clamp01(Vector2.Dot(point - first, segment) / Mathf.Max(0.000001f, segment.sqrMagnitude));
                        float distance = (point - first - segment * progress).sqrMagnitude;
                        if (distance >= closest) continue;
                        closest = distance;
                        closestId = node.Id;
                        parentId = parent.Id;
                    }
                }
            }
            return closestId;
        }

        private bool DragHandle(SkillTreeNode node, int index, Vector2 delta)
        {
            int edge = SelectedConnectionIndex(node);
            if (delta.sqrMagnitude < 0.000001f ||
                !GetConnectionHandles(node.Id, node.GetIncomingConnection(edge).ParentId, out var start, out var end)) return false;
            var style = node.GetIncomingConnection(edge).Connection;
            if (style.Mode == SkillTreeConnectionMode.Auto) style.Mode = SkillTreeConnectionMode.Custom;
            if (index == 0) start += delta;
            else end += delta;
            start = ClampPosition(start);
            end = ClampPosition(end);
            if (style.Mode == SkillTreeConnectionMode.Mirrored)
            {
                if (index == 0) end = -start;
                else start = -end;
            }
            style.StartHandle = start;
            style.EndHandle = end;
            SetConnectionStyle(node, edge, style);
            return true;
        }

        private static Vector2 ClampPosition(Vector2 value)
        {
            return new Vector2(Mathf.Clamp(value.x, -PositionLimit, PositionLimit),
                Mathf.Clamp(value.y, -PositionLimit, PositionLimit));
        }

        private static void ValidateAppearance(SkillTreeNode node)
        {
            if (!IsFinite(node.Position) || Mathf.Abs(node.Position.x) > PositionLimit || Mathf.Abs(node.Position.y) > PositionLimit ||
                !IsFinite(node.Color) || !float.IsFinite(node.IconScale) || node.IconScale < 0.1f || node.IconScale > 10f ||
                !float.IsFinite(node.IconRotation))
            {
                throw new ArgumentException($"Invalid appearance for node '{node.Id}'. Positions and colors must be finite; icon scale must be between 0.1 and 10.");
            }

            for (int index = 0; index < node.IncomingConnectionCount; index++)
            {
                ValidateConnectionAppearance(node.Id, node.GetIncomingConnection(index).Connection);
            }
        }

        private static void ValidateConnectionAppearance(int nodeId, SkillTreeConnectionStyle connection)
        {
            if (!Enum.IsDefined(typeof(SkillTreeConnectionMode), connection.Mode) ||
                !IsFinite(connection.StartHandle) || !IsFinite(connection.EndHandle) ||
                Mathf.Abs(connection.StartHandle.x) > PositionLimit || Mathf.Abs(connection.StartHandle.y) > PositionLimit ||
                Mathf.Abs(connection.EndHandle.x) > PositionLimit || Mathf.Abs(connection.EndHandle.y) > PositionLimit ||
                !IsFinite(connection.StartColor) || !IsFinite(connection.EndColor) ||
                !float.IsFinite(connection.Width) || connection.Width < 0.1f || connection.Width > 20f)
            {
                throw new ArgumentException($"Invalid connection for node '{nodeId}'. Control points and colors must be finite; width must be between 0.1 and 20.");
            }
        }

        private static bool IsFinite(Color color)
        {
            return float.IsFinite(color.r) && float.IsFinite(color.g) && float.IsFinite(color.b) && float.IsFinite(color.a);
        }
    }
}
