using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [CreateAssetMenu(menuName = "Game/Skill Tree/Requirements/Node Level", fileName = "Node Level")]
    public sealed class SkillNodeLevelRequirementType : SkillRequirementType
    {
        [Serializable]
        public sealed class Parameters : SkillRequirementParameters
        {
            [SerializeField] private SkillNodeConfig _node;
            [SerializeField, Min(1)] private int _level;

            public SkillNodeConfig Node => _node;
            public int Level => _level;

            public Parameters(SkillNodeConfig node = null, int level = 1)
            {
                _node = node;
                _level = level;
            }
        }

        public override SkillRequirementParameters CreateParameters() => new Parameters();

        public override bool AreParametersValid(SkillRequirementParameters parameters) =>
            parameters is Parameters nodeLevel && nodeLevel.Node != null && nodeLevel.Level > 0;

        public override string Describe(SkillRequirementParameters parameters, string rankLabel) =>
            parameters is Parameters nodeLevel && nodeLevel.Node != null
                ? $"{nodeLevel.Node.Title} · {rankLabel} {nodeLevel.Level}"
                : "Missing skill";
    }
}
