using System;
using UnityEngine;
using UnityTools;

namespace Game.SkillTree
{
    internal sealed class SkillTreeIconView : IDisposable
    {
        private readonly Transform _parent;
        private SkillTreeNodeIconRenderer _renderer;
        private SkillTreeNode _previous;
        private float _opacity;
        private float _revealOpacity;
        private SkillTreeIconMode _mode;

        public UnityEngine.UI.Graphic Graphic { get; private set; }

        public SkillTreeIconView(Transform parent) => _parent = parent;

        public void SetNode(SkillTreeNode node, SkillTreeNodeIconRenderer renderer,
            float opacity = 1f, float revealOpacity = 1f)
        {
            if (Graphic == null || _mode != node.IconMode ||
                node.IconMode == SkillTreeIconMode.GraphicIcon && _renderer != renderer)
            {
                Dispose();
                _mode = node.IconMode;
                _renderer = renderer;
                if (_mode == SkillTreeIconMode.Sprite)
                {
                    var child = new GameObject("Sprite", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    child.transform.SetParent(_parent, false);
                    var image = child.GetComponent<UnityEngine.UI.Image>();
                    image.preserveAspect = true;
                    Graphic = image;
                }
                else
                {
                    Graphic = renderer.CreateIcon(_parent);
                    if (Graphic == null) throw new InvalidOperationException("An icon renderer must create a Graphic.");
                }
                Graphic.gameObject.hideFlags = HideFlags.DontSave;
                Graphic.raycastTarget = false;
            }
            if (_previous != null && _previous.HasSameValues(node) &&
                _opacity == opacity && _revealOpacity == revealOpacity) return;
            if (_mode == SkillTreeIconMode.Sprite)
            {
                var image = (UnityEngine.UI.Image)Graphic;
                image.sprite = node.IconSprite;
                image.color = node.Color.WithAlpha(node.Color.a * opacity);
                image.enabled = node.IconSprite != null;
            }
            else
            {
                renderer.UpdateIcon(Graphic, node, opacity, revealOpacity);
            }
            _previous = node.Clone();
            _opacity = opacity;
            _revealOpacity = revealOpacity;
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
