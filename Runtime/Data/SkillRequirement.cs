using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [Serializable]
    public struct SkillRequirement
    {
        [SerializeField] private SkillRequirementType _type;
        [SerializeReference] private SkillRequirementParameters _parameters;

        public SkillRequirementType Type => _type;
        public SkillRequirementParameters Parameters => _parameters;

        public SkillRequirement(SkillRequirementType type, SkillRequirementParameters parameters)
        {
            _type = type != null ? type : throw new ArgumentNullException(nameof(type));
            _parameters = parameters;
        }

        public string Describe(string rankLabel) => _type != null
            ? _type.Describe(_parameters, rankLabel)
            : "Choose a requirement type.";
    }
}
