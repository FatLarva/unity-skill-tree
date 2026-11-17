using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private Rect GetConnectionViewport(float width)
        {
            // Ribbon limits its joins to a 2.5 miter. Include its antialiased
            // edge and caps, plus the curve tolerance, before rejecting a path.
            float pixelsPerUnit = Mathf.Max(0.001f, Mathf.Min(_pixelScale.x, _pixelScale.y));
            float padding = Mathf.Max(0f, width) * 0.5f * 2.5f + (2f + _curveTolerancePixels) / pixelsPerUnit;
            Rect viewport = rectTransform.rect;
            return Rect.MinMaxRect(viewport.xMin - padding, viewport.yMin - padding,
                viewport.xMax + padding, viewport.yMax + padding);
        }

        private static bool ConnectionHullIntersectsViewport(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Rect viewport)
        {
            Vector2 minimum = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            Vector2 maximum = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            return maximum.x >= viewport.xMin && minimum.x <= viewport.xMax &&
                maximum.y >= viewport.yMin && minimum.y <= viewport.yMax;
        }

        private static bool ConnectionPathIntersectsViewport(IReadOnlyList<SkillTreeStroke.Point> path, Rect viewport)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 start = path[i - 1].Position;
                Vector2 delta = path[i].Position - start;
                float enter = 0f;
                float exit = 1f;
                if (ClipConnectionAxis(start.x, delta.x, viewport.xMin, viewport.xMax, ref enter, ref exit) &&
                    ClipConnectionAxis(start.y, delta.y, viewport.yMin, viewport.yMax, ref enter, ref exit))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ClipConnectionAxis(float origin, float direction, float minimum, float maximum,
            ref float enter, ref float exit)
        {
            if (direction == 0f) return origin >= minimum && origin <= maximum;

            float first = (minimum - origin) / direction;
            float last = (maximum - origin) / direction;
            if (first > last) (first, last) = (last, first);
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, last);
            return enter <= exit;
        }
    }
}
