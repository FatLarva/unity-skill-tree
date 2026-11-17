using System;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Game.SkillTree
{
    internal static class RoundedUiGraphic
    {
        [Serializable]
        private sealed class ShapeSettings
        {
            // Field names are the serialized names in Fat Larva's RoundedRectGraphic.
            public float _cornerRadius;
            public int _cornerQuality = 8;
            public float _antiAliasingBorderThickness = 1f;
        }

        public static UnityEngine.UI.Image Add(GameObject target, Color color, float radius = 12f)
        {
            var graphic = target.AddComponent<RoundedRectGraphic>();
            graphic.color = color;

            var size = graphic.rectTransform.rect.size;
            float shortSide = Mathf.Min(size.x, size.y);
            var settings = new ShapeSettings
            {
                _cornerRadius = shortSide > 0f ? Mathf.Clamp(radius / shortSide, 0f, 0.5f) : 0.2f,
            };

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings), graphic);
            graphic.SetVerticesDirty();
            return graphic;
        }
    }
}
