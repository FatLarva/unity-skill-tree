using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    /// <summary>A Unity-serialized asset reference constrained to the effect descriptor interface.</summary>
    [Serializable]
    public struct SkillEffectTypeReference
    {
        [SerializeField] private ScriptableObject _asset;

        public ScriptableObject Asset => _asset;
        public ISkillEffectType Value => _asset != null ? _asset as ISkillEffectType : null;

        public SkillEffectTypeReference(ScriptableObject asset)
        {
            _asset = asset;
            if (!Validate(out string error)) throw new ArgumentException(error, nameof(asset));
        }

        public static SkillEffectTypeReference Create<T>(T asset) where T : ScriptableObject, ISkillEffectType
            => new(asset);

        public bool Validate(out string error)
        {
            if (_asset == null)
            {
                error = "Assign an effect type asset.";
                return false;
            }
            if (_asset is not ISkillEffectType type)
            {
                error = $"'{_asset.name}' does not implement {nameof(ISkillEffectType)}.";
                return false;
            }

            Type parametersType = type.ParametersType;
            if (parametersType == null || !parametersType.IsClass || parametersType.IsAbstract ||
                parametersType.ContainsGenericParameters || !parametersType.IsSerializable ||
                !typeof(ISkillEffectParameters).IsAssignableFrom(parametersType) ||
                typeof(UnityEngine.Object).IsAssignableFrom(parametersType))
            {
                error = $"Effect type '{_asset.name}' must declare a concrete [Serializable] managed class implementing {nameof(ISkillEffectParameters)}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
