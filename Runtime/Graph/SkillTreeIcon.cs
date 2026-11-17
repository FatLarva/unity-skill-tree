using UnityEngine;

namespace Game.SkillTree
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillTreeIcon : UnityEngine.UI.MaskableGraphic
    {
        private const float PixelScaleEpsilon = 0.000001f;

        private SkillTreeNode _node;
        private SkillTreeNodeIconRenderer _renderer;
        private Vector2 _pixelScale;
        private float _opacity = 1f;
        private float _revealOpacity = 1f;

        public void SetNode(SkillTreeNode node, SkillTreeNodeIconRenderer renderer,
            float opacity = 1f, float revealOpacity = 1f)
        {
            _node = node;
            _renderer = renderer;
            _opacity = opacity;
            _revealOpacity = revealOpacity;
            raycastTarget = false;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            Vector2 scale = new SkillTreeStroke.PixelSpace(this).Scale;
            if ((scale - _pixelScale).sqrMagnitude > PixelScaleEpsilon)
            {
                _pixelScale = scale;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float size = Mathf.Min(rect.width, rect.height);
            if (_node == null || size <= 0f)
            {
                return;
            }

            var pixels = new SkillTreeStroke.PixelSpace(this);
            _pixelScale = pixels.Scale;
            float radius = _node.Kind switch
            {
                SkillTreeNodeKind.Root => 22f,
                SkillTreeNodeKind.Branch => 5.7f,
                _ => 10f
            };
            float zoom = size / (radius * 2.8f);
            var context = new SkillTreeNodeIconContext(rect.center, radius * zoom, zoom, _opacity, _revealOpacity, pixels);
            if (_renderer != null)
            {
                _renderer.DrawIcon(mesh, _node, context);
            }
            else
            {
                SkillTreeNodeIconRenderer.DrawDefaultIcon(mesh, _node, context);
            }
        }
    }
}
