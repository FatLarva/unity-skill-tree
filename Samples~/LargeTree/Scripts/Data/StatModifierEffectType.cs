using System;
using Game.SkillTree.Data;
using UnityEngine;

namespace Game.SkillTree.LargeSample
{
    public enum SampleModifierOperation
    {
        Add,
        Multiply,
        Set
    }

    [Serializable]
    public sealed class StatModifierParameters : ISkillEffectParameters
    {
        public string Stat = "damage";
        public float Value = 1f;
        public SampleModifierOperation Operation;
    }

    /// <summary>Example data schema. The consuming game decides how and when its modifiers are applied.</summary>
    [CreateAssetMenu(menuName = "Game/Skill Tree/Large Sample/Stat Modifier", fileName = "Stat Modifier")]
    public sealed class StatModifierEffectType : SkillEffectType<StatModifierParameters>
    {
        protected override bool Validate(StatModifierParameters parameters, out string error)
        {
            if (string.IsNullOrWhiteSpace(parameters.Stat))
            {
                error = "Choose a stat.";
                return false;
            }
            if (float.IsNaN(parameters.Value) || float.IsInfinity(parameters.Value))
            {
                error = "The modifier value must be finite.";
                return false;
            }
            if (!Enum.IsDefined(typeof(SampleModifierOperation), parameters.Operation))
            {
                error = "Choose a valid operation.";
                return false;
            }
            error = null;
            return true;
        }

        protected override string Describe(StatModifierParameters parameters)
            => $"{parameters.Stat}: {parameters.Operation} {parameters.Value}";
    }
}


