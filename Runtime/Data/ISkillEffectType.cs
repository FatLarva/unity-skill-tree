using System;

namespace Game.SkillTree.Data
{
    /// <summary>
    /// Implement on a ScriptableObject to describe an effect type. The object may inherit any SO base.
    /// This contract describes authored data only; it never applies effects or stores player state.
    /// </summary>
    public interface ISkillEffectType
    {
        Type ParametersType { get; }
        ISkillEffectParameters CreateParameters();
        bool ValidateParameters(ISkillEffectParameters parameters, out string error);
        string Describe(ISkillEffectParameters parameters);
    }
}
