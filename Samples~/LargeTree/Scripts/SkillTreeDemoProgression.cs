using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using Game.SkillTree.Requirements;

namespace Game.SkillTree.LargeSample
{
    /// <summary>Example game state. Currency, purchases and effect execution belong to the game.</summary>
    public sealed class SkillTreeDemoProgression : ISkillLevelReader
    {
        private readonly SkillTreeConfig _config;
        private readonly SkillRequirementEvaluator _requirements;
        private readonly Dictionary<int, int> _levels = new();
        private readonly Dictionary<string, float> _stats = new(StringComparer.Ordinal);
        private readonly int _initialPoints;

        public int Wallet { get; private set; }
        public IReadOnlyDictionary<string, float> Stats => _stats;

        public SkillTreeDemoProgression(SkillTreeConfig config, int? initialPoints = null)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            if (initialPoints is < 0) throw new ArgumentOutOfRangeException(nameof(initialPoints));
            _initialPoints = initialPoints ?? config.InitialPoints;
            _requirements = new SkillRequirementEvaluator(config, this);
            Reset();
        }

        public int GetLevel(SkillNodeConfig node) => node != null && node.Owner == _config &&
            _levels.TryGetValue(node.Id, out int level) ? level : 0;

        public float GetStatValue(string stat) => _stats.TryGetValue(stat, out float value) ? value : 0f;

        public bool ChangePoints(int amount)
        {
            int balance = (int)Math.Clamp((long)Wallet + amount, 0L, int.MaxValue);
            if (balance == Wallet) return false;
            Wallet = balance;
            return true;
        }

        public void Reset()
        {
            _levels.Clear();
            _stats.Clear();
            Wallet = _initialPoints;
        }

        public bool TryPurchase(int id, out string message)
        {
            if (!_config.Validate(out message) || !_requirements.Validate(out message)) return false;
            var node = _config.FindNode(id);
            if (node == null || !node.IsRanked)
            {
                message = "Choose a skill with purchasable levels.";
                return false;
            }
            int level = GetLevel(node);
            if (level >= node.MaxLevel)
            {
                message = "This skill is already fully learned.";
                return false;
            }
            var next = node.Levels[level];
            if (!_requirements.IsVisible(node, level) || !_requirements.MeetsRequirements(node, level))
            {
                message = "The skill requirements are not met.";
                return false;
            }
            if (Wallet < next.Cost)
            {
                message = "Not enough points.";
                return false;
            }

            // Check all effects before changing game state. Extend this game-owned dispatcher
            // when adding another effect type; the package does not dictate how effects run.
            foreach (var effect in next.Effects)
            {
                if (effect.Type is not StatModifierEffectType || effect.Parameters is not StatModifierParameters)
                {
                    message = $"The sample game has no effect handler for '{effect.TypeAsset?.name}'.";
                    return false;
                }
            }
            Wallet -= next.Cost;
            _levels[id] = level + 1;
            foreach (var effect in next.Effects) Apply((StatModifierParameters)effect.Parameters);
            message = $"Learned {node.Title}, level {level + 1}.";
            if (next.Effects.Count > 0)
            {
                var parameters = (StatModifierParameters)next.Effects[0].Parameters;
                message += $" {parameters.Stat}: {GetStatValue(parameters.Stat):0.##}.";
            }
            return true;
        }

        private void Apply(StatModifierParameters effect)
        {
            // This demo starts every stat at zero and applies modifiers in purchase order.
            float value = GetStatValue(effect.Stat);
            _stats[effect.Stat] = effect.Operation switch
            {
                SampleModifierOperation.Add => value + effect.Value,
                SampleModifierOperation.Multiply => value * effect.Value,
                SampleModifierOperation.Set => effect.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(effect.Operation))
            };
        }
    }
}

