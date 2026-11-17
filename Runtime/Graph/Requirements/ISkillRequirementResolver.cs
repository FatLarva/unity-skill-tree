using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    public interface ISkillRequirementResolver
    {
        /// <summary>Return Unhandled only for unsupported requirements; an unmet condition is a handled result.</summary>
        SkillRequirementResolution Resolve(SkillRequirement requirement);
    }
}
