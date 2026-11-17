using System;
using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    public sealed class SkillRequirementResolverChain : ISkillRequirementResolver
    {
        private readonly ISkillRequirementResolver[] _resolvers;

        public SkillRequirementResolverChain(params ISkillRequirementResolver[] resolvers)
        {
            if (resolvers == null) throw new ArgumentNullException(nameof(resolvers));
            _resolvers = (ISkillRequirementResolver[])resolvers.Clone();
            foreach (var resolver in _resolvers)
            {
                if (resolver == null)
                {
                    throw new ArgumentException("Every chain entry must contain a resolver.", nameof(resolvers));
                }
            }
        }

        public SkillRequirementResolution Resolve(SkillRequirement requirement)
        {
            foreach (var resolver in _resolvers)
            {
                var result = resolver.Resolve(requirement);
                if (result.Handled) return result;
            }
            return SkillRequirementResolution.Unhandled;
        }
    }
}
