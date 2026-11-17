using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [Serializable]
    public struct SkillTreeEnumSettings
    {
        [SerializeField] private bool _enabled;
        [SerializeField] private string _namespace;
        [SerializeField] private string _typeName;
        [SerializeField, Tooltip("Assembly which receives the generated enum (the asmdef name, or Assembly-CSharp).")]
        private string _assemblyName;

        public bool Enabled => _enabled;
        public string Namespace => _namespace;
        public string TypeName => _typeName;
        public string AssemblyName => _assemblyName;

        public SkillTreeEnumSettings(bool enabled, string @namespace, string typeName, string assemblyName)
        {
            _enabled = enabled;
            _namespace = @namespace ?? string.Empty;
            _typeName = typeName ?? string.Empty;
            _assemblyName = assemblyName ?? string.Empty;
        }

        public static SkillTreeEnumSettings Default => new(true, "Game.Skills", "SkillId", "Assembly-CSharp");
    }
}
