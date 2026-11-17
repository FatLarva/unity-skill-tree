using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public static class SkillTreeAutoLayout
    {
        public static void Apply(IReadOnlyList<SkillTreeNode> nodes, SkillTreeArrangementSettings settings,
            int selectedNodeId = 0)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Validate(settings);
            if (nodes.Count == 0) return;

            if (settings.Scope != SkillTreeArrangementScope.All && (selectedNodeId == 0))
            {
                throw new ArgumentException("Select a node before arranging its branches.");
            }
            if (settings.Scope == SkillTreeArrangementScope.SelectedNodeOnly)
            {
                ArrangeBranches(nodes, settings, selectedNodeId);
                return;
            }
            int subtreeRootId = settings.Scope == SkillTreeArrangementScope.SelectedSubtreeOnly ? selectedNodeId : 0;
            int centeredRootId = settings.Preset == SkillTreeLayoutPreset.CenteredCircle && subtreeRootId == 0
                ? selectedNodeId
                : 0;
            var tree = new Tree(nodes, subtreeRootId, centeredRootId);
            var positions = new Dictionary<int, Vector2>();
            switch (settings.Preset)
            {
                case SkillTreeLayoutPreset.TreeDown:
                case SkillTreeLayoutPreset.TreeUp:
                    ArrangeTree(tree, settings, positions);
                    break;
                case SkillTreeLayoutPreset.Fractal:
                    ArrangeFractal(tree, settings, positions);
                    break;
                case SkillTreeLayoutPreset.Radial:
                    ArrangeRadial(tree, settings, positions);
                    break;
                case SkillTreeLayoutPreset.Circle:
                    ArrangeCircle(tree, settings, positions);
                    break;
                case SkillTreeLayoutPreset.CenteredCircle:
                    ArrangeCenteredCircle(tree, settings, positions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.Preset));
            }

            float angle = settings.Rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector2 anchor = tree.Roots.Count == 1 ? tree.Roots[0].Position : GetCenter(tree.Roots);
            Vector2 offset = tree.Roots.Count == 1 ? positions[tree.Roots[0].Id] : Vector2.zero;
            foreach (var node in tree.Order)
            {
                Vector2 point = positions[node.Id] - offset;
                point = anchor + new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
                ValidatePosition(point);

                positions[node.Id] = point;
            }

            // Validate the complete result before changing any of the caller's nodes.
            foreach (var node in tree.Order)
            {
                node.Position = positions[node.Id];
                node.Connection.Mode = SkillTreeConnectionMode.Auto;
            }
        }

        private static void ArrangeTree(Tree tree, SkillTreeArrangementSettings settings, Dictionary<int, Vector2> positions)
        {
            var leftEdges = new Dictionary<int, float>();
            float totalWidth = 0f;
            foreach (var root in tree.Roots) totalWidth += tree.Widths[root.Id];
            float left = -totalWidth * 0.5f;
            foreach (var root in tree.Roots)
            {
                leftEdges[root.Id] = left;
                left += tree.Widths[root.Id];
            }

            float direction = settings.Preset == SkillTreeLayoutPreset.TreeUp ? -1f : 1f;
            foreach (var node in tree.Order)
            {
                float nodeLeft = leftEdges[node.Id];
                positions[node.Id] = new Vector2((nodeLeft + tree.Widths[node.Id] * 0.5f) * settings.HorizontalSpacing,
                    tree.Depths[node.Id] * settings.VerticalSpacing * direction);
                foreach (var child in tree.Children[node.Id])
                {
                    leftEdges[child.Id] = nodeLeft;
                    nodeLeft += tree.Widths[child.Id];
                }
            }
        }

        private static void ArrangeFractal(Tree tree, SkillTreeArrangementSettings settings, Dictionary<int, Vector2> positions)
        {
            var angles = new Dictionary<int, float>();
            for (int i = 0; i < tree.Roots.Count; i++)
            {
                var root = tree.Roots[i];
                positions[root.Id] = new Vector2((i - (tree.Roots.Count - 1) * 0.5f) * settings.HorizontalSpacing * 2f, 0f);
                angles[root.Id] = 90f;
            }

            foreach (var node in tree.Order)
            {
                var children = tree.Children[node.Id];
                float length = settings.VerticalSpacing * Mathf.Pow(settings.LengthScale, tree.Depths[node.Id]);
                for (int i = 0; i < children.Count; i++)
                {
                    float spread = BranchSpread(i, children.Count, settings.BranchAngle);
                    float angle = angles[node.Id] + spread;
                    float radians = angle * Mathf.Deg2Rad;
                    positions[children[i].Id] = positions[node.Id] + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * length;
                    angles[children[i].Id] = angle;
                }
            }
        }

        private static void ArrangeRadial(Tree tree, SkillTreeArrangementSettings settings, Dictionary<int, Vector2> positions)
        {
            var sectors = new Dictionary<int, Vector2>();
            float totalWidth = 0f;
            int maxDepth = tree.Roots.Count == 1 ? 0 : 1;
            foreach (var root in tree.Roots) totalWidth += tree.Widths[root.Id];
            foreach (var node in tree.Order) maxDepth = Mathf.Max(maxDepth, tree.Depths[node.Id] + (tree.Roots.Count == 1 ? 0 : 1));
            float start = -Mathf.PI * 0.5f;
            foreach (var root in tree.Roots)
            {
                float span = Mathf.PI * 2f * tree.Widths[root.Id] / totalWidth;
                sectors[root.Id] = new Vector2(start, span);
                start += span;
            }

            foreach (var node in tree.Order)
            {
                Vector2 sector = sectors[node.Id];
                int depth = tree.Depths[node.Id] + (tree.Roots.Count == 1 ? 0 : 1);
                float radius = maxDepth == 0 ? 0f : settings.Radius * depth / maxDepth;
                float angle = sector.x + sector.y * 0.5f;
                positions[node.Id] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float childStart = sector.x;
                foreach (var child in tree.Children[node.Id])
                {
                    float span = sector.y * tree.Widths[child.Id] / tree.Widths[node.Id];
                    sectors[child.Id] = new Vector2(childStart, span);
                    childStart += span;
                }
            }
        }

        private static void ArrangeCircle(Tree tree, SkillTreeArrangementSettings settings, Dictionary<int, Vector2> positions)
        {
            for (int i = 0; i < tree.Order.Count; i++)
            {
                float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * i / tree.Order.Count;
                positions[tree.Order[i].Id] = tree.Order.Count == 1
                    ? Vector2.zero
                    : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * settings.Radius;
            }
        }

        private static void ArrangeCenteredCircle(Tree tree, SkillTreeArrangementSettings settings, Dictionary<int, Vector2> positions)
        {
            var perimeter = new List<SkillTreeNode>(tree.Order.Count);
            for (int index = 0; index < tree.Roots.Count; index++)
            {
                var root = tree.Roots[index];
                Vector2 center = new Vector2((index - (tree.Roots.Count - 1) * 0.5f) * settings.Radius * 2.5f, 0f);
                positions[root.Id] = center;
                perimeter.Clear();
                foreach (var child in tree.Children[root.Id]) AppendCenteredBranch(tree, child, perimeter);
                for (int slot = 0; slot < perimeter.Count; slot++)
                {
                    float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * slot / perimeter.Count;
                    positions[perimeter[slot].Id] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * settings.Radius;
                }
            }
        }

        private static void AppendCenteredBranch(Tree tree, SkillTreeNode node, List<SkillTreeNode> perimeter)
        {
            var children = tree.Children[node.Id];
            int split = 0;
            int leftCount = 0;
            int descendants = tree.Sizes[node.Id] - 1;
            int imbalance = descendants;
            for (int index = 0; index < children.Count; index++)
            {
                leftCount += tree.Sizes[children[index].Id];
                int nextImbalance = Math.Abs(descendants - leftCount * 2);
                if (nextImbalance < imbalance)
                {
                    split = index + 1;
                    imbalance = nextImbalance;
                }
            }

            // Split between whole child branches. Splitting the flattened descendant
            // list would put ancestors inside chains and make their links double back.
            for (int index = 0; index < split; index++) AppendOutwardBranch(tree, children[index], perimeter, true);
            perimeter.Add(node);
            for (int index = split; index < children.Count; index++) AppendOutwardBranch(tree, children[index], perimeter, false);
        }

        private static void AppendOutwardBranch(Tree tree, SkillTreeNode node, List<SkillTreeNode> perimeter, bool rootAtEnd)
        {
            if (!rootAtEnd) perimeter.Add(node);
            foreach (var child in tree.Children[node.Id]) AppendOutwardBranch(tree, child, perimeter, rootAtEnd);
            if (rootAtEnd) perimeter.Add(node);
        }

        private static void ArrangeBranches(IReadOnlyList<SkillTreeNode> nodes, SkillTreeArrangementSettings settings, int selectedNodeId)
        {
            var tree = new Tree(nodes, selectedNodeId);
            var root = tree.Roots[0];
            var children = tree.Children[root.Id];
            var positions = new Dictionary<int, Vector2>();
            var rotations = new Dictionary<int, float>();
            var branchIds = new Dictionary<int, int>();
            float direction = 90f;
            foreach (var node in nodes)
            {
                if (node.Id == root.ParentId && (root.Position - node.Position).sqrMagnitude > 0.0001f)
                {
                    Vector2 incoming = root.Position - node.Position;
                    direction = Mathf.Atan2(incoming.y, incoming.x) * Mathf.Rad2Deg;
                    break;
                }
            }
            for (int index = 0; index < children.Count; index++)
            {
                var child = children[index];
                Vector2 offset;
                switch (settings.Preset)
                {
                    case SkillTreeLayoutPreset.TreeDown:
                    case SkillTreeLayoutPreset.TreeUp:
                        offset = new Vector2((index - (children.Count - 1) * 0.5f) * settings.HorizontalSpacing,
                            settings.VerticalSpacing * (settings.Preset == SkillTreeLayoutPreset.TreeUp ? -1f : 1f));
                        offset = Rotate(offset, settings.Rotation);
                        break;
                    case SkillTreeLayoutPreset.Fractal:
                        float angle = direction + settings.Rotation + BranchSpread(index, children.Count, settings.BranchAngle);
                        offset = Rotate(Vector2.right * settings.VerticalSpacing, angle);
                        break;
                    default:
                        offset = Rotate(Vector2.right * settings.Radius, -90f + settings.Rotation + 360f * index / children.Count);
                        break;
                }
                Vector2 previous = child.Position - root.Position;
                float turn = previous.sqrMagnitude < 0.0001f ? 0f : Vector2.SignedAngle(previous, offset);
                positions[child.Id] = root.Position + offset;
                rotations[child.Id] = turn;
                branchIds[child.Id] = child.Id;
            }
            foreach (var node in tree.Order)
            {
                if (node == root || branchIds.ContainsKey(node.Id)) continue;
                int branchId = branchIds[node.ParentId];
                var branch = tree.ById[branchId];
                positions[node.Id] = positions[branchId] + Rotate(node.Position - branch.Position, rotations[branchId]);
                rotations[node.Id] = rotations[branchId];
                branchIds[node.Id] = branchId;
            }
            foreach (var position in positions.Values) ValidatePosition(position);
            foreach (var node in tree.Order)
            {
                if (node == root) continue;
                node.Position = positions[node.Id];
                float turn = rotations[node.Id];
                node.Connection = RotateConnection(node.Connection, turn);
                for (int index = 0; index < node.AdditionalParents.Length; index++)
                {
                    var edge = node.AdditionalParents[index];
                    if (branchIds.TryGetValue(edge.ParentId, out int parentBranch) && parentBranch == branchIds[node.Id])
                    {
                        node.AdditionalParents[index] = new SkillTreeVisualConnection(edge.ParentId, RotateConnection(edge.Connection, turn));
                    }
                }
            }
        }

        private static float BranchSpread(int index, int count, float angle)
        {
            if (count <= 1) return 0f;
            return ((index + 0.5f) / count - 0.5f) * angle;
        }

        private static SkillTreeConnectionStyle RotateConnection(SkillTreeConnectionStyle style, float angle)
        {
            style.StartHandle = Rotate(style.StartHandle, angle);
            style.EndHandle = Rotate(style.EndHandle, angle);
            return style;
        }

        private static Vector2 Rotate(Vector2 point, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }

        private static Vector2 GetCenter(IReadOnlyList<SkillTreeNode> nodes)
        {
            Vector2 center = Vector2.zero;
            foreach (var node in nodes) center += node.Position;
            return center / nodes.Count;
        }

        private static void ValidatePosition(Vector2 point)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) ||
                Mathf.Abs(point.x) > SkillTreeLayout.MaxCoordinate || Mathf.Abs(point.y) > SkillTreeLayout.MaxCoordinate)
            {
                throw new ArgumentException("The layout exceeds the tree bounds. Reduce spacing or radius.");
            }
        }

        private static void Validate(SkillTreeArrangementSettings settings)
        {
            if (!Enum.IsDefined(typeof(SkillTreeLayoutPreset), settings.Preset) ||
                !Enum.IsDefined(typeof(SkillTreeArrangementScope), settings.Scope) ||
                !FinitePositive(settings.HorizontalSpacing) || !FinitePositive(settings.VerticalSpacing) ||
                !FinitePositive(settings.Radius) || !float.IsFinite(settings.BranchAngle) ||
                settings.BranchAngle < 0f || settings.BranchAngle > 360f ||
                !FinitePositive(settings.LengthScale) || settings.LengthScale > 1f || !float.IsFinite(settings.Rotation))
            {
                throw new ArgumentException("Use positive spacing and radius, a branch angle from 0 to 360, and a length scale above 0 and at most 1.");
            }
        }

        private static bool FinitePositive(float value) => float.IsFinite(value) && value > 0f;

        private sealed class Tree
        {
            public Dictionary<int, SkillTreeNode> ById { get; } = new();
            public List<SkillTreeNode> Roots { get; } = new();
            public List<SkillTreeNode> Order { get; } = new();
            public Dictionary<int, List<SkillTreeNode>> Children { get; } = new();
            public Dictionary<int, int> Depths { get; } = new();
            public Dictionary<int, int> Widths { get; } = new();
            public Dictionary<int, int> Sizes { get; } = new();

            public Tree(IReadOnlyList<SkillTreeNode> nodes, int subtreeRootId, int centeredRootId = 0)
            {
                var byId = ById;
                foreach (var node in nodes)
                {
                    if (node == null || !byId.TryAdd(node.Id, node))
                    {
                        throw new ArgumentException("Every node needs a unique ID.");
                    }
                    Children.Add(node.Id, new List<SkillTreeNode>());
                }

                foreach (var node in nodes)
                {
                    if ((node.ParentId == 0)) continue;
                    if (!Children.TryGetValue(node.ParentId, out var siblings))
                    {
                        throw new ArgumentException($"Unknown parent '{node.ParentId}' for '{node.Id}'.");
                    }
                    siblings.Add(node);
                }

                // A subtree operation must not accept an invalid disconnected part of the tree.
                var visited = new HashSet<int>();
                var validation = new Queue<SkillTreeNode>();
                foreach (var node in nodes)
                {
                    if ((node.ParentId == 0)) validation.Enqueue(node);
                }
                while (validation.Count > 0)
                {
                    var node = validation.Dequeue();
                    if (!visited.Add(node.Id)) throw new ArgumentException("A tree cannot contain cycles.");
                    foreach (var child in Children[node.Id]) validation.Enqueue(child);
                }
                if (visited.Count != nodes.Count) throw new ArgumentException("A tree cannot contain cycles.");

                if (centeredRootId != 0)
                {
                    if (!byId.TryGetValue(centeredRootId, out var center)) throw new ArgumentException("Select a node first.");
                    var neighbors = new Dictionary<int, List<SkillTreeNode>>();
                    foreach (var node in nodes) neighbors.Add(node.Id, new List<SkillTreeNode>(Children[node.Id]));
                    foreach (var node in nodes)
                    {
                        if (!(node.ParentId == 0)) neighbors[node.Id].Add(byId[node.ParentId]);
                        Children[node.Id].Clear();
                    }
                    visited.Clear();
                    void VisitRoot(SkillTreeNode root)
                    {
                        Roots.Add(root);
                        validation.Enqueue(root);
                        visited.Add(root.Id);
                        while (validation.Count > 0)
                        {
                            var parent = validation.Dequeue();
                            foreach (var child in neighbors[parent.Id])
                            {
                                if (!visited.Add(child.Id)) continue;
                                Children[parent.Id].Add(child);
                                validation.Enqueue(child);
                            }
                        }
                    }
                    VisitRoot(center);
                    foreach (var node in nodes)
                    {
                        if (!visited.Contains(node.Id)) VisitRoot(node);
                    }
                }
                else if (subtreeRootId != 0)
                {
                    if (!byId.TryGetValue(subtreeRootId, out var selected)) throw new ArgumentException("Select a node first.");
                    Roots.Add(selected);
                }
                else
                {
                    foreach (var node in nodes)
                    {
                        if ((node.ParentId == 0)) Roots.Add(node);
                    }
                }

                var queue = new Queue<SkillTreeNode>();
                foreach (var root in Roots)
                {
                    queue.Enqueue(root);
                    Depths.Add(root.Id, 0);
                }
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    Order.Add(node);
                    foreach (var child in Children[node.Id])
                    {
                        if (!Depths.TryAdd(child.Id, Depths[node.Id] + 1)) throw new ArgumentException("A tree cannot contain cycles.");
                        queue.Enqueue(child);
                    }
                }
                if (Roots.Count == 0 || (subtreeRootId == 0 && Order.Count != nodes.Count))
                {
                    throw new ArgumentException("A tree cannot contain cycles.");
                }
                for (int i = Order.Count - 1; i >= 0; i--)
                {
                    var node = Order[i];
                    int width = 0;
                    int size = 1;
                    foreach (var child in Children[node.Id])
                    {
                        width += Widths[child.Id];
                        size += Sizes[child.Id];
                    }
                    Widths[node.Id] = Mathf.Max(1, width);
                    Sizes[node.Id] = size;
                }
            }
        }
    }
}
