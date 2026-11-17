using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    public abstract class SkillTreeInspectorBase : UnityEditor.Editor
    {
        protected readonly List<SerializedObject> Bindings = new();
        protected VisualElement RootElement;

        protected VisualElement BeginInspector()
        {
            ReleaseBindings();
            RootElement = SkillTreeInspectorFields.Root();
            return RootElement;
        }

        protected virtual void OnDisable() => ReleaseBindings();

        private void ReleaseBindings()
        {
            RootElement?.Unbind();
            foreach (var binding in Bindings)
            {
                binding.Dispose();
            }
            Bindings.Clear();
            RootElement = null;
        }
    }
}
