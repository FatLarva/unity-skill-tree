using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        /// <summary>
        /// Applies new snapshots for existing nodes. Unchanged nodes and their label objects are retained.
        /// Use SetNodes when adding or removing node identifiers.
        /// </summary>
        public int UpdateNodes(IEnumerable<SkillTreeNode> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            var updates = new Dictionary<int, SkillTreeNode>();
            var suppliedIds = new HashSet<int>();
            bool topologyChanged = false;
            bool connectionsChanged = false;
            foreach (var node in changes)
            {
                if (node == null || !suppliedIds.Add(node.Id) || !_nodesById.TryGetValue(node.Id, out var current))
                {
                    throw new ArgumentException("Each update must identify one existing node exactly once.", nameof(changes));
                }

                ValidateAppearance(node);
                if (current.HasSameValues(node))
                {
                    continue;
                }

                updates.Add(node.Id, node);
                topologyChanged |= !current.HasSameParents(node);
                connectionsChanged |= current.Visible != node.Visible || current.Position != node.Position ||
                    current.Color != node.Color || current.State != node.State || current.Kind != node.Kind ||
                    !current.Connection.Equals(node.Connection) || !current.HasSameAdditionalConnections(node);
            }

            if (updates.Count == 0)
            {
                return 0;
            }

            if (topologyChanged)
            {
                var merged = new List<SkillTreeNode>(_nodes);
                foreach (var update in updates)
                {
                    merged[_nodeIndices[update.Key]] = update.Value;
                }

                SetNodes(merged);
                if (_selectedId != 0 && updates.ContainsKey(_selectedId))
                {
                    Selected?.Invoke(_nodesById[_selectedId]);
                }
                return updates.Count;
            }

            UpdateChangedReveals(updates);
            float textScale = Mathf.Pow(_zoom, 1f - _textZoomCompensation);
            foreach (var update in updates)
            {
                var node = update.Value;
                _nodes[_nodeIndices[node.Id]] = node;
                _nodesById[node.Id] = node;
                RefreshNodeLabel(node);
            }
            PrepareRevealFrame();
            foreach (var node in updates.Values)
            {
                RefreshLabelTransform(node, textScale);
            }

            int previousSelection = _selectedId;
            if (_selectedId == 0 || !_nodesById.TryGetValue(_selectedId, out var selected) || !IsShown(selected))
            {
                _selectedId = _nodes.Find(IsShown)?.Id ?? 0;
            }

            SetNodeVisualsDirty(connectionsChanged || previousSelection != _selectedId);
            if (previousSelection != _selectedId || (_selectedId != 0 && updates.ContainsKey(_selectedId)))
            {
                Selected?.Invoke(_selectedId == 0 ? null : _nodesById[_selectedId]);
            }
            return updates.Count;
        }

        private void UpdateChangedReveals(IReadOnlyDictionary<int, SkillTreeNode> updates)
        {
            BeginRevealFrameUpdate(updates.Values);
            bool animate = !_suppressNextReveal && isActiveAndEnabled && !Editable && _revealDuration > 0f;
            if (!animate)
            {
                CancelRevealFrame();
                _reveals.Clear();
            }

            foreach (var update in updates)
            {
                var node = update.Value;
                if (!node.Visible)
                {
                    _reveals.Remove(node.Id);
                }
                else if (animate && _previousVisibility.TryGetValue(node.Id, out bool wasVisible) && !wasVisible)
                {
                    SkillTreeNode parent = null;
                    if (!(node.ParentId == 0) && !updates.TryGetValue(node.ParentId, out parent))
                    {
                        _nodesById.TryGetValue(node.ParentId, out parent);
                    }

                    float travel = parent != null && parent.Visible ? _revealTravelDuration : 0f;
                    _reveals[node.Id] = new RevealState(node.Id, travel);
                    _newRevealIds.Add(node.Id);
                }

                _previousVisibility[node.Id] = node.Visible;
            }
            _suppressNextReveal = false;
        }
    }
}
