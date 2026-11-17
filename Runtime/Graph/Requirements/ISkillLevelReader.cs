using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    public interface ISkillLevelReader
    {
        int GetLevel(SkillNodeConfig node);
    }
}
