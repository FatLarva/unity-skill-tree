using Game.SkillTree.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomEditor(typeof(SkillNodeLayoutConfig))]
    public sealed class SkillNodeLayoutConfigEditor : SkillTreeInspectorBase
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = BeginInspector();
            var layout = (SkillNodeLayoutConfig)target;
            root.Add(SkillTreeInspectorFields.Appearance(layout, Bindings, SkillTreeAssetEditor.FindGraph(layout)));
            return root;
        }
    }
}
