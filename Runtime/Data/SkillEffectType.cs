using System;

namespace Game.SkillTree.Data
{
    /// <summary>
    /// Optional convenience base. Descriptors with an existing SO base can implement ISkillEffectType directly.
    /// </summary>
    public abstract class SkillEffectType<TParameters> : SkillTreeConfigAsset, ISkillEffectType
        where TParameters : class, ISkillEffectParameters, new()
    {
        public Type ParametersType => typeof(TParameters);
        public virtual ISkillEffectParameters CreateParameters() => new TParameters();

        public bool ValidateParameters(ISkillEffectParameters parameters, out string error)
        {
            if (parameters is not TParameters typed)
            {
                error = $"Expected {typeof(TParameters).FullName}.";
                return false;
            }
            return Validate(typed, out error);
        }

        public string Describe(ISkillEffectParameters parameters) => parameters is TParameters typed
            ? Describe(typed)
            : $"Invalid {name} parameters";

        protected virtual bool Validate(TParameters parameters, out string error)
        {
            error = null;
            return true;
        }

        protected virtual string Describe(TParameters parameters) => name;
    }
}
