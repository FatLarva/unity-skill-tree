using System;
using Game.SkillTree.Requirements;
using UnityEngine;

namespace Game.SkillTree.Data
{
    /// <summary>An authoring asset that describes and creates independent runtime requirement checks.</summary>
    public abstract class SkillRequirementResolver : SkillTreeConfigAsset
    {
        public abstract Type ContextType { get; }

        public abstract bool Supports(SkillRequirementType type);
        public abstract bool ValidateBindings(out string error);
        public abstract bool ValidateRequirementType(SkillRequirementType type, out string error);

        /// <summary>The selected asset checks the supplied context type before creating its checks.</summary>
        public ISkillRequirementResolver Create<TContext>(TContext context)
        {
            try
            {
                return CreateWithContext(context);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Requirement resolver '{name}': {exception.Message}", exception);
            }
        }

        protected abstract ISkillRequirementResolver CreateWithContext(object context);
    }

    /// <summary>
    /// Configure declares typed bindings only. Contexts, checks and bound delegates belong to the returned resolver.
    /// </summary>
    public abstract class SkillRequirementResolver<TContext> : SkillRequirementResolver
    {
        public sealed override Type ContextType => typeof(TContext);

        protected abstract void Configure(RequirementBindings<TContext> bindings);

        public sealed override bool Supports(SkillRequirementType type) =>
            type != null && BuildBindings().Supports(type);

        public sealed override bool ValidateBindings(out string error)
        {
            try
            {
                BuildBindings();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Requirement resolver '{name}': {exception.Message}";
                return false;
            }
        }

        public sealed override bool ValidateRequirementType(SkillRequirementType type, out string error)
        {
            try
            {
                if (BuildBindings().ValidateRequirementType(type, out error)) return true;
                error = $"Requirement resolver '{name}': {error}";
                return false;
            }
            catch (Exception exception)
            {
                error = $"Requirement resolver '{name}': {exception.Message}";
                return false;
            }
        }

        protected sealed override ISkillRequirementResolver CreateWithContext(object context)
        {
            if (context is not TContext typedContext)
            {
                throw new ArgumentException(
                    $"Expected runtime context assignable to '{typeof(TContext).FullName}', " +
                    $"received '{context?.GetType().FullName ?? "null"}'.", nameof(context));
            }
            return BuildBindings().Bind(typedContext);
        }

        private RequirementBindings<TContext> BuildBindings()
        {
            var bindings = new RequirementBindings<TContext>();
            Configure(bindings);
            return bindings;
        }
    }
}
