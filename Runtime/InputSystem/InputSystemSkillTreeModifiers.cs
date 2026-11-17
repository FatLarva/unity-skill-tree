using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.SkillTree.InputSystem
{
    /// <summary>Shift and Alt through the optional Input System package.</summary>
    public sealed class InputSystemSkillTreeModifiers : ISkillTreeModifiers
    {
        public static InputSystemSkillTreeModifiers Instance { get; } = new();

        public bool MoveSubtree => Keyboard.current?.shiftKey.isPressed == true;
        public bool ToggleAdditionalParent => Keyboard.current?.altKey.isPressed == true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            SkillTreeModifierDefaults.RegisterDefaultProvider(Instance);
        }
    }
}
