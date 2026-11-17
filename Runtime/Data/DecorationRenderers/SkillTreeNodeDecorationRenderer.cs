using System;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    public readonly struct SkillTreeNodeDecorationContext
    {
        public float Radius { get; }
        public float SelectionRadius { get; }
        public float Opacity { get; }
        public float RevealOpacity { get; }
        public bool Selected { get; }

        public SkillTreeNodeDecorationContext(float radius, float selectionRadius,
            float opacity, float revealOpacity, bool selected)
        {
            Radius = radius;
            SelectionRadius = selectionRadius;
            Opacity = opacity;
            RevealOpacity = revealOpacity;
            Selected = selected;
        }
    }

    [CreateAssetMenu(menuName = "Game/Skill Tree/Decoration Renderer", fileName = "NodeDecorations")]
    public class SkillTreeNodeDecorationRenderer : ScriptableObject
    {
        [SerializeField, Tooltip("Graphic prefab for node progress and selection. Its children may use any UI Graphics.")]
        private UnityEngine.UI.Graphic _prefab;

        private int _revision;
        public int Revision => _revision;

        public void NotifyChanged() => _revision = unchecked(_revision + 1);

        protected virtual void OnValidate() => NotifyChanged();

        /// <summary>Creates a Graphic owned by the graph. Override to build another decoration hierarchy.</summary>
        public virtual UnityEngine.UI.Graphic CreateDecoration(Transform parent)
        {
            if (_prefab == null) throw new InvalidOperationException("Assign a node decoration Graphic prefab.");
            return Instantiate(_prefab, parent, false);
        }

        /// <summary>The graph controls position, zoom, visibility and lifetime of the returned Graphic.</summary>
        public virtual void UpdateDecoration(UnityEngine.UI.Graphic graphic, SkillTreeNode node,
            SkillTreeNodeDecorationContext context)
        {
            if (graphic is SkillTreeNodeDecorationGraphic decoration)
                decoration.SetNode(node, context);
            else if (graphic != null)
                graphic.color = node.Color.WithAlpha(node.Color.a * context.Opacity);
        }
    }
}
