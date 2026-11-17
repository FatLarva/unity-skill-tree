using System;

namespace Game.SkillTree.Data
{
    [Serializable]
    public abstract class SkillRequirementParameters { }

    public abstract class SkillRequirementType : SkillTreeConfigAsset
    {
        public virtual SkillRequirementParameters CreateParameters() => null;

        public virtual bool AreParametersValid(SkillRequirementParameters parameters) => true;

        public virtual string Describe(SkillRequirementParameters parameters, string rankLabel) => name;
    }
}
