using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEngine;

namespace Game.SkillTree.Requirements
{
    public sealed class SkillRequirementEvaluator
    {
        private readonly SkillTreeConfig _config;
        private readonly object _context;
        private ISkillRequirementResolver _resolver;
        private SkillRequirementResolver _definition;
        private int _definitionRevision;
        private int _observedRevision;
        private bool _validated;
        private string _error;
        private string _reportedError;
        private readonly Dictionary<(int Node, int Level, bool Show, int Index,
            SkillRequirementType Type, SkillRequirementParameters Parameters), string> _runtimeErrors = new();

        /// <summary>
        /// The root asset selects the factory; context must implement that factory's ContextType.
        /// Each evaluator owns its bound checks. Configuration failures are logged once until they change or recover.
        /// </summary>
        public SkillRequirementEvaluator(SkillTreeConfig config, object context)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _context = context;
            Validate(out _);
        }

        public bool Validate(out string error)
        {
            int revision = GetRevision();
            if (!_validated || revision != _observedRevision)
            {
                _validated = true;
                _observedRevision = revision;
                Refresh();
            }
            error = _error;
            return error == null;
        }

        public bool MeetsRequirements(SkillNodeConfig node, int levelIndex)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if ((uint)levelIndex >= (uint)node.Levels.Count) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            return MeetsRequirements(node.Levels[levelIndex].Requirements, node.Id, levelIndex + 1, false);
        }

        public bool MeetsRequirements(SkillLevel level) => MeetsRequirements(level.Requirements, 0, 0, false);

        public bool IsVisible(SkillNodeConfig node, int acquiredLevels)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (acquiredLevels < 0) throw new ArgumentOutOfRangeException(nameof(acquiredLevels));
            if (acquiredLevels > 0 || !node.HideUntilAvailable) return true;
            if (node.ShowRequirements.Count > 0)
                return MeetsRequirements(node.ShowRequirements, node.Id, 0, true);
            return !node.IsRanked || MeetsRequirements(node, acquiredLevels);
        }

        private bool MeetsRequirements(IReadOnlyList<SkillRequirement> requirements,
            int nodeId, int levelNumber, bool showRequirements)
        {
            if (!Validate(out _)) return false;
            for (int index = 0; index < requirements.Count; index++)
            {
                var requirement = requirements[index];
                SkillRequirementResolution result;
                try
                {
                    result = _resolver.Resolve(requirement);
                }
                catch (Exception exception)
                {
                    ReportRuntimeError(nodeId, levelNumber, showRequirements, index, requirement,
                        $"{Location(nodeId, levelNumber, showRequirements)}: {exception.Message}");
                    return false;
                }
                if (!result.Handled)
                {
                    ReportRuntimeError(nodeId, levelNumber, showRequirements, index, requirement,
                             $"{Location(nodeId, levelNumber, showRequirements)} uses " +
                             $"'{(requirement.Type != null ? requirement.Type.name : "missing requirement type")}', " +
                             "but the bound resolver did not handle it.");
                    return false;
                }
                // A failed game-side check must be retried with the next runtime state, without
                // poisoning the configuration or suppressing a later failure after recovery.
                if (_runtimeErrors.Count != 0)
                    _runtimeErrors.Remove((nodeId, levelNumber, showRequirements, index,
                        requirement.Type, requirement.Parameters));
                if (!result.IsMet) return false;
            }
            return true;
        }

        private void Refresh()
        {
            try
            {
                if (!_config.ValidateRequirementResolver(out string error))
                {
                    SetError($"Skill tree '{_config.name}': {error}");
                    return;
                }
                var definition = _config.RequirementResolver;
                if (definition == null)
                {
                    SetError($"Skill tree '{_config.name}' has no requirement resolver factory. " +
                             "Assign a resolver asset to the root config.");
                    return;
                }
                int definitionRevision = definition.Revision;
                if (_resolver == null || _definition != definition || _definitionRevision != definitionRevision)
                {
                    _resolver = null;
                    _resolver = definition.Create(_context);
                    _definition = definition;
                    _definitionRevision = definitionRevision;
                    _runtimeErrors.Clear();
                }
                SetError(null);
            }
            catch (Exception exception)
            {
                SetError($"Skill tree '{_config.name}': {exception.Message}");
            }
        }

        private void SetError(string error)
        {
            _error = error;
            if (error == null)
            {
                _reportedError = null;
                return;
            }
            if (string.Equals(_reportedError, error, StringComparison.Ordinal)) return;
            _reportedError = error;
            Debug.LogError(error, _config);
        }

        private void ReportRuntimeError(int nodeId, int levelNumber, bool showRequirements, int index,
            SkillRequirement requirement, string error)
        {
            var key = (nodeId, levelNumber, showRequirements, index, requirement.Type, requirement.Parameters);
            if (_runtimeErrors.TryGetValue(key, out string previous) &&
                string.Equals(previous, error, StringComparison.Ordinal)) return;
            _runtimeErrors[key] = error;
            Debug.LogError(error, _config);
        }

        private int GetRevision()
        {
            var hash = new HashCode();
            hash.Add(_config.Revision);
            var definition = _config.RequirementResolver;
            if (definition != null)
            {
                hash.Add(definition.GetEntityId());
                hash.Add(definition.Revision);
            }
            foreach (var type in _config.RequirementTypes)
            {
                if (type == null) continue;
                hash.Add(type.GetEntityId());
                hash.Add(type.Revision);
            }
            return hash.ToHashCode();
        }

        private string Location(int nodeId, int levelNumber, bool showRequirements)
        {
            if (nodeId == 0) return $"Skill tree '{_config.name}'";
            return showRequirements
                ? $"Show requirements of skill '{nodeId}' in tree '{_config.name}'"
                : $"Skill '{nodeId}' level {levelNumber} in tree '{_config.name}'";
        }
    }
}
