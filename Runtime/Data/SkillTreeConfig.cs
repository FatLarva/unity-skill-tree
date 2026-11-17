using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Tree", fileName = "SkillTree")]
    public sealed class SkillTreeConfig : SkillTreeConfigAsset
    {
        [SerializeField]
        private List<SkillNodeConfig> _nodes = new();

        [SerializeField]
        private SkillTreeLayoutConfig _layout;

        [SerializeField, Tooltip("Requirement types offered when editing levels in this tree.")]
        private List<SkillRequirementType> _requirementTypes = new();

        [SerializeField, Tooltip("Stateless factory for this tree's runtime requirement resolver.")]
        private SkillRequirementResolver _requirementResolver;

        [SerializeField] private List<SkillEffectTypeReference> _effectTypes = new();
        [SerializeField] private SkillTreeEnumSettings _enumGeneration = SkillTreeEnumSettings.Default;
        [SerializeField, HideInInspector] private long _nextNodeId = 1;

        [SerializeField, Min(0)]
        private int _initialPoints = 24;

        public IReadOnlyList<SkillNodeConfig> Nodes => _nodes;
        public SkillTreeLayoutConfig Layout => _layout;
        public int InitialPoints => _initialPoints;
        public IReadOnlyList<SkillRequirementType> RequirementTypes => _requirementTypes;
        public SkillRequirementResolver RequirementResolver => _requirementResolver;
        public IReadOnlyList<SkillEffectTypeReference> EffectTypes => _effectTypes;
        public SkillTreeEnumSettings EnumGeneration => _enumGeneration;

        public void ConfigureEnumGeneration(SkillTreeEnumSettings settings)
        {
            _enumGeneration = settings;
            NotifyChanged();
        }

        public void ConfigureEffectTypes(IEnumerable<SkillEffectTypeReference> types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            var incoming = new List<SkillEffectTypeReference>(types);
            var unique = new HashSet<ScriptableObject>();
            foreach (var type in incoming)
            {
                if (!type.Validate(out string error)) throw new ArgumentException(error, nameof(types));
                if (!unique.Add(type.Asset)) throw new ArgumentException("Effect types must be distinct assets.", nameof(types));
            }
            _effectTypes = incoming;
            NotifyChanged();
        }

        public void ConfigureRequirements(IEnumerable<SkillRequirementType> types, SkillRequirementResolver resolver)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            var incoming = new List<SkillRequirementType>(types);
            if (!SkillTreeConfigValidation.ValidateRequirementResolver(incoming, resolver, out string error))
                throw new ArgumentException(error, nameof(resolver));
            _requirementTypes = incoming;
            _requirementResolver = resolver;
            NotifyChanged();
        }

        /// <summary>Adds a node owned by this tree and allocates its stable ID if it has none.</summary>
        public int AddNode(SkillNodeConfig node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.Contains(node)) throw new ArgumentException("The node already belongs to this list.", nameof(node));
            CheckOwnership(node);
            SynchronizeNodeIds();
            int id = node.Id;
            if (id == 0)
            {
                if (_nextNodeId > int.MaxValue) throw new InvalidOperationException("This tree has exhausted its integer node IDs.");
                id = (int)_nextNodeId;
            }
            if (id <= 0 || FindNode(id) != null)
                throw new ArgumentException($"Node ID {id} is invalid or already present in tree '{name}'.", nameof(node));
            node.BindToTree(this, id);
            _nextNodeId = Math.Max(_nextNodeId, (long)id + 1);
            _nodes.Add(node);
            NotifyChanged();
            return id;
        }

        /// <summary>Assigns IDs to newly assigned nodes without changing any existing identifier.</summary>
        public void SynchronizeNodeIds()
        {
            var identifiers = new HashSet<int>();
            var assets = new HashSet<SkillNodeConfig>();
            long next = Math.Max(1, _nextNodeId);
            foreach (var node in _nodes)
            {
                if (node == null) throw new InvalidOperationException($"Tree '{name}' contains an empty node reference.");
                CheckOwnership(node);
                if (!assets.Add(node)) throw new InvalidOperationException($"Node '{node.name}' is listed twice in tree '{name}'.");
                if (node.Id < 0 || (node.Id != 0 && !identifiers.Add(node.Id)))
                    throw new InvalidOperationException($"Node '{node.name}' has invalid or duplicate ID {node.Id} in tree '{name}'.");
                next = Math.Max(next, (long)node.Id + 1);
            }
            long needed = 0;
            foreach (var node in _nodes) if (node.Id == 0) needed++;
            if (needed > 0 && next + needed - 1 > int.MaxValue)
                throw new InvalidOperationException("This tree has exhausted its integer node IDs.");
            foreach (var node in _nodes)
            {
                if (node.Id == 0) node.BindToTree(this, (int)next++);
                else if (node.Owner == null) node.BindToTree(this, node.Id);
            }
            _nextNodeId = next;
        }

        private void CheckOwnership(SkillNodeConfig node)
        {
            if (node.Owner != null && node.Owner != this)
                throw new InvalidOperationException($"Node '{node.name}' belongs to tree '{node.Owner.name}' and cannot be shared with '{name}'.");
        }

        public void Configure(IEnumerable<SkillNodeConfig> nodes, SkillTreeLayoutConfig layout, int initialPoints)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (initialPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialPoints));
            }

            var incoming = new List<SkillNodeConfig>(nodes);
            foreach (var node in incoming)
                if (node != null) CheckOwnership(node);
            if (!SkillTreeConfigValidation.Validate(incoming, layout, _requirementTypes, out string error))
            {
                throw new ArgumentException(error, nameof(nodes));
            }
            if (!ValidateRequirementResolver(out error))
            {
                throw new InvalidOperationException(error);
            }
            if (!SkillTreeConfigValidation.ValidateEffectTypes(incoming, _effectTypes, out error))
                throw new ArgumentException(error, nameof(nodes));

            _nodes = incoming;
            _layout = layout;
            _initialPoints = initialPoints;
            SynchronizeNodeIds();
            NotifyChanged();
        }

        public SkillNodeConfig FindNode(int id)
        {
            foreach (var node in _nodes)
            {
                if (node != null && node.Id == id)
                {
                    return node;
                }
            }

            return null;
        }

        public bool Validate(out string error)
        {
            return SkillTreeConfigValidation.Validate(this, out error);
        }

        public bool ValidateRequirementResolver(out string error)
        {
            return SkillTreeConfigValidation.ValidateRequirementResolver(_requirementTypes, _requirementResolver, out error);
        }

        /// <summary>Copies root value records; their nested level arrays are shared for read-only access.</summary>
        public void CopyNodePropertiesTo(List<SkillNodeProperties> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (var node in _nodes)
            {
                if (node != null)
                {
                    destination.Add(node.Properties);
                }
            }
        }

        protected override void OnValidate()
        {
            _nodes ??= new List<SkillNodeConfig>();
            _requirementTypes ??= new List<SkillRequirementType>();
            _effectTypes ??= new List<SkillEffectTypeReference>();
            _nextNodeId = Math.Max(1, _nextNodeId);
            _initialPoints = Mathf.Max(0, _initialPoints);
            base.OnValidate();
        }
    }
}
