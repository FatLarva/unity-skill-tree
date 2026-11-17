using Game.SkillTree.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomEditor(typeof(SkillNodeConfig))]
    public sealed class SkillNodeConfigEditor : SkillTreeInspectorBase
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = BeginInspector();
            root.Add(SkillTreeInspectorFields.Gameplay((SkillNodeConfig)target, Bindings));
            return root;
        }
    }
}
