using UnityEditor;

namespace Game.SkillTree.InputSystem.Editor
{
    internal static class InputSystemModifierEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            SkillTreeModifierDefaults.RegisterDefaultProvider(InputSystemSkillTreeModifiers.Instance);
        }
    }
}
