using UnityEngine;

namespace Game.SkillTree.Data
{
    public abstract class SkillTreeInspectorContext : ScriptableObject
    {
        public SkillTreeConfig Tree { get; private set; }
        public SkillTreeGraph Graph { get; private set; }

        public void Initialize(SkillTreeConfig tree, SkillTreeGraph graph)
        {
            Tree = tree;
            Graph = graph;
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        }

        public virtual void Release() { }
    }
}
