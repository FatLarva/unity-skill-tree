using System;
using System.Collections.Generic;
using Game.SkillTree.Data;

namespace Game.SkillTree.Requirements
{
    /// <summary>One declaration supplies both editor support metadata and runtime dispatch.</summary>
    public sealed class RequirementBindings<TContext>
    {
        private readonly Dictionary<Type, Binding> _bindings = new();

        /// <summary>
        /// Registers an exact requirement SO type. The check factory runs once when a session is bound,
        /// never when the editor inspects support. The returned callback can own a plain C# checker.
        /// </summary>
        public void Add<TRequirement, TParameters>(Func<TContext, Func<TParameters, bool>> createCheck)
            where TRequirement : SkillRequirementType
            where TParameters : SkillRequirementParameters
        {
            if (createCheck == null)
            {
                throw new ArgumentNullException(nameof(createCheck),
                    $"The check factory for '{typeof(TRequirement).FullName}' cannot be null.");
            }
            if (!_bindings.TryAdd(typeof(TRequirement), new TypedBinding<TParameters>(createCheck)))
            {
                throw new InvalidOperationException(
                    $"Requirement type '{typeof(TRequirement).FullName}' is registered more than once.");
            }
        }

        internal bool Supports(SkillRequirementType type) =>
            type != null && _bindings.ContainsKey(type.GetType());

        internal bool ValidateRequirementType(SkillRequirementType type, out string error)
        {
            if (type == null)
            {
                error = "Assign a requirement type before checking its binding.";
                return false;
            }
            if (!_bindings.TryGetValue(type.GetType(), out var binding))
            {
                error = $"No typed check is registered for requirement '{type.name}' ({type.GetType().FullName}).";
                return false;
            }

            // Creating authoring parameters does not create a runtime checker or require its dependencies.
            var parameters = type.CreateParameters();
            if (!binding.ParametersType.IsInstanceOfType(parameters))
            {
                error = $"Requirement '{type.name}' creates '{parameters?.GetType().FullName ?? "null"}' parameters, " +
                        $"but its binding expects '{binding.ParametersType.FullName}'.";
                return false;
            }
            error = null;
            return true;
        }

        internal ISkillRequirementResolver Bind(TContext context)
        {
            var checks = new Dictionary<Type, BoundCheck>(_bindings.Count);
            foreach (var pair in _bindings)
            {
                try
                {
                    checks.Add(pair.Key, pair.Value.Create(context));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Cannot create the check for '{pair.Key.FullName}': {exception.Message}", exception);
                }
            }
            return new BoundResolver(checks);
        }

        private abstract class Binding
        {
            public abstract Type ParametersType { get; }
            public abstract BoundCheck Create(TContext context);
        }

        private sealed class TypedBinding<TParameters> : Binding where TParameters : SkillRequirementParameters
        {
            private readonly Func<TContext, Func<TParameters, bool>> _createCheck;

            public TypedBinding(Func<TContext, Func<TParameters, bool>> createCheck) => _createCheck = createCheck;
            public override Type ParametersType => typeof(TParameters);

            public override BoundCheck Create(TContext context)
            {
                var check = _createCheck(context);
                if (check == null) throw new InvalidOperationException("The check factory returned a null callback.");
                return new TypedCheck<TParameters>(check);
            }
        }

        private abstract class BoundCheck
        {
            public abstract bool IsMet(SkillRequirement requirement);
        }

        private sealed class TypedCheck<TParameters> : BoundCheck where TParameters : SkillRequirementParameters
        {
            private readonly Func<TParameters, bool> _check;

            public TypedCheck(Func<TParameters, bool> check) => _check = check;

            public override bool IsMet(SkillRequirement requirement)
            {
                if (requirement.Parameters is not TParameters parameters)
                {
                    throw new ArgumentException(
                        $"Requirement '{requirement.Type.name}' expects '{typeof(TParameters).FullName}' parameters, " +
                        $"received '{requirement.Parameters?.GetType().FullName ?? "null"}'.", nameof(requirement));
                }
                if (!requirement.Type.AreParametersValid(parameters))
                {
                    throw new ArgumentException($"Requirement '{requirement.Type.name}' has invalid parameters.",
                        nameof(requirement));
                }
                return _check(parameters);
            }
        }

        private sealed class BoundResolver : ISkillRequirementResolver
        {
            private readonly Dictionary<Type, BoundCheck> _checks;

            public BoundResolver(Dictionary<Type, BoundCheck> checks) => _checks = checks;

            public SkillRequirementResolution Resolve(SkillRequirement requirement)
            {
                if (requirement.Type == null)
                    throw new ArgumentException("A requirement must reference its type asset.", nameof(requirement));
                if (!_checks.TryGetValue(requirement.Type.GetType(), out var check))
                    return SkillRequirementResolution.Unhandled;
                return check.IsMet(requirement) ? SkillRequirementResolution.Met : SkillRequirementResolution.Unmet;
            }
        }
    }
}
