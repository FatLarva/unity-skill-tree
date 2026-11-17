using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    /// <summary>
    /// One effect entry. The descriptor is shared; the serialized parameters belong to this entry.
    /// Treat parameters from an authored tree as read-only at runtime. Applying an effect belongs to the game.
    /// </summary>
    [Serializable]
    public struct SkillEffect
    {
        [SerializeField] private SkillEffectTypeReference _type;
        [SerializeReference] private ISkillEffectParameters _parameters;

        public ScriptableObject TypeAsset => _type.Asset;
        public ISkillEffectType Type => _type.Value;
        public ISkillEffectParameters Parameters => _parameters;

        public SkillEffect(SkillEffectTypeReference type, ISkillEffectParameters parameters)
        {
            _type = type;
            _parameters = parameters;
            if (!Validate(out string error)) throw new ArgumentException(error, nameof(parameters));
        }

        public static SkillEffect Create<T>(T type, ISkillEffectParameters parameters = null)
            where T : ScriptableObject, ISkillEffectType
        {
            var reference = new SkillEffectTypeReference(type);
            return new SkillEffect(reference, parameters ?? type.CreateParameters());
        }

        public bool Validate(out string error)
        {
            if (!_type.Validate(out error)) return false;
            if (_parameters == null)
            {
                error = $"Effect '{TypeAsset.name}' has no parameters. Select its type or reset its parameters in the Inspector.";
                return false;
            }

            Type actualType = _parameters.GetType();
            if (_parameters is UnityEngine.Object || !actualType.IsSerializable)
            {
                error = $"Effect '{TypeAsset.name}' parameters must be a serializable managed class, not a Unity object.";
                return false;
            }
            if (!Type.ParametersType.IsInstanceOfType(_parameters))
            {
                error = $"Effect '{TypeAsset.name}' expects '{Type.ParametersType.FullName}', but has '{actualType.FullName}'.";
                return false;
            }
            if (!Type.ValidateParameters(_parameters, out error))
            {
                error = $"Effect '{TypeAsset.name}': {error ?? "invalid parameters"}";
                return false;
            }

            error = null;
            return true;
        }

        public string Describe() => Validate(out string error) ? Type.Describe(_parameters) : error;
    }
}
