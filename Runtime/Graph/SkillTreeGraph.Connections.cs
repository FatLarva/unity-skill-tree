using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private int _selectedConnectionParentId;
        public int SelectedConnectionParentId => _selectedConnectionParentId;

        public void SelectConnection(int nodeId, int parentId)
        {
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node) || FindConnectionIndex(node, parentId) < 0)
            {
                throw new ArgumentException("Choose an existing visual connection.");
            }
            Select(nodeId);
            _selectedConnectionParentId = parentId;
            SetVerticesDirty();
        }

        public void AddVisualParent(int nodeId, int parentId)
        {
            var node = RequireParentEndpoints(nodeId, parentId);
            if (FindConnectionIndex(node, parentId) >= 0) return;
            var candidate = node.Clone();
            var parents = new List<SkillTreeVisualConnection>(candidate.AdditionalParents)
            {
                new(parentId)
            };
            candidate.AdditionalParents = parents.ToArray();
            ValidateParentChange(candidate);
            node.AdditionalParents = candidate.AdditionalParents;
            _selectedConnectionParentId = parentId;
            CommitLayoutChanges();
        }

        public void RemoveVisualParent(int nodeId, int parentId)
        {
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node)) throw new ArgumentException("Choose an existing node.");
            int index = FindConnectionIndex(node, parentId);
            if (index < 0) return;
            if (index == 0) throw new ArgumentException("Reparent the primary connection instead of removing it.");
            var parents = new List<SkillTreeVisualConnection>(node.AdditionalParents);
            parents.RemoveAt(index - 1);
            node.AdditionalParents = parents.ToArray();
            if (_selectedConnectionParentId == parentId) _selectedConnectionParentId = node.ParentId;
            CommitLayoutChanges();
        }

        public void ToggleVisualParent(int nodeId, int parentId)
        {
            var node = RequireParentEndpoints(nodeId, parentId);
            if (FindConnectionIndex(node, parentId) >= 0) RemoveVisualParent(nodeId, parentId);
            else AddVisualParent(nodeId, parentId);
        }

        private SkillTreeNode RequireParentEndpoints(int nodeId, int parentId)
        {
            if (nodeId == 0 || !_nodesById.TryGetValue(nodeId, out var node) ||
                parentId == 0 || !_nodesById.ContainsKey(parentId))
                throw new ArgumentException("Choose an existing node and parent.");
            if (node.Kind == SkillTreeNodeKind.Root) throw new ArgumentException("The root cannot be reparented.");
            return node;
        }

        private void ValidateParentChange(SkillTreeNode candidate)
        {
            var nodes = new List<SkillTreeNode>(_nodes);
            nodes[_nodeIndices[candidate.Id]] = candidate;
            var byId = new Dictionary<int, SkillTreeNode>(_nodesById) { [candidate.Id] = candidate };
            ValidateHierarchy(nodes, byId);
        }

        private static int FindConnectionIndex(SkillTreeNode node, int parentId)
        {
            if ((parentId == 0)) return -1;
            for (int index = 0; index < node.IncomingConnectionCount; index++)
            {
                if (node.GetIncomingConnection(index).ParentId == parentId) return index;
            }
            return -1;
        }

        private static void SetConnectionStyle(SkillTreeNode node, int index, SkillTreeConnectionStyle style)
        {
            if (index == 0) node.Connection = style;
            else node.AdditionalParents[index - 1] = new SkillTreeVisualConnection(node.AdditionalParents[index - 1].ParentId, style);
        }

        private SkillTreeNode GetConnectionParent(SkillTreeNode node, int index)
        {
            int parentId = node.GetIncomingConnection(index).ParentId;
            return !(parentId == 0) && _nodesById.TryGetValue(parentId, out var parent) ? parent : null;
        }

        private int SelectedConnectionIndex(SkillTreeNode node)
        {
            int index = FindConnectionIndex(node, _selectedConnectionParentId);
            return index >= 0 ? index : 0;
        }
    }
}
