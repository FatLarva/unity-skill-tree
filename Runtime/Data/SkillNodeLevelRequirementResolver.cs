using Game.SkillTree.Requirements;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Requirements/Node Level Resolver", fileName = "Node Level Resolver")]
    public sealed class SkillNodeLevelRequirementResolver : SkillRequirementResolver<ISkillLevelReader>
    {
        protected override void Configure(RequirementBindings<ISkillLevelReader> bindings)
        {
            bindings.Add<SkillNodeLevelRequirementType, SkillNodeLevelRequirementType.Parameters>(
                levels => new SkillNodeLevelCheck(levels).IsMet);
        }
    }
}
