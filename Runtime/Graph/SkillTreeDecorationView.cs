using System;
using UnityEngine;

namespace Game.SkillTree
{
    internal sealed class SkillTreeDecorationView : IDisposable
    {
        private readonly Transform _parent;
        private SkillTreeNodeDecorationRenderer _renderer;
        private int _rendererRevision;
        private SkillTreeNode _previous;
        private SkillTreeNodeDecorationContext _context;

        public UnityEngine.UI.Graphic Graphic { get; private set; }

        public SkillTreeDecorationView(Transform parent) => _parent = parent;

        public void SetNode(SkillTreeNode node, SkillTreeNodeDecorationRenderer renderer,
            SkillTreeNodeDecorationContext context)
        {
            if (Graphic == null || _renderer != renderer || _rendererRevision != renderer.Revision)
            {
                Dispose();
                _renderer = renderer;
                _rendererRevision = renderer.Revision;
                Graphic = renderer.CreateDecoration(_parent);
                if (Graphic == null) throw new InvalidOperationException("A decoration renderer must create a Graphic.");
                Graphic.gameObject.hideFlags = HideFlags.DontSave;
                Graphic.raycastTarget = false;
            }
            if (_previous != null && _previous.HasSameValues(node) &&
                _context.Radius == context.Radius && _context.SelectionRadius == context.SelectionRadius &&
                _context.Opacity == context.Opacity && _context.RevealOpacity == context.RevealOpacity &&
                _context.Selected == context.Selected) return;

            renderer.UpdateDecoration(Graphic, node, context);
            _previous = node.Clone();
            _context = context;
        }

        public void SetActive(bool active)
        {
            if (Graphic != null && Graphic.gameObject.activeSelf != active) Graphic.gameObject.SetActive(active);
        }

        public void Dispose()
        {
            if (Graphic != null)
            {
                Graphic.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(Graphic.gameObject);
                else UnityEngine.Object.DestroyImmediate(Graphic.gameObject);
            }
            Graphic = null;
            _previous = null;
        }
    }
}
