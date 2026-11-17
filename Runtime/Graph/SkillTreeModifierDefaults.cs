using System;
using UnityEngine;

namespace Game.SkillTree
{
    /// <summary>Default provider installed by the available input integration.</summary>
    public static class SkillTreeModifierDefaults
    {
        private static ISkillTreeModifiers _current = LegacySkillTreeModifiers.Instance;

        public static ISkillTreeModifiers Current => _current;

        /// <summary>
        /// Used by input integrations at initialization. For a single graph, assign Graph.Modifiers instead.
        /// </summary>
        public static void RegisterDefaultProvider(ISkillTreeModifiers provider)
        {
            _current = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }

    /// <summary>Disables editing modifiers, for example on a touch-only interface.</summary>
    public sealed class NoSkillTreeModifiers : ISkillTreeModifiers
    {
        public static NoSkillTreeModifiers Instance { get; } = new();
        public bool MoveSubtree => false;
        public bool ToggleAdditionalParent => false;
    }

    /// <summary>
    /// Shift and Alt through the legacy Input Manager. Returns false when that backend is disabled.
    /// </summary>
    public sealed class LegacySkillTreeModifiers : ISkillTreeModifiers
    {
        public static LegacySkillTreeModifiers Instance { get; } = new();

        public bool MoveSubtree
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
                return false;
#endif
            }
        }

        public bool ToggleAdditionalParent
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#else
                return false;
#endif
            }
        }
    }
}
