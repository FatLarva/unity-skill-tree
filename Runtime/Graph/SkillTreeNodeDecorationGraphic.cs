using UnityEngine;
using UnityTools;
using UnityTools.GraphicPrimitives;

namespace Game.SkillTree
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillTreeNodeDecorationGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private EllipseGraphic _emptyArcPrefab;
        [SerializeField] private EllipseGraphic _learnedArcPrefab;
        [SerializeField] private EllipseGraphic _completedRing;
        [SerializeField] private EllipseGraphic _selectionRing;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("Visible angular gap between the round caps of adjacent levels, in degrees.")]
        private float _levelGapAngle = 12f;

        private EllipseGraphic _emptyLevels;
        private EllipseGraphic _learnedLevels;
        private bool _reportedMissingReferences;

        public void SetNode(SkillTreeNode node, SkillTreeNodeDecorationContext context)
        {
            raycastTarget = false;
            if (node == null)
            {
                HideArcs();
                SetActive(_completedRing, false);
                SetActive(_selectionRing, false);
                return;
            }
            if (_emptyArcPrefab == null || _learnedArcPrefab == null || _completedRing == null || _selectionRing == null)
            {
                if (!_reportedMissingReferences)
                {
                    Debug.LogError("Assign the empty and learned arc prefabs, completed ring, and selection ring on the skill tree decoration prefab.", this);
                    _reportedMissingReferences = true;
                }
                HideArcs();
                SetActive(_completedRing, false);
                SetActive(_selectionRing, false);
                return;
            }
            _reportedMissingReferences = false;

            SetActive(_selectionRing, context.Selected);
            if (context.Selected)
            {
                SetEllipse(_selectionRing, context.SelectionRadius, 0f, 360f,
                    node.Color.WithAlpha(node.Color.a * 0.85f * context.RevealOpacity));
            }

            int level = Mathf.Clamp(node.Level, 0, Mathf.Max(0, node.MaxLevel));
            bool completed = node.MaxLevel > 0 && level == node.MaxLevel;
            SetActive(_completedRing, completed);
            if (completed)
            {
                HideArcs();
                SetEllipse(_completedRing, context.Radius + 2f, 0f, 360f,
                    node.Color.WithAlpha(node.Color.a * context.Opacity));
                return;
            }
            if (node.MaxLevel <= 0)
            {
                HideArcs();
                return;
            }

            Color empty = Color.Lerp(node.Color, new Color(0.18f, 0.22f, 0.2f), 0.65f)
                .WithAlpha(node.Color.a * context.Opacity * 0.6f);
            Color learned = node.Color.WithAlpha(node.Color.a * context.Opacity);
            float step = 360f / node.MaxLevel;
            SetLevels(ref _learnedLevels, _learnedArcPrefab, "Learned Levels", level, 0f, step,
                context.Radius + 2f, learned);
            SetLevels(ref _emptyLevels, _emptyArcPrefab, "Empty Levels", node.MaxLevel - level,
                step * level, step, context.Radius + 2f, empty);
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh) => mesh.Clear();

        private void SetLevels(ref EllipseGraphic graphic, EllipseGraphic prefab, string objectName,
            int count, float startAngle, float step, float radius, Color tint)
        {
            if (count <= 0)
            {
                SetActive(graphic, false);
                return;
            }
            if (graphic == null)
            {
                graphic = Instantiate(prefab, transform, false);
                graphic.name = objectName;
                graphic.gameObject.hideFlags = HideFlags.DontSave;
            }
            SetActive(graphic, true);
            graphic.SetDashesByCount(count, _levelGapAngle);
            SetEllipse(graphic, radius, startAngle, step * count, tint);
        }

        private void SetEllipse(EllipseGraphic graphic, float radius, float startDegrees, float sweepDegrees, Color tint)
        {
            var rect = graphic.rectTransform;
            Vector2 anchor = rectTransform.pivot;
            Vector2 size = Vector2.one * (Mathf.Max(0f, radius) * 2f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, -startDegrees);
            if (rect.anchorMin != anchor) rect.anchorMin = anchor;
            if (rect.anchorMax != anchor) rect.anchorMax = anchor;
            if (rect.pivot != Vector2.one * 0.5f) rect.pivot = Vector2.one * 0.5f;
            if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            if (rect.localRotation != rotation) rect.localRotation = rotation;
            if (rect.localScale != Vector3.one) rect.localScale = Vector3.one;
            graphic.raycastTarget = false;
            graphic.maskable = maskable;
            graphic.SweepAngle = -sweepDegrees;
            if (graphic.canvasRenderer.GetColor() != tint) graphic.canvasRenderer.SetColor(tint);
        }

        private void HideArcs()
        {
            SetActive(_emptyLevels, false);
            SetActive(_learnedLevels, false);
        }

        private static void SetActive(EllipseGraphic graphic, bool active)
        {
            if (graphic != null && graphic.gameObject.activeSelf != active) graphic.gameObject.SetActive(active);
        }
    }
}
