using System;
using System.Collections.Generic;
using Game.SkillTree.Requirements;
using UnityEngine;

namespace Game.SkillTree.Data
{
    internal static class SkillTreeConfigValidation
    {
        private const float CoordinateLimit = 20_000f;

        public static bool Validate(SkillTreeConfig tree, out string error)
        {
            if (!Validate(tree.Nodes, tree.Layout, tree.RequirementTypes, out error) ||
                !tree.ValidateRequirementResolver(out error) ||
                !ValidateEffectTypes(tree.Nodes, tree.EffectTypes, out error)) return false;
            foreach (var node in tree.Nodes)
            {
                if (node.Owner != tree)
                {
                    error = $"Node '{node.name}' is not owned by tree '{tree.name}'. Add nodes through the tree Inspector.";
                    return false;
                }
            }
            return true;
        }

        public static bool ValidateEffectTypes(IReadOnlyList<SkillNodeConfig> nodes,
            IReadOnlyList<SkillEffectTypeReference> types, out string error)
        {
            var assets = new HashSet<ScriptableObject>();
            foreach (var type in types)
            {
                if (!type.Validate(out error)) return false;
                if (!assets.Add(type.Asset))
                {
                    error = $"Effect type '{type.Asset.name}' is registered twice.";
                    return false;
                }
            }
            foreach (var node in nodes)
            {
                if (node == null) continue;
                for (int index = 0; index < node.Levels.Count; index++)
                {
                    foreach (var effect in node.Levels[index].Effects)
                    {
                        if (!effect.Validate(out error))
                        {
                            error = $"Node '{node.name}' level {index + 1}: {error}";
                            return false;
                        }
                        if (!assets.Contains(effect.TypeAsset))
                        {
                            error = $"Node '{node.name}' level {index + 1} uses unregistered effect type '{effect.TypeAsset.name}'.";
                            return false;
                        }
                    }
                }
            }
            error = null;
            return true;
        }

        public static bool ValidateRequirementResolver(IReadOnlyList<SkillRequirementType> requirementTypes,
            SkillRequirementResolver resolver, out string error)
        {
            if (resolver == null)
            {
                error = "Assign a requirement resolver SO to the tree.";
                return false;
            }
            if (!resolver.ValidateBindings(out error)) return false;

            var unique = new HashSet<SkillRequirementType>();
            foreach (var type in requirementTypes)
            {
                if (type == null || !unique.Add(type))
                {
                    error = "Assign distinct requirement type assets before checking resolver support.";
                    return false;
                }
                if (!resolver.ValidateRequirementType(type, out error)) return false;
            }

            error = null;
            return true;
        }

        public static bool Validate(IReadOnlyList<SkillNodeConfig> nodeConfigs, SkillTreeLayoutConfig layout,
            IReadOnlyList<SkillRequirementType> requirementTypes, out string error)
        {
            if (layout == null)
            {
                error = "Assign a layout config to the skill tree.";
                return false;
            }

            var nodes = new Dictionary<int, SkillNodeConfig>();
            foreach (var node in nodeConfigs)
            {
                if (node == null || node.Id <= 0)
                {
                    error = "Every skill tree node must reference a config with a stable identifier.";
                    return false;
                }
                if (!nodes.TryAdd(node.Id, node))
                {
                    error = $"The node identifier '{node.Id}' is used more than once.";
                    return false;
                }
                if (!ValidateNode(node.Properties, out error))
                {
                    return false;
                }
            }

            var registered = new HashSet<SkillRequirementType>();
            foreach (var type in requirementTypes)
            {
                if (type == null || !registered.Add(type))
                {
                    error = "Assign distinct requirement types to the skill tree.";
                    return false;
                }
            }
            if (!ValidateRequirements(nodes, registered, out error))
            {
                return false;
            }
            return ValidateLayout(layout, nodes, out error);
        }

        public static bool ValidateNode(SkillNodeProperties node, out string error)
        {
            if (node.Id <= 0 || !Enum.IsDefined(typeof(SkillTreeNodeKind), node.Kind))
            {
                error = "A node needs a stable identifier and a valid kind.";
                return false;
            }
            if (node.Kind == SkillTreeNodeKind.Skill && node.MaxLevel == 0)
            {
                error = $"Skill '{node.Id}' needs at least one level.";
                return false;
            }
            if (!ValidateRequirementParameters(node.ShowRequirements, $"'{node.Id}' show requirements", out error))
            {
                return false;
            }

            for (int index = 0; index < node.Levels.Count; index++)
            {
                var level = node.Levels[index];
                if (level.Cost < 0)
                {
                    error = $"The cost of '{node.Id}' level {index + 1} cannot be negative.";
                    return false;
                }
                foreach (var effect in level.Effects)
                {
                    if (!effect.Validate(out error))
                    {
                        error = $"Effect of '{node.Id}' level {index + 1}: {error}";
                        return false;
                    }
                }
                if (!ValidateRequirementParameters(level.Requirements, $"'{node.Id}' level {index + 1}", out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateRequirementParameters(IReadOnlyList<SkillRequirement> requirements,
            string location, out string error)
        {
            var prerequisites = new HashSet<SkillNodeConfig>();
            foreach (var requirement in requirements)
            {
                if (requirement.Type == null || !requirement.Type.AreParametersValid(requirement.Parameters))
                {
                    string typeName = requirement.Type != null ? requirement.Type.name : "unassigned requirement";
                    error = $"{location} has invalid parameters for '{typeName}'.";
                    return false;
                }
                if (requirement.Type is SkillNodeLevelRequirementType &&
                    requirement.Parameters is SkillNodeLevelRequirementType.Parameters nodeLevel &&
                    !prerequisites.Add(nodeLevel.Node))
                {
                    error = $"Prerequisites of {location} need distinct skill references.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        public static bool ValidateNodeLayout(SkillTreeNodeLayout node, out string error)
        {
            if (node.NodeId <= 0 || node.ParentId < 0 || node.ParentId == node.NodeId)
            {
                error = "A layout needs a node identifier and cannot make that node its own parent.";
                return false;
            }
            if (!IsCoordinate(node.Position) || !IsFinite(node.Color) ||
                !float.IsFinite(node.IconScale) || node.IconScale < 0.1f || node.IconScale > 10f ||
                !float.IsFinite(node.IconRotation) || !Enum.IsDefined(typeof(SkillTreeIconMode), node.IconMode) ||
                !ValidateConnection(node.Connection))
            {
                error = $"Node '{node.NodeId}' has invalid visual settings.";
                return false;
            }

            var parents = new HashSet<int>();
            if (!(node.ParentId == 0)) parents.Add(node.ParentId);
            foreach (var edge in node.AdditionalParents)
            {
                if (edge.ParentId <= 0 || edge.ParentId == node.NodeId ||
                    !parents.Add(edge.ParentId) || !ValidateConnection(edge.Connection))
                {
                    error = $"Node '{node.NodeId}' needs distinct visual parents and valid connection settings.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateRequirements(Dictionary<int, SkillNodeConfig> nodes,
            HashSet<SkillRequirementType> registered, out string error)
        {
            var dependents = new Dictionary<(int Id, int Rank), List<(int Id, int Rank)>>();
            var remaining = new Dictionary<(int Id, int Rank), int>();
            var available = new Queue<(int Id, int Rank)>();
            foreach (var node in nodes.Values)
            {
                var showPrerequisites = new HashSet<(int Id, int Rank)>();
                if (!CollectRequirementReferences(node.ShowRequirements, nodes, registered,
                    $"'{node.Id}' show requirements", showPrerequisites, out error))
                {
                    return false;
                }
                for (int index = 0; index < node.Levels.Count; index++)
                {
                    var rank = (node.Id, index + 1);
                    var prerequisites = new HashSet<(int Id, int Rank)>();
                    if (index > 0)
                    {
                        prerequisites.Add((node.Id, index));
                    }
                    if (!CollectRequirementReferences(node.Levels[index].Requirements, nodes, registered,
                        $"'{node.Id}' level {index + 1}", prerequisites, out error))
                    {
                        return false;
                    }
                    if (index == 0 && node.HideUntilAvailable)
                    {
                        prerequisites.UnionWith(showPrerequisites);
                    }

                    remaining.Add(rank, prerequisites.Count);
                    if (prerequisites.Count == 0)
                    {
                        available.Enqueue(rank);
                    }
                    foreach (var prerequisite in prerequisites)
                    {
                        if (!dependents.TryGetValue(prerequisite, out var children))
                        {
                            children = new List<(int Id, int Rank)>();
                            dependents.Add(prerequisite, children);
                        }
                        children.Add(rank);
                    }
                }
            }

            int visited = 0;
            while (available.Count > 0)
            {
                var rank = available.Dequeue();
                visited++;
                if (!dependents.TryGetValue(rank, out var children))
                {
                    continue;
                }
                foreach (var child in children)
                {
                    if (--remaining[child] == 0)
                    {
                        available.Enqueue(child);
                    }
                }
            }
            if (visited != remaining.Count)
            {
                error = "Skill level prerequisites contain an unreachable cycle.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool CollectRequirementReferences(IReadOnlyList<SkillRequirement> requirements,
            Dictionary<int, SkillNodeConfig> nodes, HashSet<SkillRequirementType> registered,
            string location, HashSet<(int Id, int Rank)> prerequisites, out string error)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.Type != null && !registered.Contains(requirement.Type))
                {
                    error = $"{location} uses an unregistered requirement type '{requirement.Type.name}'.";
                    return false;
                }
                if (requirement.Type is not SkillNodeLevelRequirementType ||
                    requirement.Parameters is not SkillNodeLevelRequirementType.Parameters nodeLevel) continue;
                if (!nodes.TryGetValue(nodeLevel.Node.Id, out var requiredNode) ||
                    requiredNode != nodeLevel.Node || !requiredNode.IsRanked ||
                    nodeLevel.Level > requiredNode.MaxLevel)
                {
                    error = $"{location} has an invalid prerequisite. Use an attainable skill level from this tree.";
                    return false;
                }
                prerequisites.Add((requiredNode.Id, nodeLevel.Level));
            }
            error = null;
            return true;
        }

        private static bool ValidateLayout(SkillTreeLayoutConfig layout,
            Dictionary<int, SkillNodeConfig> nodes, out string error)
        {
            var entries = new Dictionary<int, SkillTreeNodeLayout>();
            foreach (var config in layout.Nodes)
            {
                if (config == null)
                {
                    error = "Every layout entry must reference a node layout config.";
                    return false;
                }
                var entry = config.Properties;
                if (!ValidateNodeLayout(entry, out error))
                {
                    return false;
                }
                if (!nodes.ContainsKey(entry.NodeId) || !entries.TryAdd(entry.NodeId, entry))
                {
                    error = "Every layout config must refer to a unique node in this skill tree.";
                    return false;
                }
                if (!(entry.ParentId == 0) && !nodes.ContainsKey(entry.ParentId))
                {
                    error = $"Node '{entry.NodeId}' has an unknown visual parent '{entry.ParentId}'.";
                    return false;
                }
            }
            if (entries.Count != nodes.Count)
            {
                error = "Assign one node layout config for every gameplay node.";
                return false;
            }

            var remaining = new Dictionary<int, int>();
            var children = new Dictionary<int, List<int>>();
            var available = new Queue<int>();
            foreach (var entry in entries.Values)
            {
                int count = (entry.ParentId == 0) ? 0 : 1;
                for (int index = 0; index <= entry.AdditionalParents.Count; index++)
                {
                    int parentId = index == 0 ? entry.ParentId : entry.AdditionalParents[index - 1].ParentId;
                    if (index == 0 && (parentId == 0)) continue;
                    if (!entries.ContainsKey(parentId))
                    {
                        error = $"Node '{entry.NodeId}' has an unknown visual parent '{parentId}'.";
                        return false;
                    }
                    if (!children.TryGetValue(parentId, out var descendants))
                    {
                        descendants = new List<int>();
                        children.Add(parentId, descendants);
                    }
                    descendants.Add(entry.NodeId);
                }
                count += entry.AdditionalParents.Count;
                remaining.Add(entry.NodeId, count);
                if (count == 0) available.Enqueue(entry.NodeId);
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
            if (visited != entries.Count)
            {
                error = "Visual connections contain a cycle.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateConnection(SkillTreeConnectionStyle connection)
        {
            return Enum.IsDefined(typeof(SkillTreeConnectionMode), connection.Mode) &&
                   IsCoordinate(connection.StartHandle) && IsCoordinate(connection.EndHandle) &&
                   IsFinite(connection.StartColor) && IsFinite(connection.EndColor) &&
                   float.IsFinite(connection.Width) && connection.Width >= 0.1f && connection.Width <= 20f;
        }

        private static bool IsCoordinate(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) &&
                   Mathf.Abs(value.x) <= CoordinateLimit && Mathf.Abs(value.y) <= CoordinateLimit;
        }

        private static bool IsFinite(Color value)
        {
            return float.IsFinite(value.r) && float.IsFinite(value.g) &&
                   float.IsFinite(value.b) && float.IsFinite(value.a);
        }
    }
}
